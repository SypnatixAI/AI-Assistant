# Architecture cible du RAG agentique d’onPremia

## Public et date

Ce rapport s’adresse aux développeurs et responsables techniques d’onPremia.
Il a été préparé le 30 août 2026.

## Portée

Le rapport couvre la recherche dans les contenus d’entreprise déjà pris en
charge, l’orchestration du modèle, la clarification avec l’utilisateur, la
validation des preuves, la sécurité et l’évaluation. L’ajout de nouveaux
formats documentaires est explicitement hors périmètre et doit être traité
dans un ticket séparé.

## Réponse exécutive

Le système actuel possède une base RAG saine : outils en lecture seule,
recherche textuelle et vectorielle, ACL, normalisation des preuves, citations
et boucle modèle-outils. Il lui manque toutefois les contrôles qui transforment
ces briques en système agentique fiable : planification de recherche,
clarification, requêtes complémentaires, reranking, mesure de couverture,
validation de l’ancrage et évaluations représentatives.

L’architecture cible conserve Azure AI Search et la Clean Architecture du
projet. Elle ajoute une boucle explicite `planifier -> rechercher -> évaluer ->
corriger -> répondre -> vérifier`. Le modèle conserve la liberté de choisir une
stratégie, tandis que le backend contrôle les permissions, les budgets, les
états autorisés et les conditions minimales permettant de conclure que les
sources sont insuffisantes.

## Conclusions de la recherche

### Recherche hybride et reranking

Azure AI Search exécute la recherche textuelle et vectorielle en parallèle,
puis fusionne les classements avec RRF. Microsoft recommande de mesurer un
pipeline hybride équilibré et d’activer le classement sémantique lorsqu’il
améliore effectivement la pertinence. Le classement sémantique travaille sur
un ensemble de candidats plus large que les résultats finalement retournés.

Sources :

