# Ticket 7 - Indexer les messages Teams dans Azure AI Search

<a id="search-07-objective"></a>
## Table des matieres

- [Objectif](#objectif)
- [Dependances](#dependances)
- [Decisions obligatoires](#decisions-obligatoires-avant-le-developpement)
- [Configurer l'acces Teams](#etape-1---configurer-lacces-teams)
- [Creer le job d'ingestion](#etape-2---creer-le-job-dingestion)
- [Synchronisation initiale](#etape-3---realiser-la-synchronisation-initiale)
- [Mapping vers l'index](#etape-4---mapper-le-contenu-vers-lindex)
- [Synchronisation des changements](#etape-5---synchroniser-les-messages-et-les-membres)
- [Integration avec messages](#etape-6---activer-teams-dans-loutil-logique)
- [Securite](#securite)
- [Gestion des erreurs](#gestion-des-erreurs)
- [Tests](#tests-attendus)
- [Criteres d'acceptation](#criteres-dacceptation)
- [Hors perimetre](#hors-perimetre)

## Objectif

Ajouter les messages Teams explicitement autorises dans Azure AI Search afin que `search_microsoft_365` puisse les rechercher sans appeler Teams pendant une question utilisateur.

Les fichiers partages dans Teams sont stockes dans SharePoint ou OneDrive et sont deja couverts par le pipeline documentaire. Ce ticket concerne les messages et leurs reponses, pas les fichiers.

## Dependances

- le MVP SharePoint et OneDrive est valide
- les ACL et l'isolation tenant sont testees
- l'adaptateur Azure AI Search fonctionne
- un administrateur Microsoft 365 peut approuver les permissions Graph
- la politique de retention Teams est validee
- les equipes et canaux pilotes sont identifies

## Decisions obligatoires avant le developpement

L'equipe doit documenter et faire approuver :

- si le perimetre couvre les canaux, les conversations privees ou les deux
- les equipes et canaux autorises
- l'inclusion des reponses dans les fils de discussion
- la duree de retention dans l'index
- le traitement des messages modifies ou supprimes
- le traitement des reactions, mentions et cartes
- le delai maximal de retrait apres un changement de membre
- les contraintes legales, RH et de confidentialite

Les conversations privees demandent une validation de securite distincte. Elles ne sont jamais incluses par defaut.

## Etape 1 - Configurer l'acces Teams

Dans Microsoft Entra ID :

1. Choisir ou creer une identite d'ingestion avec une responsabilite claire.
2. Ajouter uniquement les permissions Graph necessaires au perimetre retenu.
3. Limiter l'acces aux equipes, canaux ou utilisateurs approuves lorsque Microsoft le permet.
4. Faire accorder le consentement par un administrateur Microsoft 365.
5. Utiliser une identite managee, une federation ou un certificat en environnement heberge.
6. Conserver un secret local uniquement dans `dotnet user-secrets` si necessaire.
7. Documenter chaque permission et sa justification.

## Etape 2 - Creer le job d'ingestion

Le job doit etre separe de `/api/messages`.

Il doit :

- charger les organisations et sources Teams activees
- lire uniquement les equipes et canaux configures
- parcourir les messages et leurs reponses
- convertir les contenus supportes en texte lisible
- conserver la provenance equipe, canal, conversation et message
- decouper les contenus longs
- produire les embeddings si la recherche vectorielle est activee
- envoyer les passages vers Azure AI Search par lots
- eviter de reindexer les fichiers deja couverts par SharePoint

## Etape 3 - Realiser la synchronisation initiale

Pour chaque source configuree :

1. Verifier que l'equipe et le canal existent encore.
2. Recuperer les membres et groupes applicables.
3. Parcourir les messages avec pagination.
4. Recuperer les reponses des fils retenus.
5. Limiter la periode initiale selon la politique de retention.
6. Ignorer les contenus non supportes avec un statut explicite.
7. Indexer les messages par lots avec une cle deterministe.
8. Enregistrer un point de reprise par organisation et source.

Relancer le job ne doit pas creer de doublon.

## Etape 4 - Mapper le contenu vers l'index

Chaque passage Teams contient au minimum :

```json
{
  "chunkId": "org-001_teams_message-123_0001",
  "organizationId": "org-001",
  "sourceId": "message-123",
  "sourceType": "teams",
  "title": "Equipe / Canal",
  "content": "Passage autorise...",
  "url": "https://teams.microsoft.com/l/message/...",
  "modifiedAt": "2026-08-09T12:00:00Z",
  "allowedUserIds": [],
  "allowedGroupIds": ["entra-group-object-id"],
  "contentVector": [0.0123, -0.0456]
}
```

La provenance doit permettre de retourner une URL vers le message original sans exposer les ACL au modele.

## Etape 5 - Synchroniser les messages et les membres

Le mecanisme retenu doit :

1. Detecter les nouveaux messages et reponses.
2. Reindexer les messages modifies.
3. Supprimer les passages des messages supprimes.
4. Mettre a jour les ACL apres un ajout ou retrait de membre.
5. Retirer tous les contenus d'une equipe ou d'un canal desactive.
6. Gerer les relectures et evenements en double.
7. Pouvoir relancer une synchronisation complete de facon idempotente.

Si une API delta n'est pas disponible pour un type de contenu, documenter la strategie de polling, sa frequence et son cout.

## Etape 6 - Activer Teams dans l'outil logique

Ajouter `teams` aux `sourceTypes` de `search_microsoft_365` seulement si :

- la source est configuree pour l'organisation
- une synchronisation recente a reussi
- les ACL sont presentes et a jour
- la retention est configuree

Le modele ne choisit jamais une equipe, un canal ou un filtre Graph technique.

## Securite

- aucun canal ou chat n'est indexe par defaut
- une ACL inconnue rend le passage invisible
- un ancien membre perd l'acces apres synchronisation
- les contenus et identites ne sont pas journalises
- les tokens Graph ne sont jamais envoyes au modele ou au frontend
- les messages prives utilisent un perimetre et des controles separes

## Gestion des erreurs

- respecter la pagination et `Retry-After`
- borner retries, parallelisme et delais
- poursuivre les autres canaux apres l'echec controle d'un canal
- conserver un point de reprise fiable
- signaler les permissions retirees ou sources introuvables
- ne jamais masquer un echec global de synchronisation

## Tests attendus

- message et reponse crees, modifies puis supprimes
- seconde execution sans doublon
- equipe ou canal non configure ignore
- membre autorise et ancien membre retire
- isolation entre deux organisations
- absence de doublon pour les fichiers SharePoint
- pagination, limitation `429` et reprise
- source Teams masquee lorsqu'elle est desactivee

## Criteres d'acceptation

- le perimetre et les permissions Teams sont approuves
- seules les sources configurees sont indexees
- les messages et changements de membres sont synchronises
- les fichiers SharePoint ne sont pas dupliques
- les ACL empechent toute lecture non autorisee
- `search_microsoft_365` recherche Teams sans exposer Graph
- les tests automatises pertinents sont ajoutes
- `dotnet test Solution.sln` reussit

## Hors perimetre

- envoi, modification ou suppression de messages Teams
- indexation implicite de toutes les equipes du tenant
- reindexation des fichiers SharePoint et OneDrive
- conversations privees sans validation de securite explicite

