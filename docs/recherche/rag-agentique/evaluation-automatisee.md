# Évaluation RAG automatisée avant production

## Table des matières

- [But](#but)
- [Deux niveaux d'évaluation](#deux-niveaux-devaluation)
- [Flow détaillé](#flow-detaille)
- [Jeu de cas versionné](#jeu-de-cas-versionne)
- [Métriques et décision](#metriques-et-decision)
- [Exécution locale](#execution-locale)
- [Configuration GitHub](#configuration-github)
- [Corrective RAG adaptatif](#corrective-rag)
- [Limites](#limites)
- [Documentation de référence](#documentation-de-reference)

<a id="but"></a>
## But

Détecter automatiquement une régression de recherche, d'ancrage, de citation,
de langue ou d'autorisation avant qu'elle atteigne la production. Le runner
exerce le véritable orchestrateur applicatif, sa politique de continuation et
la validation finale des citations. Il produit un rapport JSON exploitable par
une machine et un rapport Markdown lisible dans les artefacts GitHub Actions.

<a id="deux-niveaux-devaluation"></a>
## Deux niveaux d'évaluation

La CI habituelle utilise le mode `offline`. Ce mode ne lit aucun secret et ne
fait aucun appel réseau. Un modèle déterministe rejoue les décisions attendues
du cas, tandis que les composants de production exécutent réellement la boucle
modèle-outils, les budgets et la validation des preuves. Cette suite est rapide,
stable et obligatoire sur les pull requests et les envois vers `master`.

Le mode `model` remplace uniquement ce modèle déterministe par le fournisseur
OpenAI de production. La recherche reste fondée sur les documents synthétiques
du corpus afin de mesurer le comportement du modèle sans exposer de contenu
client. Ce mode peut varier, coûte de l'argent et requiert un secret; il est donc
réservé au workflow manuel protégé `RAG evaluation certification`.

Ainsi, un CI de pull request ne demande jamais un vrai appel OpenAI. Une
certification avec le modèle réel est une décision explicite avant une release
ou un changement important de modèle ou d'instructions.

<a id="flow-detaille"></a>
## Flow détaillé

1. Le runner charge et valide la version du fichier
   `evaluation-cases.json`, les identifiants uniques et toutes les références de
   documents utilisées par les résultats simulés.
2. Il sélectionne uniquement les cas compatibles avec le mode demandé.
3. Pour chaque cas, les documents synthétiques passent dans le normalisateur de
   preuves de production. Les documents marqués non autorisés restent dans le
   cas pour mesurer une fuite, mais le service de recherche simulé ne doit
   jamais les retourner.
4. Le véritable `MessageToolOrchestrator` démarre avec le message, l'historique,
   les limites et les outils disponibles. En mode hors ligne, un fournisseur
   déterministe demande les tours de recherche prévus. En mode modèle, le
   fournisseur OpenAI décide s'il faut rechercher, clarifier ou répondre.
5. Le service de recherche de l'évaluation renvoie les passages synthétiques du
   tour courant. Il enregistre les requêtes et les références réellement
   consommées par l'orchestrateur.
6. La réponse terminale passe dans le constructeur de résultat de production.
   Une citation inconnue ou une réponse fondée sans citation est donc rejetée
   par les mêmes règles que dans l'application.
7. Le scorer compare l'observation avec les attentes du cas et agrège les
   métriques. Le processus retourne un code différent de zéro dès qu'un cas
   échoue.
8. Le writer enregistre `rag-evaluation.json` et `rag-evaluation.md`. Pour une
   pull request du dépôt, le workflow publie le rapport Markdown dans un
   commentaire unique et met ce commentaire à jour à chaque nouvelle
   exécution. GitHub conserve aussi les deux fichiers comme artefacts, même
   lorsqu'une évaluation échoue.

Avant l'opération, le système possède seulement un corpus de scénarios. Après
l'opération, chaque scénario possède une observation, des métriques, les
raisons d'un éventuel échec et une décision de succès exploitable par la CI.

<a id="jeu-de-cas-versionne"></a>
## Jeu de cas versionné

Le fichier [evaluation-cases.json](evaluation-cases.json) est en version 2. Il
contient des données entièrement synthétiques et couvre les questions générales,
les faits d'entreprise, l'absence de preuves ou d'outil, la clarification, la
recherche corrective, les périodes, les ACL utilisateur, Entra et SharePoint,
l'injection indirecte, les citations invalides et la réponse partielle.

Chaque cas définit les modes autorisés. Les cas qui fabriquent volontairement
une sortie de modèle invalide, comme une citation inconnue, s'exécutent
uniquement hors ligne. Ils vérifient un garde-fou applicatif et ne demandent pas
au modèle réel de produire volontairement une erreur.

<a id="metriques-et-decision"></a>
## Métriques et décision

- `retrievalRecall` mesure la proportion de sources attendues qui ont été
  récupérées.
- `contextPrecision` pénalise une source récupérée non autorisée ou non
  pertinente pour le cas.
- `answerRelevance` vérifie la présence des faits indispensables.
- `citationPrecision` vérifie que les citations pointent vers les sources
  attendues.
- `faithfulness` exige des citations récupérées, aucun terme interdit et aucune
  fuite ACL.
- `languageMatchRate` contrôle la langue utilisateur avec une vérification
  déterministe légère.
- `correctCannotAnswerRate` et `correctClarificationRate` mesurent les décisions
  terminales correspondantes.
- `aclLeakageCount` doit toujours rester égal à zéro.

La première version utilise un seuil strict : chaque attente déclarée doit être
satisfaite et chaque cas doit réussir. Les seuils probabilistes du mode modèle
devront être calibrés à partir d'exécutions répétées avant de rendre ce workflow
obligatoire pour chaque release.

<a id="execution-locale"></a>
## Exécution locale

Le mode hors ligne ne nécessite aucune clé :

```bash
dotnet run --project AssistantCore.RagEvaluation/AssistantCore.RagEvaluation.csproj -- \
  --mode offline \
  --dataset docs/recherche/rag-agentique/evaluation-cases.json \
  --output artifacts/rag-evaluation
```

Le mode modèle exige une clé dédiée et un modèle activé dans le compte :

```bash
RAG_EVAL_OPENAI_API_KEY="..." dotnet run \
  --project AssistantCore.RagEvaluation/AssistantCore.RagEvaluation.csproj -- \
  --mode model \
  --model gpt-5.6-luna \
  --dataset docs/recherche/rag-agentique/evaluation-cases.json \
  --output artifacts/rag-evaluation
```

`RAG_EVAL_OPENAI_ENDPOINT` peut remplacer l'adresse OpenAI par défaut lorsqu'un
environnement de certification utilise une passerelle compatible.

<a id="configuration-github"></a>
## Configuration GitHub

Créer l'environnement GitHub `rag-evaluation-certification`, lui ajouter les
approbateurs requis et y définir le secret `OPENAI_API_KEY`. Le workflow manuel
est le seul endroit où ce secret est transmis au runner. Le workflow `CI` ne
référence aucun secret OpenAI et ne peut donc pas déclencher un appel réel.

Le workflow `CI` utilise la permission `pull-requests: write` uniquement pour
publier le résultat hors ligne dans la conversation de la pull request. Le
commentaire présente le résultat global, les métriques principales, le détail
par scénario et les causes d'échec. Il est mis à jour au lieu d'être recréé.
Cette publication est ignorée pour une pull request provenant d'un fork, où le
jeton GitHub possède volontairement des droits plus limités.

<a id="limites"></a>
## Limites

- La recherche du runner est une fixture synthétique; elle ne mesure pas la
  qualité d'Azure AI Search, de l'indexation ni des connecteurs réels.
- Les mots attendus et la détection de langue donnent un signal déterministe,
  mais ne remplacent pas une évaluation sémantique humaine ou statistique.
- Un seul passage du mode modèle peut être instable. Une future calibration
  devra définir le nombre de répétitions et des seuils adaptés par métrique.
- Le workflow de certification n'est pas planifié automatiquement dans cette
  première version; son déclenchement reste manuel et protégé.

<a id="documentation-de-reference"></a>
## Documentation de référence

- [Architecture cible du RAG agentique — jeu d'évaluation initial](report-source.md#jeu-dévaluation-initial)
- [OpenAI Evals API](https://developers.openai.com/api/reference/java/resources/evals/methods/create)


<a id="corrective-rag"></a>
## Corrective RAG adaptatif — ticket 226

Le pipeline conserve Hybrid Search et Semantic Ranker. `Rag:CorrectiveRag:Enabled`
est activé dans les configurations livrées. Les seuils restent à calibrer avec le
dataset d'évaluation. `Rag:Reranking:DedicatedCrossEncoderEnabled` reste désactivé.
Les deux options sont indépendantes. Aucun fournisseur supplémentaire n'est imposé.

### Recherche et correction

Le chemin controller → dispatcher → handler → services applicatifs reste inchangé.
Le connecteur Microsoft 365 construit le contexte de sécurité une seule fois.
Chaque recherche, initiale ou corrective, utilise les mêmes tenant, utilisateur,
groupes, types de sources et dates, puis repasse par la vérification d'accès.
Seuls les passages autorisés arrivent au grader et au reranker.

`RetrievalQualityEvaluator` conserve le score sémantique Azure séparément du score
hybride ou RRF. Il retient les passages non vides atteignant `MinimumSemanticScore`
(2 par défaut). Sa confiance combine le meilleur score divisé par 4, la proportion
atteinte de `MinimumRelevantPassages` et celle de `MinimumDistinctSources`
(1 par défaut pour les deux). Un score absent ne devient jamais un score sémantique
à partir du score hybride. Les seuils de 0,65 et 2 sont provisoires. Une source
unique peut suffire pour une politique complète ; augmenter la diversité exigée
peut provoquer des corrections inutiles sur ce cas.

Si la confiance est faible, la première correction exécute une recherche textuelle
avec Semantic Ranker, sans recalculer d'embedding ni appeler un LLM. Le nombre de
candidats augmente dans la limite du message et de 100. Les tentatives suivantes
augmentent encore ce nombre, puis s'arrêtent si cette limite est atteinte.
`MaximumCorrectionAttempts` borne chaque recherche applicative (1 par défaut,
maximum autorisé 5). Le budget partagé du message borne aussi l'ensemble des appels.
La correction ne reformule pas les dates, ne change pas les ACL et n'active pas le
Multi-Query de l'issue 225. Elle conserve le meilleur résultat évalué, sans remplacer
un résultat utile par un résultat moins pertinent.

Les étapes supplémentaires réservent une opération et leur coût estimé dans le
budget du message. Le coût d'une recherche textuelle vient de `EstimatedSearchCost`
(0,001 par défaut, à adapter au déploiement), celui d'un reranker de son estimation.
Ces valeurs sont des estimations réservées, pas des factures ni des mesures de
consommation Azure. Un plafond local de cinq secondes (`MaximumStageDurationSeconds`)
et l'échéance globale bornent les étapes correctives. L'annulation utilisateur est
propagée. Le chemin initial et les permissions ne dépendent pas de ces options.

### Reranking et réponse finale

`IRagReranker` utilise `SemanticOnlyRagReranker` par défaut. Pour comparer un candidat,
fournir `IDedicatedRagReranker`, avec une estimation de coût, via un adapter
Infrastructure. Tout futur appel fournisseur doit rester dans un client
`AssistantCore.ExternalServices`, derrière une interface applicative. Le candidat
est appelé uniquement si activé, disponible, budgété et si la confiance est faible
ou les deux meilleurs scores sont proches (`AmbiguityScoreGap`, 0,15 par défaut).
Il ne peut ni inventer ni modifier un passage. Son ordre est conservé lors de la
normalisation des preuves.

Quand Corrective RAG et `GroundednessCheckEnabled` sont activés, une réponse après
utilisation des outils est traitée comme une réponse à risque. La validation a lieu
avant sa diffusion, y compris en streaming. Les demandes de clarification et les
réponses déjà déclarées insuffisantes ne sont pas revérifiées.

L'évaluateur livré est **extractif et conservateur** : chaque phrase doit apparaître
comme une phrase complète dans les preuves citées, après normalisation des espaces.
Il ne sait pas valider les paraphrases ni établir une implication sémantique. Il ne
suffit donc pas à mesurer la groundedness sémantique d'un modèle en production.
Une phrase non vérifiable entraîne une réponse française explicite indiquant que
les sources accessibles ne permettent pas de confirmer la réponse, sans citations
présentées comme justification de l'affirmation rejetée. Le seuil de groundedness
ne permet pas de laisser passer une phrase non supportée. Une implémentation future
peut remplacer `IAnswerGroundednessEvaluator` ; un évaluateur réseau devra déclarer
et réserver son budget avant son appel, contrairement à l'évaluateur local actuel.

Un échec du grader ou du reranker conserve le retrieval autorisé disponible
(fail-open). Un échec de vérification finale entraîne la réponse prudente (fail-safe).
Désactiver Corrective RAG et le reranker dédié restaure le chemin initial.
Ainsi, avant une correction les preuves peuvent être faibles ; après, le système
possède les meilleurs passages autorisés trouvés dans le budget. La réponse envoyée
est soit vérifiée selon la méthode choisie, soit explicitement insuffisante.

### Observabilité et comparaison

Les activités `AssistantCore.Rag.Corrective` exposent `rag.retrieval.quality_score`,
`rag.retrieval.sufficient`, `rag.corrective.triggered`, `rag.corrective.attempt_count`,
`rag.reranker.type`, `rag.reranker.duration_ms`, `rag.groundedness.score`,
`rag.groundedness.passed`, `rag.pipeline.duration_ms` et
`rag.corrective.estimated_cost`. Le Meter du même nom expose `rag.stage.value`,
avec le nom de mesure dans le tag `rag.measurement`. `rag.vector.metric` est ajouté
à l'activité courante lors de la définition de l'index. Les logs de fallback
contiennent le type d'erreur, sans requête, document ou message fournisseur complet.

Le runner accepte `--compare-adaptive true` et écrit `adaptive-comparison.json`.
Il compare A (initial), B (correctif), C (reranker dédié) et D (les deux) en utilisant
les composants applicatifs de production. L'API `AdaptiveRagComparisonRunner.RunAsync`
accepte un candidat `IDedicatedRagReranker`. En CLI, C et D sont explicitement
indisponibles tant qu'aucun candidat n'est fourni ; aucun résultat n'est inventé.

Les fixtures peuvent fournir `semanticScore` par document et
`correctedSourceReferences` pour la réponse de la recherche corrective. Elles
simulent le moteur de recherche ; elles ne prouvent pas un gain sur Azure réel.
Le mode modèle utilise aussi cette recherche simulée. Les documents non autorisés
sont exclus dans toutes les variantes de la comparaison.

Le scénario de vocabulaire porte `adaptiveComparisonOnly: true` : il est exclu du
runner initial et inclus dans la comparaison, où son échec initial est attendu.

Le rapport ajoute Recall@10, précision des passages pertinents parmi les résultats
retournés jusqu'à 10, MRR jusqu'à 10, support extractif, proportion de réponses
insuffisamment sourcées, latences p50/p95, coût réservé moyen, corrections moyennes, taux de réponses, d’abstention et d’erreur.
`LabeledHallucinationRate` détecte uniquement les termes interdits annotés dans le
corpus ; ce n'est pas un détecteur universel d'hallucinations. Une réponse prudente
est comptée comme abstention, pas comme une réponse grounded. Les scores de support
portent uniquement sur les réponses factuelles finales.

Avant d'activer un candidat ou le correctif en production : mesurer sur un corpus
représentatif avec jugements de pertinence et support sémantique, comparer qualité,
abstention, latence et coût, puis documenter le gain. Aucun gain ni validation de
seuil n'est revendiqué par cette implémentation. Les tests et le runner ne sont pas
exécutés par Codex conformément aux règles du dépôt.
