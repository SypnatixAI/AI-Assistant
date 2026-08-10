# Ticket 1 - Creer l'infrastructure Azure AI Search

<a id="search-01-objective"></a>
## Table des matieres

- [Objectif](#objectif)
- [Prerequis humains](#prerequis-humains)
- [Creer le groupe de ressources](#etape-1---creer-le-groupe-de-ressources)
- [Creer Azure AI Search](#etape-2---creer-azure-ai-search)
- [Configurer l'authentification](#etape-3---configurer-lauthentification)
- [Choisir les embeddings](#etape-4---choisir-les-embeddings)
- [Creer l'index](#etape-5---creer-lindex-de-developpement)
- [Tester manuellement](#etape-6---tester-manuellement)
- [Configuration applicative](#configuration-attendue-dans-lapplication)
- [Criteres d'acceptation](#criteres-dacceptation)
- [References](#references)

## Objectif

Creer un environnement Azure de developpement capable de recevoir et rechercher des passages de documents. A la fin du ticket, un developpeur doit pouvoir ajouter un document de test a l'index et le retrouver avec Search Explorer.

Ce ticket ne se connecte pas encore a SharePoint et ne modifie pas `/api/messages`.

## Prerequis humains

- une souscription Azure active
- le droit de creer un groupe de ressources et des roles Azure
- une limite de cout mensuelle acceptee par l'equipe
- une region choisie pour les donnees

Si l'equipe ne possede pas ces droits, un administrateur Azure doit participer a cette etape.

## Etape 1 - Creer le groupe de ressources

Dans le portail Azure :

1. Ouvrir `Resource groups` puis `Create`.
2. Selectionner la souscription de developpement.
3. Utiliser un nom explicite, par exemple `rg-assistantcore-dev`.
4. Choisir la region retenue par l'equipe.
5. Ajouter des tags comme `environment=dev` et `application=assistantcore`.
6. Creer le groupe.

Toutes les ressources de cette fonctionnalite doivent etre placees dans ce groupe afin de suivre les couts et de pouvoir supprimer l'environnement de test sans toucher aux autres projets.

## Etape 2 - Creer Azure AI Search

Dans le portail Azure :

1. Selectionner `Create a resource`.
2. Rechercher `Azure AI Search`.
3. Selectionner le groupe de ressources cree precedemment.
4. Choisir un nom globalement unique, par exemple `srch-assistantcore-dev-<suffixe>`.
5. Utiliser la meme region que les autres services IA lorsque possible.
6. Pour une preuve de concept locale, commencer avec le niveau `Free` si disponible. Utiliser `Basic` ou `Standard` si les limites du niveau gratuit sont insuffisantes.
7. Creer le service et noter son endpoint, par exemple `https://srch-assistantcore-dev-<suffixe>.search.windows.net`.

Le niveau de prix doit etre valide avec l'equipe avant la creation. Ajouter une alerte de budget Azure; Azure AI Search est facture tant que le service existe.

## Etape 3 - Configurer l'authentification

La cible est une authentification Microsoft Entra avec des roles Azure. Les cles administrateur peuvent etre utilisees temporairement dans Search Explorer, mais elles ne doivent pas etre ajoutees au code ou a Git.

Roles utiles :

| Identite | Role Azure AI Search | Utilisation |
| --- | --- | --- |
| Developpeur autorise | `Search Service Contributor` | Creer l'index |
| Service d'ingestion | `Search Index Data Contributor` | Ajouter, modifier et supprimer des documents |
| Backend AssistantCore | `Search Index Data Reader` | Executer les recherches |

Pour le developpement local, utiliser l'identite Azure du developpeur avec `DefaultAzureCredential`. En environnement Azure, activer une identite managee sur le service qui heberge l'application et lui attribuer uniquement le role necessaire.

## Etape 4 - Choisir les embeddings

La recherche vectorielle demande un modele qui transforme un texte en tableau de nombres.

Avant de creer le champ vectoriel :

1. Choisir le fournisseur et le modele d'embeddings autorise par l'organisation.
2. Deployer ou configurer ce modele dans la meme region lorsque possible.
3. Noter le nombre exact de dimensions retourne par ce deploiement.
4. Ne pas inventer cette valeur : le champ `contentVector` doit utiliser exactement le meme nombre de dimensions.

Si le choix du modele n'est pas encore valide, creer d'abord un index textuel sans vecteur. Le vecteur sera ajoute apres une decision explicite.

## Etape 5 - Creer l'index de developpement

Creer un index nomme par exemple `microsoft-content-dev` avec les champs suivants :

| Champ | Type | Proprietes principales |
| --- | --- | --- |
| `chunkId` | `Edm.String` | cle |
| `organizationId` | `Edm.String` | filterable |
| `sourceId` | `Edm.String` | filterable |
| `sourceType` | `Edm.String` | filterable, facetable |
| `title` | `Edm.String` | searchable, retrievable |
| `content` | `Edm.String` | searchable, retrievable |
| `url` | `Edm.String` | retrievable |
| `modifiedAt` | `Edm.DateTimeOffset` | filterable, sortable |
| `allowedUserIds` | `Collection(Edm.String)` | filterable, non-retrievable |
| `allowedGroupIds` | `Collection(Edm.String)` | filterable, non-retrievable |
| `contentVector` | `Collection(Edm.Single)` | searchable avec les dimensions du modele |

Configurer un profil vectoriel HNSW si `contentVector` est cree. Configurer aussi une recherche textuelle sur `title` et `content`. La recherche hybride sera activee plus tard par l'adaptateur backend.

## Etape 6 - Tester manuellement

Ajouter deux documents fictifs avec des `organizationId` et ACL differents. Aucun contenu d'entreprise reel ne doit etre utilise pour ce test.

Dans Search Explorer :

1. Rechercher un mot present dans le premier document.
2. Ajouter un filtre sur `organizationId`.
3. Verifier que le document de l'autre organisation disparait.
4. Verifier que `allowedUserIds` et `allowedGroupIds` ne sont pas retournes.

## Infrastructure reproductible

Une fois la creation manuelle comprise et validee, ajouter une definition Bicep ou Terraform dans un ticket d'infrastructure dedie. La production ne doit pas dependre d'une liste d'actions manuelles non versionnee.

## Configuration attendue dans l'application

Prevoir des valeurs de configuration, sans secret :

```json
{
  "AzureSearch": {
    "Endpoint": "https://<service>.search.windows.net",
    "IndexName": "microsoft-content-dev"
  }
}
```

Les secrets locaux doivent utiliser `dotnet user-secrets`. En Azure, utiliser l'identite managee et Azure Key Vault lorsqu'un secret reste necessaire.

## Criteres d'acceptation

- le service Azure AI Search existe dans le groupe de ressources de developpement
- l'alerte de budget est configuree
- les roles sont attribues selon le principe du moindre privilege
- l'index contient les champs fonctionnels, de provenance et de securite
- deux documents fictifs peuvent etre ajoutes et filtres par organisation
- aucun secret ou cle Azure n'est ajoute au depot
- la procedure et les valeurs non secretes propres a l'environnement sont consignees

## References

- [Creer Azure AI Search dans le portail](https://learn.microsoft.com/en-us/azure/search/search-create-service-portal)
- [Utiliser les roles Azure avec Azure AI Search](https://learn.microsoft.com/en-us/azure/search/search-security-rbac)
- [Creer un index vectoriel](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-create-index)
