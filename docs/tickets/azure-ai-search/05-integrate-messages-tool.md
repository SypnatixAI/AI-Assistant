# Ticket 5 - Integrer Azure AI Search au flow messages

<a id="search-05-objective"></a>
## Table des matieres

- [Objectif](#objectif)
- [Dependances](#dependances)
- [Declarer l'outil logique](#etape-1---declarer-loutil-logique)
- [Valider l'appel](#etape-2---valider-lappel-doutil)
- [Completer le contexte securise](#etape-3---completer-avec-le-contexte-securise)
- [Executer et normaliser](#etape-4---executer-et-normaliser)
- [Produire les sources](#etape-5---produire-les-sources-finales)
- [Gerer les echecs](#etape-6---gerer-les-echecs)
- [Tests](#tests-attendus)
- [Scenario manuel](#scenario-manuel-dacceptation)
- [Criteres d'acceptation](#criteres-dacceptation)
- [Hors perimetre](#hors-perimetre)

## Objectif

Brancher l'adaptateur Azure AI Search sur l'outil logique `search_microsoft_365` utilise par l'orchestration de `POST /api/messages`.

Le modele peut demander une recherche Microsoft 365, mais seul le backend construit et execute la requete securisee.

## Dependances

- les tickets 1 a 4 sont termines
- le flow de base `/api/messages` existe jusqu'au registre des outils et a l'execution d'un outil
- un fournisseur de modele supporte les appels d'outils
- le format interne `Evidence` ou son equivalent existe

Si le flow `/messages` n'existe pas encore dans le code, ses fondations doivent etre implementees dans des tickets separes. Ce ticket ne doit pas devenir l'implementation complete des conversations, messages, modeles, outils et connecteurs en une seule livraison.

## Etape 1 - Declarer l'outil logique

Ajouter `search_microsoft_365` au registre uniquement si :

- Azure AI Search est configure pour l'environnement
- l'organisation possede au moins une source Microsoft active et indexee
- le membre courant peut utiliser cette source

Contrat fonctionnel propose :

```json
{
  "name": "search_microsoft_365",
  "description": "Rechercher des informations dans les contenus Microsoft 365 autorises et deja indexes.",
  "parameters": {
    "query": "politique de vacances",
    "sourceTypes": ["sharepoint", "onedrive"],
    "dateFrom": null,
    "dateTo": null
  }
}
```

Le modele peut choisir `query`, `sourceTypes` et les dates. Il ne peut jamais fournir :

- `organizationId`
- `userObjectId`
- `groupObjectIds`
- l'endpoint Azure AI Search
- le nom de l'index
- un filtre OData libre
- une cle ou un token

## Etape 2 - Valider l'appel d'outil

Avant l'execution, le backend verifie :

- que le nom de l'outil existe dans le registre courant
- que `query` n'est pas vide et respecte la longueur maximale
- que chaque `sourceType` appartient a la liste autorisee
- que les dates sont valides et coherentes
- que le budget d'appels d'outils n'est pas depasse
- que la meme recherche n'est pas repetee sans nouvelle information

Le handler orchestre ces appels. Les regles complexes de validation, de budget et d'autorisation restent dans des services ou policies dedies.

## Etape 3 - Completer avec le contexte securise

Apres validation des arguments du modele, le backend ajoute :

- l'organisation issue du membre authentifie
- l'Object ID Entra du membre
- ses groupes applicables
- les sources reellement activees pour l'organisation
- les limites configurees par le backend

Ce contexte securise doit etre cree apres la validation de l'appel d'outil et ne doit pas etre visible par le modele.

## Etape 4 - Executer et normaliser

Appeler l'abstraction de recherche du ticket 4. Convertir les resultats en preuves et les renvoyer au modele avec :

- un identifiant stable
- le passage textuel necessaire
- le titre
- le type de source
- la reference et l'URL d'origine
- la date utile

Le modele doit recevoir uniquement les passages autorises et necessaires. Limiter leur nombre et leur taille pour controler les couts et reduire les risques d'injection de prompt contenue dans les documents.

Les contenus trouves sont des donnees non fiables. Les instructions presentes dans un document ne doivent jamais remplacer les instructions systeme ou autoriser un autre outil.

## Etape 5 - Produire les sources finales

Une source retournee au frontend doit correspondre a une preuve reellement fournie au modele et utilisee pour la reponse.

Pour Azure AI Search :

- `type` vient de `sourceType`
- `title` vient du document indexe
- `url` vient de la source Microsoft 365
- `reference` vient de `sourceId`

Le backend doit retirer une citation inventee par le modele ou qui ne correspond a aucune preuve connue.

## Etape 6 - Gerer les echecs

- si Azure AI Search echoue mais qu'une autre source suffit, retourner la reponse avec un avertissement
- si la source Microsoft est indispensable et indisponible, retourner une reponse d'echec controlee
- ne jamais reessayer sans limite
- ne jamais contourner les ACL pour recuperer davantage de resultats
- conserver le message utilisateur meme lorsque la recherche echoue

## Tests attendus

- outil absent lorsque la source n'est pas configuree
- contrat d'outil limite aux champs fonctionnels
- rejet d'un type de source invente
- contexte d'organisation ajoute par le backend
- resultats Azure convertis en preuves
- source finale liee a une preuve existante
- citation inventee retiree ou refusee
- document interdit absent du contexte du modele
- erreur Azure traduite en avertissement ou erreur selon le scenario
- budget et detection de recherche dupliquee respectes

## Scenario manuel d'acceptation

1. Ajouter dans SharePoint un document fictif autorise a l'utilisateur A.
2. Attendre ou declencher la synchronisation.
3. Poser avec A une question dont la reponse se trouve dans le document.
4. Verifier la reponse et l'URL de citation.
5. Poser la meme question avec l'utilisateur B non autorise.
6. Verifier que le contenu, le titre et l'URL ne sont jamais retournes.
7. Supprimer le document ou retirer l'acces de A.
8. Synchroniser de nouveau et verifier que A ne retrouve plus le document.

## Criteres d'acceptation

- le modele voit un outil fonctionnel, pas les details Azure
- le handler orchestre sans contenir la logique pure de securite ou de recherche
- le backend impose le tenant, les ACL et les limites
- les preuves et citations gardent la provenance du document
- les erreurs partielles suivent la spec fonctionnelle `/messages`
- les tests automatises pertinents sont ajoutes
- `dotnet test Solution.sln` reussit

## Hors perimetre

- indexation des courriels Outlook
- indexation des messages Teams
- streaming de la reponse
- interface d'administration des sources
- ajustement avance du classement
