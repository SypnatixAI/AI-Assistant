# Feuille de route Azure AI Search

## Table des matieres

- [Objectif](#objectif)
- [Architecture cible](#architecture-cible)
- [Ordre des tickets](#ordre-des-tickets)
- [Strategie de livraison](#strategie-de-livraison)
- [Decisions initiales](#decisions-a-prendre-avant-le-premier-ticket)
- [Definition de termine](#definition-globale-de-termine)
- [References Microsoft](#references-microsoft)

## Objectif

Cette feuille de route explique comment rendre les contenus Microsoft 365 recherchables par l'assistant avec Azure AI Search.

Elle est ecrite pour une equipe qui debute avec Azure. Les tickets doivent etre realises dans l'ordre. Il ne faut pas commencer l'integration de `/messages` tant qu'une recherche securisee ne fonctionne pas directement contre Azure AI Search.

## Architecture cible

```text
Microsoft Graph
  lit les documents et leurs permissions
        |
        v
Service d'ingestion
  extrait, decoupe et synchronise les contenus
        |
        v
Azure AI Search
  stocke les passages, vecteurs, sources et ACL
        ^
        |
Backend AssistantCore
  impose le tenant et les droits, puis recherche
        ^
        |
POST /api/messages
  utilise l'outil logique search_microsoft_365
```

## Ordre des tickets

1. [Creer l'infrastructure Azure AI Search](01-create-azure-search-infrastructure.md)
2. [Indexer SharePoint et OneDrive](02-index-sharepoint-onedrive.md)
3. [Appliquer les permissions aux recherches](03-enforce-document-permissions.md)
4. [Creer l'adaptateur de recherche backend](04-create-search-adapter.md)
5. [Integrer la recherche au flow messages](05-integrate-messages-tool.md)
6. [Indexer Outlook](06-index-outlook.md), apres validation du MVP
7. [Indexer les messages Teams](07-index-teams.md), apres validation du MVP

## Strategie de livraison

La premiere livraison couvre seulement SharePoint et OneDrive. Les fichiers partages dans Teams sont deja stockes dans SharePoint ou OneDrive et peuvent donc etre couverts sans indexer les messages Teams.

Outlook et les messages Teams seront traites dans des tickets futurs apres validation de la premiere version. Ils demandent des permissions Microsoft Graph plus sensibles, des choix de retention et une ingestion personnalisee par utilisateur ou par groupe.

## Decisions a prendre avant le premier ticket

- identifier la souscription Azure de developpement
- choisir la region qui respecte les exigences de residence des donnees
- identifier un administrateur Azure capable de creer les ressources et roles
- identifier un administrateur Microsoft 365 capable d'accorder les permissions Graph
- choisir un site SharePoint de test sans information sensible
- choisir le modele d'embeddings et noter le nombre de dimensions produit
- definir une limite de cout mensuelle et une alerte de budget Azure

## Definition globale de termine

La fonctionnalite est terminee seulement si :

- un document SharePoint autorise peut etre trouve
- un document interdit ne peut pas etre trouve, meme avec son titre exact
- un document supprime disparait de l'index
- les donnees de deux organisations restent isolees
- la reponse de `/messages` cite l'URL du document utilise
- les secrets ne sont ni dans Git, ni dans les logs, ni envoyes au modele

## References Microsoft

- [Creer un service Azure AI Search](https://learn.microsoft.com/en-us/azure/search/search-create-service-portal)
- [Creer un index vectoriel](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-create-index)
- [Recherche hybride](https://learn.microsoft.com/en-us/azure/search/hybrid-search-how-to-query)
- [Filtres de securite](https://learn.microsoft.com/en-us/azure/search/search-security-trimming-for-azure-search)
- [Requetes delta Microsoft Graph](https://learn.microsoft.com/en-us/graph/delta-query-overview)
