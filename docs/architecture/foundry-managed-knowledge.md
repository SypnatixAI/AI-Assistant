# Work IQ et Foundry IQ dans le Foundry Agent

## Objectif

Cette branche fait évoluer le Foundry Agent pour utiliser des sources de connaissance managées sans supprimer encore le pipeline Microsoft 365 existant.

Le runtime attend désormais trois capacités sur l'agent publié :

- `EnterpriseSearch`, conservé temporairement pendant la migration ;
- Work IQ, pour les données Microsoft 365 de l'utilisateur ;
- Foundry IQ, exposé comme serveur MCP de Knowledge Base.

Les labels MCP attendus par défaut sont `work-iq` et `foundry-iq`. Ils sont configurables dans `FoundryAgent`.

## Work IQ

Work IQ doit être configuré avec une identité utilisateur déléguée. Le flow cible est :

```text
Utilisateur Entra
  -> API SynaptixAI
  -> OBO dans le tenant d'origine de l'utilisateur
  -> Foundry Agent
  -> Work IQ
  -> Microsoft 365
```

### Configuration Entra

1. L'application SynaptixAI reste multi-tenant (`organizations` / `AzureADMultipleOrgs`).
2. Ajouter la permission déléguée Work IQ `WorkIQAgent.Ask`.
3. Accorder le consentement administrateur lorsque requis.
4. L'échange OBO doit être fait contre le tenant d'origine de l'utilisateur, pas contre un tenant SynaptixAI fixe.
5. Ne pas utiliser un jeton app-only pour Work IQ.

Le client Microsoft expose maintenant `AcquireOnBehalfOfTokenAsync` pour ce flow. Le passage du jeton utilisateur jusqu'au runtime sera raccordé dans l'étape suivante de la migration, lorsque Work IQ remplacera effectivement le chemin `EnterpriseSearch`.

### Configuration Foundry

Dans le projet Foundry utilisé par l'environnement :

1. Créer la connexion Work IQ conformément à la documentation Microsoft.
2. Ajouter Work IQ au toolbox de l'agent, ou l'ajouter directement comme `work_iq_preview`.
3. Si Work IQ est exposé comme MCP, utiliser le label `work-iq` ou aligner `FoundryAgent:WorkIqServerLabel` sur le label réellement publié.
4. Autoriser les capacités Microsoft 365 nécessaires : SharePoint, OneDrive, Outlook Mail, Outlook Calendar et Teams.

## Foundry IQ

La Knowledge Base Foundry IQ est ajoutée à l'agent via son endpoint MCP.

1. Créer ou réutiliser la Knowledge Base de l'environnement.
2. Exposer son endpoint MCP `knowledge_base_retrieve` au Foundry Agent.
3. Utiliser le label MCP `foundry-iq` ou aligner `FoundryAgent:FoundryIqServerLabel` sur le label réellement publié.
4. Conserver les futures sources non-Microsoft 365 dans cette Knowledge Base : Azure SQL, Blob, index Azure AI Search spécifiques et serveurs MCP métier.

## Publication de l'agent

Après configuration des deux outils :

1. Publier une nouvelle version de l'agent Foundry.
2. Mettre `FoundryAgent:AgentVersion` à jour avec cette version.
3. Au premier appel, `FoundryAgentExternalClient` lit la définition publiée et refuse de démarrer si un outil requis manque ou si `web_search` est activé.

Pendant cette première étape de migration, `RequireEnterpriseSearch` reste à `true`. Il passera à `false` lorsque le runtime aura été basculé sur Work IQ / Foundry IQ et que l'ancien retrieval pourra être supprimé.

## Références Microsoft

- Work IQ dans Foundry Agent Service : https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/work-iq
- Work IQ API et applications multi-tenant : https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq-api-overview
- Work IQ MCP avec Foundry : https://learn.microsoft.com/en-us/microsoft-365/copilot/extensibility/work-iq/mcp/quickstart/foundry
- Foundry IQ et Knowledge Base MCP : https://learn.microsoft.com/en-us/azure/foundry/agents/quickstarts/quickstart-foundry-iq-hosted-agent