- [Hybrid search overview — Microsoft Learn](https://learn.microsoft.com/en-us/azure/search/hybrid-search-overview)
- [Create a hybrid query — Microsoft Learn](https://learn.microsoft.com/en-us/azure/search/hybrid-search-how-to-query)
- [Semantic ranking — Microsoft Learn](https://learn.microsoft.com/en-us/azure/search/semantic-search-overview)

### Planification agentique

Les systèmes récents décomposent une demande complexe en sous-requêtes,
exécutent les recherches en parallèle, rerankent les résultats et conservent
les références. Cette approche correspond à la boucle déjà amorcée dans
onPremia, mais exige des états et critères d’arrêt plus riches.

Sources :

- [Azure AI Search agentic retrieval — Microsoft Learn](https://learn.microsoft.com/en-us/azure/search/search-what-is-azure-search#what-is-agentic-retrieval)
- [OpenAI model guidance](https://developers.openai.com/api/docs/guides/latest-model?model=gpt-5.5)
- [Responses API](https://developers.openai.com/api/reference/cli/resources/responses/methods/create)

### Contextualisation des passages

Anthropic observe qu’un passage isolé perd souvent le sujet, la période ou
l’entité définis ailleurs dans le document. Son approche ajoute un court
contexte propre au passage avant l’indexation textuelle et vectorielle, puis
utilise un reranker. Cette technique correspond directement aux passages
d’onPremia qui sont actuellement aplatis et indexés sans contexte de section.

Source : [Contextual Retrieval — Anthropic](https://www.anthropic.com/engineering/contextual-retrieval)

### Évaluation corrective et auto-vérification

CRAG évalue la qualité des résultats avant la génération et déclenche une
action corrective lorsque la recherche est faible. Self-RAG sépare la décision
de récupérer, la pertinence des passages et le soutien de la réponse. onPremia
ne doit pas reproduire leurs mécanismes d’entraînement, mais peut adopter ces
états sous forme de décisions structurées et de politiques applicatives.

Sources :

- [Corrective Retrieval Augmented Generation](https://arxiv.org/abs/2401.15884)
- [Self-RAG, ICLR 2024](https://openreview.net/pdf?id=hSyW5go0v8)

### Évaluation continue

Une évaluation RAG doit séparer au minimum la pertinence du contexte, la
fidélité de la réponse et la pertinence de la réponse. Les juges automatiques
accélèrent les comparaisons, mais un jeu humain réduit et représentatif reste
nécessaire pour calibrer et contrôler ces juges.

Sources :

- [ARES, NAACL 2024](https://aclanthology.org/2024.naacl-long.20.pdf)
- [RAGAS, EACL 2024](https://aclanthology.org/2024.eacl-demo.16.pdf)
- [OpenAI Evals](https://developers.openai.com/api/reference/java/resources/evals/methods/create)

### Sécurité

Le contenu récupéré reste une donnée non fiable. Le RAG ne supprime pas le
risque d’injection indirecte par un document. Les permissions et outils doivent
donc rester contrôlés par le backend, indépendamment de toute instruction
trouvée dans une preuve.

Source : [OWASP LLM01:2025 Prompt Injection](https://genai.owasp.org/llmrisk/llm01-prompt-injection/)

## Matrice des lacunes

| Capacité | État actuel | Cible | Priorité |
|---|---|---|---|
| Langue | fallback anglais fixe | message produit dans la langue du tour courant | critique |
| Clarification | aucun état dédié | question précise et persistée comme réponse normale | critique |
| Arrêt | décision du modèle acceptée immédiatement | épuisement vérifié et dernier tour de synthèse | critique |
| Contexte conversationnel | évolution locale en cours | historique et mémoire structurés, sans doublon | critique |
| ACL SharePoint | identifiants indexés mais appartenance non résolue à la recherche | résolution fermée et mise en cache | critique |
| Filtres | dates et types validés mais ignorés | filtres appliqués au moteur | élevée |
| Classement | RRF seulement | candidats larges, classement sémantique mesuré, top final réduit | élevée |
| Requête | une formulation du modèle | décomposition et reformulations bornées | élevée |
| Passages | texte aplati | contexte de document et de section conservé | élevée |
| Couverture | aucune mesure | faits demandés et manquants structurés | élevée |
| Grounding | IDs inconnus retirés | affirmations importantes vérifiées contre les preuves | élevée |
| Observabilité | usage et avertissements | trace RAG sans contenu sensible | élevée |
| Évaluations | tests unitaires | corpus multilingue, ACL, retrieval et grounding | critique |
| GraphRAG | absent | option évaluée pour questions globales et relationnelles | future |

## Architecture recommandée

1. Résoudre la langue, le contexte conversationnel et les références
   implicites sans fabriquer de fait.
2. Répondre directement lorsque la demande ne dépend pas des données privées.
3. Demander une clarification lorsque seule une information de l’utilisateur
   permet de choisir une recherche utile.
4. Pour les données d’entreprise, produire un plan de recherche borné avec
   des sous-requêtes indépendantes.
5. Exécuter une recherche hybride sécurisée avec filtres applicatifs, candidats
   suffisants et reranking.
6. Évaluer la pertinence, la couverture et les contradictions des preuves.
7. Reformuler ou utiliser une autre source uniquement si un manque précis peut
   encore être comblé.
8. Produire une réponse ou une limite utile dans la langue du message courant.
9. Vérifier les affirmations importantes et leurs citations avant de persister
   la réponse.

## Limites et décisions différées

- Aucun système probabiliste ne garantit une réponse exacte à 100 %. Les
  métriques, la révision humaine et les seuils métier restent nécessaires.
- GraphRAG augmente fortement le coût d’indexation. Il ne sera ajouté que si
  les évaluations démontrent un gain sur des questions globales ou
  relationnelles que la recherche hybride ne résout pas.
- Le changement de modèle ou d’effort de raisonnement sera piloté par les
  évaluations de qualité, coût et latence.
- Les nouveaux formats documentaires ne font pas partie de ce chantier.

## Jeu d'évaluation initial

Le fichier [evaluation-cases.json](evaluation-cases.json) définit les scénarios
minimaux à automatiser : français et anglais, suivi conversationnel,
clarification, recherche corrective, filtres, isolation ACL, injection indirecte,
citations et réponse partielle. Il s'agit d'un corpus de départ; ses seuils
doivent être calibrés sur des questions réelles anonymisées avant la production.
Le fonctionnement du runner, la séparation entre CI hors ligne et certification
avec OpenAI, ainsi que les métriques produites sont décrits dans
[Évaluation RAG automatisée avant production](evaluation-automatisee.md#but).

## Registre des sources

Les sources principales consultées sont les documentations techniques
officielles d’OpenAI, Anthropic et Microsoft, les publications originales
Self-RAG, CRAG, ARES et RAGAS, ainsi que la référence de sécurité OWASP. Les
pages ont été consultées le 30 août 2026. Les fonctionnalités Azure en préversion
ne sont pas retenues comme dépendances obligatoires de production.
