# Ticket 4 - Creer l'adaptateur de recherche backend

<a id="search-04-objective"></a>
## Table des matieres

- [Objectif](#objectif)
- [Dependances](#dependances)
- [Separation des couches](#separation-des-couches)
- [Ajouter le client Azure](#etape-1---ajouter-le-client-azure)
- [Construire la recherche](#etape-2---construire-la-recherche)
- [Normaliser les resultats](#etape-3---normaliser-les-resultats)
- [Gerer les erreurs](#etape-4---gerer-les-erreurs)
- [Ajouter la telemetrie](#etape-5---ajouter-la-telemetrie)
- [Tests](#tests-attendus)
- [Criteres d'acceptation](#criteres-dacceptation)
- [References](#references)

## Objectif

Permettre au code applicatif de rechercher du contenu Microsoft sans dependre directement du SDK Azure AI Search.

A la fin du ticket, un test d'integration controle doit pouvoir envoyer une question, appliquer les droits du membre et recevoir une liste de preuves normalisees. Ce ticket ne branche pas encore le modele IA.

## Dependances

- les tickets 1 a 3 sont termines
- des documents fictifs et securises existent dans l'index de developpement
- le backend possede une identite ayant le role `Search Index Data Reader`

## Separation des couches

Respecter Clean Architecture :

| Couche | Responsabilite |
| --- | --- |
| Application | definir le besoin de recherche et le resultat attendu |
| Domain/Application service | construire et valider le contexte fonctionnel autorise |
| Infrastructure | utiliser le SDK Azure AI Search et traduire les resultats |
| Configuration/DI | fournir endpoint, index et identite Azure |
| Controller/Handler | orchestrer seulement l'appel au service |

L'interface applicative ne doit pas exposer `SearchClient`, `SearchOptions` ou un autre type du SDK Azure.

Exemple conceptuel de demande applicative :

```text
query
organizationId
userObjectId
groupObjectIds
allowedSourceTypes
dateFrom/dateTo optionnels
maximumResults
```

Les champs de securite viennent du contexte backend. Ils ne sont jamais construits a partir des arguments produits par le modele.

## Etape 1 - Ajouter le client Azure

Ajouter seulement le package officiel necessaire pour Azure AI Search. Configurer le client avec :

- l'endpoint depuis `AzureSearch:Endpoint`
- le nom d'index depuis `AzureSearch:IndexName`
- `DefaultAzureCredential` pour le developpement et l'identite managee
- un delai maximal et une politique de reprise bornes

Valider les options de configuration au demarrage. Un endpoint ou un index manquant doit produire une erreur explicite avant la premiere requete utilisateur.

## Etape 2 - Construire la recherche

La premiere version doit executer une recherche hybride lorsque les embeddings sont disponibles :

- recherche textuelle sur `title` et `content`
- recherche vectorielle sur `contentVector`
- filtre d'organisation et d'ACL construit par le backend
- filtre optionnel sur les types de sources autorises
- filtre optionnel sur les dates
- nombre de resultats borne par la configuration
- selection limitee aux champs utiles

Commencer avec des valeurs de classement simples. Le semantic ranker et les ajustements fins doivent etre actives seulement apres avoir constitue un petit jeu de questions et de resultats attendus.

## Etape 3 - Normaliser les resultats

Transformer chaque resultat Azure AI Search vers une preuve interne stable :

```json
{
  "evidenceId": "chunk-001",
  "sourceType": "sharepoint",
  "sourceReference": "document-123",
  "title": "Politique de vacances",
  "content": "Passage utile...",
  "url": "https://contoso.sharepoint.com/...",
  "modifiedAt": "2026-08-09T12:00:00Z"
}
```

Ne pas exposer au modele :

- les ACL
- l'endpoint et le nom de l'index
- les vecteurs
- les identifiants de tenant techniques
- les details d'authentification Azure

Les scores peuvent etre conserves pour la telemetrie et le classement, mais ils ne doivent pas etre presentes comme une probabilite de verite.

## Etape 4 - Gerer les erreurs

Traduire les erreurs techniques vers des erreurs applicatives controlees :

- configuration invalide
- authentification Azure refusee
- index absent
- requete invalide
- limitation `429`
- delai depasse
- service temporairement indisponible

Ne pas masquer l'exception originale dans les logs internes. Ne pas retourner au frontend les cles, tokens, endpoints sensibles ou corps complets de documents.

## Etape 5 - Ajouter la telemetrie

Mesurer sans enregistrer le texte complet de la question ou des documents :

- duree de recherche
- nombre de resultats
- types de sources
- succes ou type d'echec
- nombre d'appels et reprises
- organisation sous une forme conforme a la politique de logs

## Tests attendus

- construction d'une recherche avec filtre d'organisation
- ajout des ACL utilisateur et groupe
- validation des types de sources
- normalisation correcte d'un resultat
- aucun champ de securite dans la preuve
- limitation du nombre de resultats
- traduction des erreurs Azure
- test d'integration contre un index de test ou un serveur simule approuve

## Criteres d'acceptation

- la couche Application ne depend pas du SDK Azure AI Search
- l'adaptateur utilise une identite Azure, pas une cle codee en dur
- toute requete contient les filtres de securite obligatoires
- les resultats suivent le format commun de preuve
- les erreurs et la telemetrie ne divulguent pas le contenu sensible
- les tests automatises pertinents sont ajoutes
- `dotnet test Solution.sln` reussit

## References

- [Recherche hybride Azure AI Search](https://learn.microsoft.com/en-us/azure/search/hybrid-search-how-to-query)
- [Requete vectorielle](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-query)
- [Configuration du semantic ranker](https://learn.microsoft.com/en-us/azure/search/semantic-how-to-configure)
