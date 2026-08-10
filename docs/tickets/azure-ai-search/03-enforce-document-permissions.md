# Ticket 3 - Appliquer les permissions aux recherches

## Objectif

Garantir qu'un utilisateur ne peut retrouver que les passages autorises par son organisation et par les permissions Microsoft 365 du document source.

Ce ticket est une barriere de securite. La recherche ne doit pas etre branchee a `/messages` tant que ces scenarios ne sont pas valides.

## Regle principale

Une correspondance textuelle ou vectorielle ne donne jamais un droit d'acces. Le backend doit ajouter un filtre de securite a chaque requete Azure AI Search, meme lorsque le modele ne demande aucun filtre.

## Etape 1 - Definir les identifiants de securite

Utiliser exclusivement :

- l'identifiant interne de l'organisation AssistantCore pour `organizationId`
- le claim Entra `oid` pour l'utilisateur
- les Object IDs Entra pour les groupes

Ne pas utiliser les courriels et noms de groupes. Ils peuvent changer et ne constituent pas des identifiants stables.

## Etape 2 - Obtenir les groupes du membre

Definir une abstraction backend qui retourne les groupes de securite applicables au membre courant.

L'implementation peut lire les groupes depuis Microsoft Graph ou depuis un cache synchronise. Elle doit gerer :

- l'appartenance directe
- les groupes Microsoft 365 et groupes de securite retenus par la politique
- les utilisateurs membres de nombreux groupes
- l'expiration du cache
- l'indisponibilite de Microsoft Graph

Le handler `/messages` ne doit pas contenir cette logique. Il demande un contexte de recherche autorise a un service dedie.

## Etape 3 - Construire le filtre

Le filtre doit toujours combiner le tenant et les ACL :

```text
organizationId eq '<organizationId>'
and (
  allowedUserIds contient '<entraObjectId>'
  or allowedGroupIds contient un des groupes du membre
)
```

Utiliser les fonctions de filtre Azure AI Search prevues pour les collections, notamment `search.in` lorsque cela convient. Echappez les valeurs avec le SDK ou une construction de filtre testee; ne concatenez pas directement du texte provenant du modele ou du frontend.

Les champs `allowedUserIds` et `allowedGroupIds` doivent etre `filterable` et non `retrievable`.

## Etape 4 - Decider le comportement d'echec

En cas d'impossibilite de connaitre les permissions, refuser la recherche. Il est interdit de retirer le filtre pour rendre la recherche disponible.

Exemples :

- aucun utilisateur authentifie : ne pas appeler Azure AI Search
- groupes indisponibles et acces utilisateur insuffisant : retourner un echec controle
- ACL absentes sur un document : document non visible par defaut
- organisation absente du contexte : ne pas appeler Azure AI Search

## Etape 5 - Tester l'isolation

Creer des donnees de test avec :

- deux organisations
- deux utilisateurs de la meme organisation
- un groupe partage par certains utilisateurs seulement
- un document public au groupe
- un document accorde directement a un utilisateur
- un document sans ACL

Executer une recherche avec le titre exact de chaque document. Un document interdit ne doit jamais apparaitre dans les resultats, les logs, les preuves ou les sources finales.

## Changements de permissions

Lorsqu'un utilisateur perd un droit dans SharePoint, l'index peut rester temporairement obsolete jusqu'a la prochaine synchronisation. Le pipeline doit donc synchroniser les ACL frequemment et mesurer ce delai.

Avant une mise en production, l'equipe doit definir le delai maximal acceptable entre une modification de permission et son application dans la recherche.

## Tests attendus

- filtre d'organisation toujours present
- acces direct par `allowedUserIds`
- acces par `allowedGroupIds`
- refus d'un utilisateur non autorise
- refus d'un document sans ACL
- refus lorsque le contexte de securite est incomplet
- caracteres speciaux correctement echappes
- aucune ACL retournee dans les resultats normalises

## Criteres d'acceptation

- les filtres sont construits exclusivement dans le backend
- le modele et le frontend ne peuvent pas modifier les champs de securite
- les tests couvrent les acces autorises et interdits
- une panne de resolution des droits ferme l'acces au lieu de l'ouvrir
- le delai de synchronisation des ACL est mesure et documente
- `dotnet test Solution.sln` reussit

## References

- [Modele de filtre de securite Azure AI Search](https://learn.microsoft.com/en-us/azure/search/search-security-trimming-for-azure-search)
- [Controle d'acces au niveau document](https://learn.microsoft.com/en-us/azure/search/search-document-level-access-overview)
- [Indexer des ACL avec l'API push](https://learn.microsoft.com/en-us/azure/search/search-index-access-control-lists-and-rbac-push-api)

