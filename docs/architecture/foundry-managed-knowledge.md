# Work IQ et Foundry IQ dans le Foundry Agent

## Objectif

Cette branche fait évoluer le runtime pour que les connaissances générales ne passent plus par le moteur `EnterpriseSearch` local. Le Foundry Agent doit utiliser :

- Work IQ pour les données Microsoft 365 de l'utilisateur ;
- Foundry IQ pour les Knowledge Bases et les futures sources non-Microsoft 365 ;
- les tools locaux uniquement lorsqu'une capacité déterministe ou métier reste nécessaire, comme `AnalyzeSpreadsheet`.

Les labels MCP attendus par défaut sont `work-iq` et `foundry-iq`. Ils sont configurables dans `FoundryAgent`.

## Flow runtime

Le flow d'un message devient :

```text
Utilisateur Entra
  -> API SynaptixAI
  -> OBO vers https://ai.azure.com/.default dans le tenant d'origine
  -> Foundry Agent exécuté avec l'identité déléguée
      -> Work IQ pour Microsoft 365
      -> Foundry IQ pour les Knowledge Bases
      -> AnalyzeSpreadsheet pour les calculs Excel déterministes
```

Le bearer token reçu par l'API n'est jamais envoyé directement à Work IQ. L'API l'utilise comme assertion OAuth OBO pour obtenir un jeton Foundry délégué. Le `AIProjectClient` utilisé pour exécuter l'agent est ensuite créé avec ce jeton utilisateur afin que Foundry conserve le contexte délégué requis par la connexion Work IQ.

La lecture administrative de la définition publiée de l'agent continue d'utiliser l'identité technique Azure (`DefaultAzureCredential`). Cette identité sert uniquement à vérifier la configuration de l'agent ; elle ne sert pas à exécuter une requête utilisateur Work IQ.

## Work IQ

Work IQ utilise une identité utilisateur déléguée. L'authentification app-only n'est pas utilisée pour les recherches Microsoft 365.

### Configuration Entra à réaliser dans l'environnement

1. L'application SynaptixAI reste multi-tenant (`organizations` / `AzureADMultipleOrgs`).
2. Ajouter la permission déléguée Work IQ `WorkIQAgent.Ask`.
3. Accorder le consentement administrateur dans chaque organisation cliente qui active Work IQ.
4. L'utilisateur doit se connecter via l'autorité de son tenant d'origine.
5. Configurer les droits Foundry requis pour les identités impliquées dans le flow OAuth.

Le backend utilise `AcquireOnBehalfOfTokenAsync` pour échanger le token API utilisateur contre un token délégué destiné à `https://ai.azure.com/.default`.

### Configuration Foundry à réaliser dans l'environnement

1. Créer la connexion Work IQ conformément à la documentation Microsoft.
2. Ajouter Work IQ au toolbox de l'agent ou l'ajouter comme `work_iq_preview`.
3. Pour un toolbox MCP, utiliser une connexion de type `user-entra-token` afin de préserver l'identité de l'appelant.
4. Aligner `FoundryAgent:WorkIqServerLabel` sur le `server_label` réellement publié.
5. Autoriser les capacités Microsoft 365 voulues : SharePoint, OneDrive, Outlook Mail, Outlook Calendar et Teams.
6. Publier une nouvelle version de l'agent.

## Foundry IQ

La Knowledge Base Foundry IQ est attachée à l'agent via MCP.

1. Créer ou réutiliser la Knowledge Base de l'environnement.
2. Exposer son endpoint MCP `knowledge_base_retrieve` au Foundry Agent.
3. Utiliser l'identité managée de l'agent pour l'accès à la Knowledge Base lorsque cette configuration est disponible.
4. Aligner `FoundryAgent:FoundryIqServerLabel` sur le label réellement publié.
5. Conserver les futures sources non-Microsoft 365 dans cette Knowledge Base : Azure SQL, Blob, index Azure AI Search spécifiques et serveurs MCP métier.

## Retrieval Microsoft 365 legacy

Le retrieval Microsoft 365 général a été retiré du cœur applicatif. Le runtime ne possède plus de tool local `search_microsoft_365` et les composants qui servaient uniquement à ce chemin ont été supprimés : connector, handler, adapter d'agentic retrieval, modèles de requête/résultat et filtres Azure AI Search.

Le chemin documentaire cible est donc directement :

```text
Foundry Agent -> Work IQ -> Microsoft 365
```

Les composants liés à l'ingestion, l'indexation et aux ACL de l'ancien pipeline ne sont pas tous supprimés à cette étape, car `AnalyzeSpreadsheet` dépend encore temporairement des métadonnées indexées et de la vérification d'accès. Ils seront retirés après découplage de l'analyse Excel.

## AnalyzeSpreadsheet

`AnalyzeSpreadsheet` reste un tool local. Il sert uniquement aux opérations qui exigent un calcul déterministe et exhaustif sur un classeur XLSX/XLSM : sommes, moyennes, comptages, filtres de lignes et autres calculs tabulaires.

Si l'utilisateur ne connaît pas le nom exact du fichier, l'agent doit utiliser Work IQ pour identifier le classeur puis appeler `AnalyzeSpreadsheet`.

## Validation au démarrage du runtime

Au premier appel, `FoundryAgentExternalClient` lit la définition publiée de l'agent et refuse l'exécution si :

- Work IQ est requis mais absent ;
- Foundry IQ est requis mais absent ;
- `web_search` est activé alors qu'il n'est pas autorisé par l'architecture.

La version `AgentVersion` doit être mise à jour après la publication de l'agent configuré dans Foundry.

## Références Microsoft

- Work IQ dans Foundry Agent Service : https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/work-iq
- Work IQ API et applications multi-tenant : https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq-api-overview
- Work IQ MCP avec Foundry : https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/mcp/quickstart/foundry
- Foundry IQ et Knowledge Base MCP : https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-foundry-iq-hosted-agent
