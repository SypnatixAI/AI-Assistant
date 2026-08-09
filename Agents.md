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
* Toute nouvelle fonctionnalité ou modification de comportement doit inclure les tests pertinents.
* Après chaque ajout ou modification de fonctionnalité, exécuter la suite complète avec `dotnet test Solution.sln`.
* Vérifier qu’aucun test existant n’est cassé avant de considérer le travail comme terminé.
* Si les tests ne peuvent pas être exécutés, expliquer clairement la raison et ne pas présenter la fonctionnalité comme entièrement validée.
* Gérer les erreurs proprement sans masquer les exceptions importantes.
* Utiliser async/await correctement pour les appels I/O.
* Ne pas introduire de dépendances inutiles.

## Documentation et issues

* Rédiger les documents et tickets dans un langage simple, clair et concret.
* Éviter les formulations vagues qui obligent le lecteur à deviner le travail attendu.
* Expliquer l’objectif, le périmètre, les étapes principales et les critères d’acceptation.
* Donner assez de détails techniques pour guider le développeur, sans décrire toute l’implémentation ligne par ligne.
* Préciser les contraintes importantes d’architecture, de sécurité et de comportement.
* Ne pas imposer les noms de toutes les classes, méthodes ou colonnes lorsque ce choix peut raisonnablement être laissé au développeur.
* Utiliser un exemple court lorsqu’il rend le résultat attendu plus facile à comprendre.
* Un ticket doit pouvoir être commencé sans clarification majeure, tout en laissant au développeur les décisions techniques locales.


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
