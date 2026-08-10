# Ticket 6 - Indexer Outlook dans Azure AI Search

<a id="search-06-objective"></a>
## Table des matieres

- [Objectif](#objectif)
- [Dependances](#dependances)
- [Decisions obligatoires](#decisions-obligatoires-avant-le-developpement)
- [Configurer l'acces Outlook](#etape-1---configurer-lacces-outlook)
- [Creer le job d'ingestion](#etape-2---creer-le-job-dingestion)
- [Synchronisation initiale](#etape-3---realiser-la-synchronisation-initiale)
- [Mapping vers l'index](#etape-4---mapper-le-contenu-vers-lindex)
- [Synchronisation incrementale](#etape-5---synchroniser-les-changements-et-suppressions)
- [Integration avec messages](#etape-6---activer-outlook-dans-loutil-logique)
- [Securite](#securite)
- [Gestion des erreurs](#gestion-des-erreurs)
- [Tests](#tests-attendus)
- [Criteres d'acceptation](#criteres-dacceptation)
- [Hors perimetre](#hors-perimetre)

## Objectif

Ajouter les contenus Outlook explicitement autorises dans Azure AI Search afin que l'outil `search_microsoft_365` puisse les rechercher sans appeler Outlook pendant une question utilisateur.

Ce ticket vient apres le MVP SharePoint et OneDrive. Il ne doit pas commencer avant que l'isolation par organisation et les ACL soient validees.

## Dependances

- l'infrastructure Azure AI Search est disponible
- le pipeline SharePoint et OneDrive fournit un exemple d'ingestion fonctionnel
- les filtres d'organisation et d'ACL sont testes
- l'adaptateur de recherche backend fonctionne
- un administrateur Microsoft 365 peut approuver les permissions Graph
- la politique de retention Outlook est validee

## Decisions obligatoires avant le developpement

L'equipe doit documenter et faire approuver :

- si le perimetre couvre les courriels, les evenements ou les deux
- les utilisateurs, groupes ou boites autorises
- l'inclusion ou non des boites partagees
- l'inclusion ou non des pieces jointes
- la duree de retention dans l'index
- le traitement des contenus sensibles et confidentiels
- le delai maximal de retrait apres une suppression ou perte d'acces

Une boite ne doit jamais etre indexee seulement parce qu'elle appartient au tenant.

## Etape 1 - Configurer l'acces Outlook

Dans Microsoft Entra ID :

1. Reutiliser l'identite d'ingestion seulement si sa responsabilite et ses permissions restent claires; sinon creer une inscription dediee.
2. Ajouter uniquement les permissions Microsoft Graph necessaires au perimetre approuve.
3. Limiter l'application aux boites autorisees avec le mecanisme de controle d'acces retenu par l'organisation.
4. Faire accorder le consentement par un administrateur Microsoft 365.
5. Utiliser une identite managee, une federation ou un certificat en environnement heberge.
6. Utiliser un secret court dans `dotnet user-secrets` seulement pour un test local.
7. Documenter les permissions et leur justification sans enregistrer de secret.

Les permissions applicatives Outlook sont sensibles. Une permission large doit faire l'objet d'une validation de securite explicite.

## Etape 2 - Creer le job d'ingestion

Le job doit etre separe de `/api/messages` et du handler `SendMessage`.

Il doit :

- charger les organisations et boites Outlook activees
- obtenir un token Graph avec l'identite d'ingestion
- lire uniquement les dossiers et types de contenus approuves
- extraire les champs autorises
- convertir le HTML en texte lisible
- retirer les signatures ou citations repetees seulement selon une regle testee
- decouper le contenu en passages
- generer les embeddings lorsque la recherche vectorielle est activee
- envoyer les passages dans Azure AI Search par lots
- produire un resume d'execution sans contenu utilisateur

## Etape 3 - Realiser la synchronisation initiale

Pour chaque boite autorisee :

1. Parcourir les dossiers approuves avec pagination.
2. Respecter une periode maximale initiale, par exemple les derniers mois retenus par la politique.
3. Ignorer les brouillons et dossiers exclus si la politique le demande.
4. Recuperer les messages ou evenements et leurs identifiants stables.
5. Recuperer les informations necessaires aux ACL.
6. Transformer et indexer les contenus par lots.
7. Enregistrer un point de reprise par organisation, boite et type de ressource.

Le job doit etre relancable sans creer de doublons.

## Etape 4 - Mapper le contenu vers l'index

Chaque passage Outlook contient au minimum :

```json
{
  "chunkId": "org-001_outlook_message-123_0001",
  "organizationId": "org-001",
  "sourceId": "message-123",
  "sourceType": "outlook",
  "title": "Objet du courriel",
  "content": "Passage autorise...",
  "url": "https://outlook.office.com/...",
  "modifiedAt": "2026-08-09T12:00:00Z",
  "allowedUserIds": ["entra-user-object-id"],
  "allowedGroupIds": [],
  "contentVector": [0.0123, -0.0456]
}
```

Les adresses, destinataires et autres metadonnees ne sont indexees que si un besoin fonctionnel et une autorisation les justifient.

## Etape 5 - Synchroniser les changements et suppressions

Utiliser les mecanismes delta Microsoft Graph supportes :

1. Conserver les `deltaLink` de facon securisee.
2. Reprendre toutes les pages via `nextLink`.
3. Reindexer les contenus modifies.
4. Supprimer tous les passages d'un element supprime.
5. Retirer les contenus d'une boite desactivee.
6. Refaire une synchronisation complete si le token delta expire.
7. Accepter les relectures d'un meme changement sans produire de doublon.

## Etape 6 - Activer Outlook dans l'outil logique

Ajouter `outlook` aux `sourceTypes` de `search_microsoft_365` seulement si :

- la source est configuree pour l'organisation
- une synchronisation recente a reussi
- les ACL sont presentes
- la politique de retention est active

Le modele ne voit ni les boites, ni les tokens, ni les filtres Graph.

## Securite

- une ACL inconnue rend le passage invisible
- les ACL sont appliquees dans Azure AI Search avant le retour des resultats
- les sujets, adresses et contenus ne sont pas journalises
- les tokens et identifiants de connexion ne sont jamais envoyes au modele
- une organisation ne peut jamais rechercher l'index d'une autre organisation
- la suppression de la source retire ses documents de l'index

## Gestion des erreurs

- respecter `Retry-After` apres un `429`
- borner les retries et les delais
- reprendre apres une erreur temporaire sans recommencer toute la boite
- isoler l'echec d'une boite sans masquer l'echec global du job
- signaler un token delta expire et lancer une resynchronisation controlee
- ne jamais journaliser le corps du message en erreur

## Tests attendus

- message cree, modifie puis supprime
- evenement cree, modifie puis supprime si inclus
- seconde execution sans doublon
- boite autorisee et boite interdite
- utilisateur autorise et utilisateur interdit
- isolation entre deux organisations
- expiration du token delta
- pagination et limitation `429`
- source Outlook masquee lorsqu'elle est desactivee

## Criteres d'acceptation

- le perimetre, les permissions et la retention sont approuves
- seules les boites configurees sont indexees
- les changements et suppressions sont synchronises
- les ACL empechent toute lecture non autorisee
- `search_microsoft_365` peut rechercher Outlook sans exposer Graph
- les tests automatises pertinents sont ajoutes
- `dotnet test Solution.sln` reussit

## Hors perimetre

- envoi ou modification de courriels
- creation ou modification d'evenements
- indexation implicite de toutes les boites du tenant
- analyse des pieces jointes sans decision fonctionnelle separee

