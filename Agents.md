# Instructions pour Codex — Backend

## Règles de modification

* Ne modifie jamais les fichiers directement sans me résumer un plan court.
* Le diff complet n'est pas requis avant modification, sauf si je le demande explicitement.
* Propose d’abord un plan court.
* Attends mon approbation avant d’appliquer les changements.
* Pour une modification minime, tu peux appliquer le changement sans demander une nouvelle approbation.
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
* Tout appel réseau ou SDK vers une API externe doit être implémenté dans le projet `AssistantCore.ExternalServices`.
* La couche Application ne doit jamais dépendre de `AssistantCore.ExternalServices` ni d’un SDK fournisseur.
* Un appel à une API externe doit suivre la chaîne `Provider applicatif -> interface applicative -> Adapter Infrastructure -> client AssistantCore.ExternalServices -> SDK externe`.
* Le provider applicatif ne doit jamais appeler directement un adapter, un client `AssistantCore.ExternalServices` ou le SDK externe. Seuls les adapters Infrastructure et la configuration d’injection de dépendances peuvent référencer `AssistantCore.ExternalServices`.
* Favoriser des services petits, testables et lisibles.
* Respecter le flow applicatif obligatoire : `Controller -> IDispatcher -> CommandHandler -> Application Service -> Repository ou service externe`.
* Un controller doit injecter uniquement `IDispatcher` et ne doit jamais appeler directement un service applicatif, un repository ou un `DbContext`.
* Une commande doit posséder exactement un handler.
* Un handler doit injecter uniquement des interfaces de services applicatifs et ne doit jamais dépendre directement de la persistence ou d’EF Core.
* Toute exception à ces règles doit être discutée et approuvée avant de modifier les tests d’architecture.
* Ne jamais supprimer, désactiver ou assouplir un test d’architecture uniquement pour faire passer le CI.
* Les règles exécutables se trouvent dans `AssistantCore.Architecture.Tests` et doivent réussir avant tout merge vers `master`.

## Qualité du code

* C# moderne et lisible.
* Nullable reference types respectés.
* Pas de code dupliqué inutile.
* Noms de classes, méthodes et variables explicites.
* Ajouter ou ajuster les tests quand c’est pertinent.
* Toute nouvelle fonctionnalité ou modification de comportement doit inclure les tests pertinents.
* Dans `AssistantCore.Service.Tests`, utiliser `[Theory, AutoDomainData]` pour tout nouveau test, meme lorsqu'une seule execution est necessaire.
* Utiliser `[InlineAutoDomainData]` lorsque le scenario exige une ou plusieurs valeurs explicites.
* Nommer les tests avec la convention Gherkin `Given_<contexte>_When_<methode_testee>_Then_<resultat>`.
* La partie `When` du nom d'un test doit toujours contenir le nom exact de la méthode testée.
* Structurer le corps des tests avec les sections `// Given`, `// When` et `// Then`.
* Ne jamais lancer les tests, qu'ils soient ciblés ou complets.
* Exécuter uniquement le build avec `dotnet build Solution.sln` lorsqu'une modification importante implique plusieurs fichiers.
* Pour une correction ponctuelle ou une modification ciblée, ne lancer ni test ni build.
* Ne jamais présenter les tests comme validés puisqu'ils ne sont pas exécutés par Codex.
* Gérer les erreurs proprement sans masquer les exceptions importantes.
* Utiliser async/await correctement pour les appels I/O.
* Ne pas introduire de dépendances inutiles.

## Documentation et issues

