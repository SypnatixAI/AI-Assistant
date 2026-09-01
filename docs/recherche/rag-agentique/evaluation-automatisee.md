# Évaluation RAG automatisée avant production

## Table des matières

- [But](#but)
- [Deux niveaux d'évaluation](#deux-niveaux-devaluation)
- [Flow détaillé](#flow-detaille)
- [Jeu de cas versionné](#jeu-de-cas-versionne)
- [Métriques et décision](#metriques-et-decision)
- [Exécution locale](#execution-locale)
- [Configuration GitHub](#configuration-github)
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
