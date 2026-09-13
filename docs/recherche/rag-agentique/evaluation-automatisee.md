# Évaluation automatisée de l’agent et de la recherche

## Table des matières

- [But](#but)
- [Deux modes](#modes)
- [Flow détaillé](#flow)
- [Métriques](#metriques)
- [Exécution](#execution)
- [Limites](#limites)

<a id="but"></a>
## But

Détecter les régressions de réponse, de recherche, de citation, de langue et
d’isolation des accès avant une mise en production. Le runner utilise uniquement
des documents synthétiques et produit un rapport JSON ainsi qu’un rapport
Markdown.

<a id="modes"></a>
## Deux modes

Le mode `offline` rejoue les résultats attendus sans secret ni appel réseau. Il
valide le corpus, le calcul des métriques, les références et les scénarios de
rejet déterministes dans la CI.

Le mode `model` appelle la définition versionnée de l’agent Foundry utilisée par
l’application. L’outil `EnterpriseSearch` est simulé avec les documents du cas :
Foundry décide quand l’appeler et formule la réponse, sans accéder aux données
d’un client. Ce workflow manuel s’authentifie auprès d’Azure par OIDC.

<a id="flow"></a>
## Flow détaillé

1. Le runner charge `evaluation-cases.json` et valide les références du corpus.
2. Il sélectionne les scénarios compatibles avec le mode demandé.
3. En mode hors ligne, il transforme directement la fixture en observation.
4. En mode Foundry, il transmet l’historique, la dernière question et, lorsque
   permis, la définition de `EnterpriseSearch` à l’agent.
5. Chaque appel d’outil retourne seulement les documents synthétiques autorisés
   pour le tour prévu et enregistre la requête ainsi que les références livrées.
6. Le scorer compare la réponse et les sources aux attentes, puis le writer
   produit `rag-evaluation.json` et `rag-evaluation.md`.

Avant l’opération, le système possède un corpus versionné. Après l’opération,
chaque cas possède une observation, des métriques et les causes précises d’un
éventuel échec.

<a id="metriques"></a>
## Métriques

Le rapport calcule le rappel de recherche, la précision du contexte, la
pertinence de la réponse, la précision des citations, la fidélité, le respect de
la langue, les décisions de clarification ou d’absence d’information et le
nombre de fuites ACL. Une référence interdite récupérée ou citée fait échouer le
cas.

<a id="execution"></a>
## Exécution

Mode hors ligne :

```bash
dotnet run --project AssistantCore.RagEvaluation/AssistantCore.RagEvaluation.csproj -- \
  --mode offline \
  --dataset docs/recherche/rag-agentique/evaluation-cases.json \
  --output artifacts/rag-evaluation
```

Mode Foundry, après authentification Azure :

```bash
az login
dotnet run --project AssistantCore.RagEvaluation/AssistantCore.RagEvaluation.csproj -- \
  --mode model \
  --dataset docs/recherche/rag-agentique/evaluation-cases.json \
  --output artifacts/rag-evaluation
```

La configuration `FoundryAgent` suit les mêmes clés que le service. Les variables
d’environnement .NET peuvent les remplacer avec le séparateur `__`.

<a id="limites"></a>
## Limites

- Le mode modèle simule `EnterpriseSearch`; il ne mesure pas la qualité réelle
  de l’index Azure AI Search.
- Le résultat Foundry actuel ne distingue pas structurellement une clarification
  d’une impossibilité de répondre. Le runner emploie donc une détection textuelle
  limitée pour ces deux états.
- Les sources observées correspondent aux preuves remises à l’agent. Une future
  sortie structurée de citations permettra de mesurer uniquement celles que la
  réponse utilise réellement.