* Ne créer un nouveau document que lorsqu’il est réellement nécessaire pour comprendre une fonctionnalité, un flow, une décision ou une contrainte importante. Pour une modification locale ou évidente, ne pas ajouter de documentation uniquement par principe. Mettre à jour un document existant seulement si son contenu devient incomplet ou incorrect.
* Rédiger les documents et tickets dans un langage simple, clair et concret.
* Choisir chaque mot pour que le texte paraisse naturel à une personne qui découvre le sujet. Éviter les tournures artificielles, les traductions littérales et le jargon inutile.
* Préférer les expressions courantes comme `membre créé automatiquement` à des formulations techniques comme `membre provisionné`. Lorsqu'un terme technique est indispensable, l'expliquer simplement à sa première utilisation.
* Relire chaque ticket du point de vue d'un humain qui doit comprendre le besoin sans connaître le vocabulaire interne du projet.
* Éviter les formulations vagues qui obligent le lecteur à deviner le travail attendu.
* Tous les tickets doivent commencer par expliquer le but réel de la fonctionnalité et la capacité finale qu'elle débloque.
* Chaque ticket doit contenir les sections `But`, `Pourquoi cette fonctionnalité est nécessaire`, `Flow détaillé`, `Résultat concret`, `Limites`, `Réalisation pas à pas`, `Critères d'acceptation` et `Documentation de référence`.
* Chaque section possède une responsabilité précise :
  * `But` explique la capacité finale recherchée;
  * `Pourquoi cette fonctionnalité est nécessaire` explique ce qui est impossible, bloqué ou risqué sans elle;
  * `Flow détaillé` explique le comportement complet, étape par étape;
  * `Résultat concret` résume ce qui fonctionne à la fin;
  * `Limites` indique clairement ce qui ne fonctionne pas encore;
  * `Réalisation pas à pas` contient uniquement les actions de développement, sans répéter le flow;
  * `Critères d'acceptation` contient uniquement des résultats vérifiables, sans recopier les étapes de réalisation.
* Le `Flow détaillé` ne doit pas être limité à un diagramme ou à une liste de composants. Après le résumé visuel, expliquer chaque étape en texte simple, précis et concret.
* Pour chaque étape importante du flow, préciser lorsque pertinent :
  * qui déclenche l'action;
  * la requête envoyée et la réponse attendue;
  * les données reçues;
  * les validations d'identité, d'autorisation et de sécurité;
  * le chemin `Controller -> Dispatcher -> Handler -> Service`;
  * la responsabilité exacte de chaque couche;
  * les lectures et écritures en base;
  * les appels aux services externes;
  * les changements d'état;
  * ce que fait ensuite le frontend, le Worker ou un autre consommateur.
* Une information doit être expliquée complètement à l'endroit le plus pertinent, puis seulement référencée ailleurs si nécessaire.
* Ajouter un exemple de requête, réponse, état ou appel frontend seulement lorsqu'il aide réellement à comprendre le comportement.
* Terminer le flow en décrivant explicitement l'état du système avant et après l'opération.
* Le lecteur ne doit pas avoir à déduire pourquoi une étape existe ni ce qu'elle rend possible pour l'étape suivante.
* Ne pas ajouter une section générique ou répétitive uniquement pour reformuler des informations déjà expliquées ailleurs dans le document.
* Donner assez de détails techniques pour guider le développeur, sans décrire toute l’implémentation ligne par ligne.
* Préciser les contraintes importantes d’architecture, de sécurité et de comportement.
* Ne pas imposer les noms de toutes les classes, méthodes ou colonnes lorsque ce choix peut raisonnablement être laissé au développeur.
* Un ticket doit pouvoir être commencé sans clarification majeure, tout en laissant au développeur les décisions techniques locales.
* Chaque document de fonctionnalité doit contenir une table des matières cliquable.
* Ajouter des ancres stables aux sections référencées par des tickets afin que les liens restent valides si un titre change.
* Chaque ticket doit contenir une section `Documentation de référence` avec un lien vers la section exacte du document concerné.
* Pour les issues GitHub, utiliser une URL vers le dépôt et la branche par défaut avec l’ancre de section, pas seulement un chemin de fichier.
* Avant de creer ou mettre a jour un ticket GitHub, ajouter ou mettre a jour la documentation de reference correspondante.
* Presenter un résumé de la documentation a l'utilisateur et attendre son approbation avant de creer ou modifier le ticket GitHub. Presenter le diff seulement si je le demande explicitement.
* Ne pas creer un ticket dont les decisions fonctionnelles importantes ne sont pas encore documentees.


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
