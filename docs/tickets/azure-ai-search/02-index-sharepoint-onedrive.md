# Ticket 2 - Indexer SharePoint et OneDrive

## Objectif

Creer un processus backend qui copie les documents autorises depuis un site SharePoint de test vers Azure AI Search. Le meme mecanisme couvrira ensuite OneDrive.

La premiere implementation utilise Microsoft Graph et l'API d'envoi de documents Azure AI Search. Elle ne depend pas de l'indexeur SharePoint natif d'Azure AI Search, car ses fonctions avancees de permissions sont encore en preversion.

## Dependances

- le ticket 1 est termine
- un site SharePoint de test existe
- le site contient quelques documents fictifs avec des permissions differentes
- un administrateur Microsoft 365 peut accorder les permissions necessaires

## Etape 1 - Creer l'identite d'ingestion

Dans Microsoft Entra ID :

1. Creer une inscription d'application pour l'ingestion, par exemple `AssistantCore Microsoft Ingestion Dev`.
2. Choisir une authentification applicative, sans utilisateur interactif.
3. Preferer un certificat ou une identite managee/federee en environnement heberge.
4. Pour un test local seulement, stocker un secret court dans `dotnet user-secrets`.
5. Ne jamais ajouter le secret, le certificat ou un token au depot.

Commencer avec un acces limite aux sites explicitement selectionnes, par exemple la permission applicative Microsoft Graph `Sites.Selected`, lorsque les operations necessaires sont supportees. Une permission tenant-wide comme `Sites.Read.All` donne acces a beaucoup plus de contenu et demande une validation de securite explicite.

Un administrateur doit accorder le consentement, puis autoriser l'application sur le site pilote. Documenter le site autorise, la permission accordee et la personne qui l'a approuvee, sans consigner de secret.

## Etape 2 - Creer le processus d'ingestion

Le processus d'ingestion doit etre separe du handler `/messages`. Il peut etre execute par un worker, un job planifie ou une fonction Azure.

Respecter les responsabilites suivantes :

- une abstraction Application decrit la lecture des contenus Microsoft
- un adaptateur Infrastructure appelle Microsoft Graph
- une abstraction Application decrit l'ecriture dans l'index
- un adaptateur Infrastructure appelle Azure AI Search
- un service d'ingestion orchestre la synchronisation
- la persistance conserve l'etat de synchronisation par organisation et source

Le choix exact de l'hebergement peut rester local au developpeur, mais le job ne doit pas etre execute dans un controller HTTP longue duree.

## Etape 3 - Realiser la synchronisation initiale

Pour le site pilote :

1. Recuperer les bibliotheques de documents autorisees.
2. Parcourir les fichiers supportes.
3. Ignorer les dossiers, fichiers temporaires et formats non supportes.
4. Telecharger le contenu avec Microsoft Graph.
5. Extraire le texte lisible.
6. Lire l'identifiant, le titre, l'URL, la date de modification et les permissions.
7. Decouper le texte en passages avec un leger chevauchement.
8. Generer l'embedding de chaque passage si la recherche vectorielle est activee.
9. Envoyer les passages vers Azure AI Search par lots.

Chaque passage doit recevoir une cle deterministe construite a partir de l'organisation, de la source, du document et de la position du passage. Relancer le job ne doit pas creer de doublons.

## Etape 4 - Mapper un document vers l'index

Exemple conceptuel :

```json
{
  "chunkId": "org-001_sharepoint_document-123_0001",
  "organizationId": "org-001",
  "sourceId": "document-123",
  "sourceType": "sharepoint",
  "title": "Politique de vacances",
  "content": "Passage extrait du document...",
  "url": "https://contoso.sharepoint.com/sites/rh/...",
  "modifiedAt": "2026-08-09T12:00:00Z",
  "allowedUserIds": ["entra-user-object-id"],
  "allowedGroupIds": ["entra-group-object-id"],
  "contentVector": [0.0123, -0.0456]
}
```

Les identifiants d'ACL doivent etre les Object IDs Microsoft Entra, pas les adresses courriel ou les noms affiches.

## Etape 5 - Ajouter la synchronisation incrementale

Apres la premiere copie complete, utiliser les requetes delta Microsoft Graph :

1. Executer la requete delta initiale.
2. Suivre chaque `@odata.nextLink` jusqu'a la fin.
3. Conserver le `@odata.deltaLink` final dans la base.
4. A la prochaine execution, reprendre avec ce lien.
5. Reindexer les fichiers ajoutes ou modifies.
6. Supprimer de l'index les passages des fichiers supprimes.
7. Recalculer les ACL lorsqu'un changement de partage est detecte.

Les tokens delta sont opaques et peuvent expirer. Si Graph demande une reinitialisation, le job doit pouvoir refaire une synchronisation complete sans creer de doublons.

## Etape 6 - Gerer les erreurs

Le job doit :

- respecter la pagination Graph
- ralentir et reessayer apres un `429` en suivant `Retry-After`
- distinguer les erreurs temporaires des erreurs permanentes
- poursuivre avec les autres documents lorsqu'un fichier individuel est corrompu
- ne pas masquer l'echec global d'une source
- journaliser les identifiants techniques, jamais le contenu complet ou les tokens
- produire un resume avec nombres de documents ajoutes, modifies, supprimes et echoues

## Formats de premiere version

Commencer par un petit ensemble clairement supporte, par exemple PDF texte, DOCX et TXT. Les images scannees, archives, fichiers chiffres et OCR doivent rester hors perimetre jusqu'a un ticket dedie.

## Tests attendus

- un fichier cree produit des passages dans l'index
- une seconde execution ne cree aucun doublon
- une modification remplace les anciens passages
- une suppression retire tous les passages du document
- un format non supporte est ignore avec un statut explicite
- une erreur `429` est reessayee sans boucle infinie
- deux organisations utilisant le meme `sourceId` ne partagent pas leurs documents

## Criteres d'acceptation

- le site pilote est lu avec une identite applicative approuvee
- au moins un document de chaque format supporte est indexe
- le texte, la provenance et les ACL sont presents dans chaque passage
- la synchronisation incrementale et les suppressions fonctionnent
- les tokens delta sont persistes de facon securisee
- les tests automatises pertinents sont ajoutes
- `dotnet test Solution.sln` reussit

## References

- [Suivre les changements avec Microsoft Graph](https://learn.microsoft.com/en-us/graph/delta-query-overview)
- [Delta pour les fichiers SharePoint et OneDrive](https://learn.microsoft.com/en-us/graph/api/driveitem-delta?view=graph-rest-1.0)
- [Type driveItem](https://learn.microsoft.com/en-us/graph/api/resources/driveitem?view=graph-rest-1.0)

