# Instructions pour Codex — Backend

## Règles de modification

* Ne modifie jamais les fichiers directement sans me montrer un diff clair.
* Propose d’abord un plan court.
* Attends mon approbation avant d’appliquer les changements.
* Garde les changements petits et ciblés.
* Ne touche pas aux fichiers non liés à la demande.
* Explique les impacts avant de modifier plusieurs fichiers.

## Architecture

* Respecter les principes SOLID.
* Respecter Clean Architecture.
* Séparer clairement :

  * API / Controllers / Endpoints
  * Application / Use Cases / Services applicatifs
  * Domain / Entities / Value Objects / Interfaces métier
  * Infrastructure / Repositories / External services
  * Persistence / EF Core / SQL / Migrations
  * Configuration / Dependency Injection
* Éviter la logique métier dans les controllers.
* Éviter que la couche Domain dépende d’Infrastructure ou d’EF Core.
* Favoriser des services petits, testables et lisibles.

## Qualité du code

* C# moderne et lisible.
* Nullable reference types respectés.
* Pas de code dupliqué inutile.
* Noms de classes, méthodes et variables explicites.
* Ajouter ou ajuster les tests quand c’est pertinent.
* Gérer les erreurs proprement sans masquer les exceptions importantes.
* Utiliser async/await correctement pour les appels I/O.
* Ne pas introduire de dépendances inutiles.


## Handlers
- Toute action applicative doit passer par un handler.
- Le handler doit uniquement orchestrer l’appel aux couches nécessaires.
- Le handler ne doit pas contenir de logique métier pure.
- Le handler ne doit pas contenir de calcul métier, règle de décision complexe ou validation métier avancée.
- La logique métier doit être placée dans :
  - Domain services
  - Entities
  - Value Objects
  - Policies
  - Specifications
  - Application services dédiés si nécessaire
- Le handler peut seulement :
  - recevoir la commande/requête
  - appeler les services/domain/repositories nécessaires
  - gérer le flux simple
  - retourner le résultat
- Les controllers/endpoints doivent appeler le handler, pas directement les services métier.
