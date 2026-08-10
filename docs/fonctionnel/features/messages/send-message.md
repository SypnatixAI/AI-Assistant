# Envoyer un message a l'assistant

## But

`POST /api/messages` permet a un utilisateur authentifie de poser une question en langage naturel.

Le frontend envoie une seule demande. Le backend s'occupe ensuite de :

- retrouver ou creer la conversation
- enregistrer la question
- comprendre ce que l'utilisateur cherche
- choisir les sources de donnees utiles
- consulter les connecteurs disponibles
- preparer les informations trouvees
- appeler le modele d'intelligence artificielle choisi
- enregistrer la reponse
- retourner la reponse avec ses sources

Les sources peuvent notamment etre :

- SharePoint
- Outlook
- ERP
- CRM
- bases de donnees internes
- autres connecteurs configures pour l'organisation

---

## Route

```http
POST /api/messages
```

## Qui peut utiliser cet endpoint

Un membre avec le role `Admin` ou `User` peut envoyer un message si :

- il est authentifie
- son compte interne est actif
- son organisation est active

Aucune permission applicative supplementaire n'est utilisee.

---

## Donnees envoyees par le frontend

```json
{
  "conversationId": "c5bd45dd-5e70-4e9d-bf49-06367a9c7928",
  "message": "Quelle est l'evolution des ventes pour le dernier trimestre?",
  "model": "gpt"
}
```

### `conversationId`

Ce champ est optionnel.

- s'il est absent ou `null`, le backend cree une nouvelle conversation
- s'il est present, le backend ajoute le message a cette conversation
- la conversation doit appartenir a l'utilisateur connecte et a son organisation

### `message`

Ce champ est obligatoire.

- il contient la question de l'utilisateur
- il ne peut pas etre vide ou contenir seulement des espaces
- sa longueur maximale doit venir de la configuration du backend

Le frontend envoie la question telle qu'elle a ete ecrite. Il n'a pas a la transformer en requete technique.

### `model`

Ce champ est optionnel.

Exemples de familles de modeles :

- `gpt`
- `claude`

Si `model` est absent :

- utiliser le modele par defaut configure pour l'organisation

Si `model` est present :

- verifier qu'il fait partie des modeles autorises et configures pour l'organisation
- refuser une valeur inconnue ou non configuree

Le frontend ne fournit jamais une cle API, une URL de fournisseur ou un secret de connexion.

---

## Donnees que le frontend ne choisit pas

Le frontend ne doit pas envoyer :

- l'identifiant de l'organisation
- la liste des connecteurs a consulter
- les identifiants techniques des outils backend
- les requetes SQL
- les filtres Microsoft Graph techniques
- les cles API des modeles ou des connecteurs

Le backend determine ces informations a partir de l'identite, de la question et de la configuration de l'organisation.

---

## Exemple de reponse

```json
{
  "conversationId": "c5bd45dd-5e70-4e9d-bf49-06367a9c7928",
  "messageId": "cd88b599-833c-4ad0-bfd8-9b65e73198a4",
  "answer": "Les ventes du dernier trimestre ont augmente de 8 % par rapport au trimestre precedent.",
  "model": "gpt",
  "sources": [
    {
      "type": "SharePoint",
      "title": "Rapport des ventes T2",
      "url": "https://metalpro.sharepoint.com/sites/ventes/rapport-t2",
      "reference": "rapport-ventes-t2"
    },
    {
      "type": "ERP",
      "title": "Ventes trimestrielles",
      "url": null,
      "reference": "erp-sales-quarter-2026-q2"
    }
  ],
  "warnings": [],
  "createdAt": "2026-08-06T14:30:00Z"
}
```

### Signification des champs de reponse

- `conversationId` identifie la conversation utilisee ou creee
- `messageId` identifie la reponse de l'assistant enregistree
- `answer` contient la reponse finale en langage naturel
- `model` indique la famille de modele reellement utilisee
- `sources` contient les documents ou donnees utilises pour produire la reponse
- `warnings` indique les sources qui n'ont pas pu etre consultees completement
- `createdAt` indique la date de creation de la reponse

---

## Architecture de recherche retenue

La responsabilite est partagee entre quatre composants.

```text
Modele IA
  demande un outil avec des arguments structures
        |
        v
Backend
  valide la demande et execute l'outil logique
        |
        v
Azure AI Search / ERP / CRM / base interne
  recherche dans les donnees deja indexees ou appelle le systeme cible
        |
        v
Backend
  normalise les donnees et les renvoie au modele
        |
        v
Modele IA
  demande un autre outil ou construit la reponse finale
```

Le modele peut proposer l'outil a utiliser, mais il ne l'execute jamais lui-meme.

Le backend reste toujours responsable de :

- choisir les outils que le modele a le droit de voir
- valider le nom de l'outil demande
- valider les arguments produits par le modele
- appliquer l'organisation et l'identite courantes
- appeler le service externe
- limiter le nombre d'appels
- retirer les donnees interdites ou invalides
- verifier les sources de la reponse finale

### Role exact d'Azure AI Search

Azure AI Search est le moteur de recherche commun pour les contenus Microsoft 365 que l'organisation a choisi d'indexer.

Le backend expose au modele un outil logique nomme `search_microsoft_365`. Le nom de l'outil reste independant du fournisseur technique. Quand cet outil est demande, le backend interroge l'index Azure AI Search configure pour l'environnement.

Azure AI Search ne recupere pas automatiquement les donnees Microsoft 365. Un pipeline d'ingestion separe doit auparavant :

- lire les contenus autorises avec Microsoft Graph
- extraire le texte utile
- decouper les contenus volumineux en passages
- produire les vecteurs si la recherche vectorielle est activee
- conserver le tenant, le type de source, l'URL et l'identifiant d'origine
- copier les utilisateurs et groupes autorises dans les metadonnees de securite
- ajouter, mettre a jour ou supprimer les passages dans l'index

Ce pipeline s'execute en dehors de `POST /api/messages`. Une question utilisateur ne doit jamais declencher l'indexation complete d'une source.

### Perimetre Microsoft 365 progressif

L'objectif a terme peut inclure SharePoint, OneDrive, Outlook et Teams. Ces sources ne doivent pas etre livrees toutes en meme temps.

La premiere version doit couvrir SharePoint et OneDrive. Outlook et Teams sont ajoutes ensuite, car ils demandent une ingestion personnalisee, des permissions Microsoft Graph plus sensibles et des regles de retention plus complexes.

| Phase | Sources | Resultat attendu |
| --- | --- | --- |
| 1 | SharePoint et OneDrive | Rechercher dans les documents et respecter leurs permissions |
| 2 | Outlook | Rechercher dans les courriels et evenements explicitement autorises |
| 3 | Teams | Rechercher dans les messages de canaux ou conversations explicitement autorises |

Le frontend n'envoie jamais un jeton Microsoft Graph, une cle Azure AI Search ou un filtre de securite.

### Securite des resultats

Chaque passage indexe doit contenir `organizationId` ainsi que les identifiants Microsoft Entra des utilisateurs ou groupes autorises.

Avant chaque recherche, le backend construit un filtre qui impose :

- l'organisation courante
- l'utilisateur courant ou un de ses groupes autorises
- les types de sources autorises pour l'organisation

Le filtrage doit etre applique dans la requete Azure AI Search, avant de retourner les resultats au modele. Un filtrage uniquement apres la recherche est insuffisant, car des donnees interdites auraient deja quitte le moteur de recherche.

### Cas des ERP et CRM

Il existe deux strategies possibles.

| Situation | Outil execute par le backend |
| --- | --- |
| Les donnees ERP ou CRM sont indexees dans Azure AI Search | Recherche dans l'index autorise |
| Les donnees ERP ou CRM ne sont pas indexees | Appel direct a l'API ERP ou CRM |

Si les donnees sont indexees :

- le pipeline envoie les donnees vers Azure AI Search avant les questions utilisateur
- chaque donnee est decoupee en un ou plusieurs passages
- les ACL et l'organisation doivent etre enregistrees avec chaque passage
- le backend recherche ces passages avec les memes controles que les contenus Microsoft 365

Si les donnees ne sont pas indexees :

- le backend utilise un outil distinct comme `query_erp` ou `query_crm`
- cet outil appelle directement le systeme cible
- Azure AI Search n'intervient pas dans ce chemin

Les deux strategies peuvent coexister dans la meme organisation.

---

## Etapes de construction fonctionnelle

### 1. Verifier l'authentification

Concretement :

- verifier que le token est present et valide
- lire `externalUserId` et `externalTenantId`
- ne jamais accepter ces valeurs depuis le body

Si l'utilisateur n'est pas authentifie :

- retourner `401 Unauthorized`
- ne creer aucune conversation et aucun message

### 2. Retrouver le contexte interne

Concretement :

- retrouver l'organisation avec `externalTenantId`
- verifier que l'organisation est active
- retrouver le membre avec `externalUserId`
- verifier que le membre est actif

Les roles `Admin` et `User` sont tous les deux acceptes.

Si l'organisation ou le membre ne peut pas utiliser l'application :

- retourner `403 Forbidden`
- arreter le traitement

### 3. Valider le body

Concretement :

- verifier que `message` est present
- retirer les espaces inutiles au debut et a la fin
- refuser un message vide
- verifier la longueur maximale configuree
- verifier le format de `conversationId` lorsqu'il est present
- verifier que `model` est autorise lorsqu'il est present

Si une valeur est invalide :

- retourner `400 Bad Request`
- expliquer quel champ est incorrect

### 4. Creer ou retrouver la conversation

Si `conversationId` est absent :

- creer une conversation interne
- lui donner un nouvel identifiant
- l'associer au membre connecte
- l'associer a l'organisation courante
- enregistrer sa date de creation

Si `conversationId` est present :

- chercher la conversation dans l'organisation courante
- verifier qu'elle appartient au membre connecte
- verifier qu'elle est encore utilisable

Si la conversation n'existe pas ou appartient a un autre utilisateur :

- retourner `404 Not Found`
- ne pas indiquer a quelle organisation ou a quel utilisateur elle appartient

### 5. Enregistrer le message utilisateur

Concretement :

- creer un message avec le type `User`
- enregistrer le texte original
- enregistrer l'utilisateur et la conversation
- enregistrer la date
- definir son etat de traitement initial

Le message doit etre enregistre avant les appels externes. Cela permet de conserver la question meme si un connecteur ou un modele echoue ensuite.

### 6. Choisir le modele d'intelligence artificielle

Concretement :

- utiliser `model` s'il a ete demande et autorise
- sinon utiliser le modele par defaut de l'organisation
- charger la configuration backend correspondant a la famille choisie

Exemples de familles :

- `gpt`
- `claude`

La configuration backend determine ensuite la version precise du modele, son fournisseur, ses limites et ses secrets.

Le reste de l'orchestration utilise une interface commune. Ainsi, les details techniques de GPT et Claude restent dans des adaptateurs differents, mais le flux metier reste le meme.

### 7. Construire le registre des outils autorises

Le backend charge les connecteurs actifs de l'organisation et construit la liste des outils que le modele peut demander.

Exemple de registre :

```json
[
  {
    "name": "search_microsoft_365",
    "description": "Rechercher dans SharePoint, OneDrive, Outlook ou les sources externes indexees.",
    "allowedSourceTypes": [
      "sharepoint",
      "outlook",
      "external"
    ]
  },
  {
    "name": "query_erp",
    "description": "Lire les ventes, commandes, factures et stocks dans l'ERP."
  },
  {
    "name": "query_crm",
    "description": "Lire les clients, contacts et opportunites dans le CRM."
  },
  {
    "name": "search_internal_data",
    "description": "Rechercher dans les donnees internes de l'application."
  }
]
```

Cette liste est dynamique.

Exemples :

- si aucun ERP n'est configure, `query_erp` n'est pas fourni au modele
- si Outlook n'est pas autorise, `outlook` n'apparait pas dans `allowedSourceTypes`
- les identifiants techniques et secrets des connecteurs ne sont jamais fournis au modele

### 8. Decrire chaque outil avec un contrat strict

Chaque outil fourni au modele doit avoir :

- un nom stable
- une description indiquant quand l'utiliser
- un schema JSON des arguments acceptes
- une liste de valeurs autorisees

Exemple simplifie pour Azure AI Search :

```json
{
  "name": "search_microsoft_365",
  "description": "Rechercher des informations professionnelles dans les sources Microsoft 365 autorisees.",
  "parameters": {
    "query": "ventes du dernier trimestre",
    "sourceTypes": [
      "sharepoint",
      "external"
    ],
    "dateFrom": "2026-04-01",
    "dateTo": "2026-06-30"
  }
}
```

Le modele choisit des valeurs fonctionnelles comme `sharepoint`. Le backend les traduit vers les valeurs `sourceType` autorisees dans l'index Azure AI Search.

Le modele ne peut pas fournir librement :

- une URL externe
- un nom de table SQL
- un identifiant de tenant
- un nom d'index ou un endpoint Azure AI Search
- une cle API

### 9. Faire le premier appel au modele pour choisir les outils

Le backend envoie au modele :

- la question actuelle
- l'historique utile de la conversation
- les instructions d'orchestration
- le registre des outils autorises et leurs schemas

A cette etape, le modele peut :

- demander un ou plusieurs outils
- demander une recherche supplementaire apres un premier resultat
- repondre sans outil pour une question qui ne demande aucune donnee d'entreprise

Exemple de decision produite par le modele :

```json
{
  "toolCalls": [
    {
      "id": "call-001",
      "name": "query_erp",
      "arguments": {
        "metric": "sales",
        "dateFrom": "2026-04-01",
        "dateTo": "2026-06-30"
      }
    },
    {
      "id": "call-002",
      "name": "search_microsoft_365",
      "arguments": {
        "query": "rapport ventes trimestre 2026 Q2",
        "sourceTypes": [
          "sharepoint"
        ]
      }
    }
  ]
}
```

Le modele a seulement demande ces outils. Aucun appel ERP ou Azure AI Search n'a encore ete execute.

### 10. Convertir la reponse du fournisseur vers un format interne

GPT et Claude ne retournent pas exactement le meme format technique.

L'adaptateur du fournisseur transforme leur reponse vers une structure interne commune :

```json
{
  "id": "call-001",
  "toolName": "query_erp",
  "arguments": {
    "metric": "sales",
    "dateFrom": "2026-04-01",
    "dateTo": "2026-06-30"
  }
}
```

Concretement :

- l'adaptateur GPT transforme un appel de fonction GPT en `ToolCall`
- l'adaptateur Claude transforme un bloc `tool_use` en `ToolCall`
- le service d'orchestration ne depend pas du format propre a un fournisseur

### 11. Valider la demande d'outil

Le backend ne doit jamais executer directement ce que le modele retourne.

Pour chaque `ToolCall`, il doit verifier :

- que `toolName` existe dans le registre autorise
- que l'outil appartient bien a l'organisation courante
- que les arguments respectent le schema
- que les dates et nombres sont valides
- que la taille de la requete respecte les limites
- que la demande est une operation de lecture
- que le nombre maximal d'appels n'est pas depasse

Si le modele invente un outil comme `delete_erp_invoice`, le backend le refuse sans l'executer.

### 12. Executer Azure AI Search lorsque cet outil est choisi

Si le modele demande `search_microsoft_365`, le backend :

1. lit l'organisation et l'identifiant Entra du membre courant depuis le contexte authentifie
2. obtient les groupes Entra utiles depuis une source backend approuvee
3. valide les types de sources demandes par le modele
4. construit une requete hybride, textuelle et vectorielle si les vecteurs sont actives
5. ajoute obligatoirement le filtre d'organisation et le filtre d'ACL
6. appelle Azure AI Search avec l'identite managee du backend
7. limite le nombre et la taille des resultats
8. traite les erreurs transitoires, les delais et la limitation `429`

Exemple conceptuel de filtre :

```text
organizationId eq '<organisation-courante>'
and sourceType in ('sharepoint', 'onedrive')
and (
  allowedUserIds contient '<utilisateur-courant>'
  or allowedGroupIds contient un groupe de l'utilisateur
)
```

Exemple simplifie de recherche Azure AI Search :

```json
{
  "search": "rapport ventes trimestre 2026 Q2",
  "filter": "organizationId eq 'org-001' and sourceType eq 'sharepoint'",
  "select": "chunkId,sourceId,sourceType,title,content,url,modifiedAt",
  "top": 10,
  "vectorQueries": [
    {
      "kind": "text",
      "text": "rapport ventes trimestre 2026 Q2",
      "fields": "contentVector",
      "k": 50
    }
  ]
}
```

Le filtre d'ACL complet est construit par le backend. Le modele ne peut jamais fournir ou retirer `organizationId`, `allowedUserIds` ou `allowedGroupIds`.

### Contenu retourne par Azure AI Search

Le contenu utile est extrait et decoupe pendant l'ingestion, pas pendant la question utilisateur. Azure AI Search retourne donc directement les passages les mieux classes avec leur provenance.

Pour chaque resultat, le backend conserve au minimum :

- l'identifiant du passage et du document source
- le type de source
- le titre et le passage textuel
- l'URL Microsoft 365 d'origine
- la date de derniere modification
- le score de recherche utilise seulement pour le classement interne

Les champs contenant les ACL ne doivent pas etre renvoyes au modele ou au frontend.

### 13. Executer les connecteurs directs

Si le modele demande `query_erp` ou `query_crm`, le backend appelle le connecteur direct correspondant.

Concretement :

- charger la configuration chiffree du connecteur
- construire une requete avec des parametres valides
- appliquer l'identifiant de l'organisation dans le backend
- executer uniquement une operation de lecture
- appliquer un delai maximal
- ne jamais transmettre les secrets au modele

Les appels independants peuvent etre executes en parallele apres validation.

### 14. Normaliser les resultats en preuves

Azure AI Search, un ERP et un CRM ne retournent pas le meme format.

Le backend transforme chaque resultat dans un format commun :

```json
{
  "evidenceId": "source-001",
  "sourceType": "SharePoint",
  "title": "Rapport des ventes T2",
  "content": "Les ventes nettes du trimestre sont de...",
  "url": "https://metalpro.sharepoint.com/sites/ventes/rapport-t2",
  "reference": "drive-item-789",
  "occurredAt": "2026-07-05T10:00:00Z"
}
```

Concretement, le backend :

- supprime les doublons
- retire les resultats non pertinents
- classe les preuves
- conserve la provenance exacte
- limite la taille du contenu
- attribue un `evidenceId` interne a chaque preuve
- traite le contenu recupere comme une donnee non fiable
- ignore les instructions trouvees dans un document qui essaient de modifier le comportement du modele ou de demander un autre outil

Une information sans provenance ne peut pas devenir une source finale.

### 15. Renvoyer les resultats d'outil au modele

Le backend construit un resultat associe au `ToolCall` original :

```json
{
  "toolCallId": "call-002",
  "status": "Success",
  "evidence": [
    {
      "evidenceId": "source-001",
      "title": "Rapport des ventes T2",
      "content": "Les ventes ont augmente de 8 %.",
      "sourceType": "SharePoint"
    }
  ]
}
```

L'adaptateur du modele convertit ce resultat interne :

- en resultat d'appel de fonction pour GPT
- en bloc `tool_result` pour Claude

Le backend rappelle ensuite le meme modele avec :

- la question initiale
- les appels d'outils demandes
- les resultats normalises
- les references `evidenceId`

### 16. Demander au modele s'il faut continuer ou arreter

Apres chaque resultat d'outil, le modele doit evaluer si les preuves sont suffisantes.

Il retourne une decision structuree parmi trois valeurs :

- `continue` : une nouvelle recherche utile est encore possible
- `answer` : les preuves disponibles permettent de repondre
- `cannotAnswer` : aucune recherche supplementaire raisonnable ne permettra de repondre

#### Decision `continue`

Le modele choisit `continue` lorsqu'il sait quelles informations manquent et quel outil peut probablement les trouver.

```json
{
  "decision": "continue",
  "reason": "Les chiffres de ventes sont disponibles, mais la cause de la baisse manque.",
  "toolCalls": [
    {
      "id": "call-003",
      "name": "query_crm",
      "arguments": {
        "customerName": "Client ABC"
      }
    },
    {
      "id": "call-004",
      "name": "search_microsoft_365",
      "arguments": {
        "query": "Client ABC baisse ventes probleme livraison",
        "sourceTypes": [
          "outlook",
          "sharepoint"
        ]
      }
    }
  ]
}
```

Le backend valide ces nouveaux appels, les execute, puis renvoie les nouvelles preuves au modele.

Exemple : le premier resultat ERP montre une baisse. Le modele consulte ensuite le CRM et Outlook pour chercher une explication.

#### Decision `answer`

Le modele choisit `answer` lorsque les preuves disponibles repondent directement a la question.

```json
{
  "decision": "answer",
  "reason": "Les donnees ERP et le courriel du client expliquent la baisse.",
  "answer": "La baisse vient principalement du report de deux commandes apres un probleme de livraison.",
  "evidenceIds": [
    "source-001",
    "source-004"
  ]
}
```

Le modele doit citer seulement des `evidenceIds` recus du backend.

#### Decision `cannotAnswer`

Le modele choisit `cannotAnswer` lorsque :

- toutes les sources pertinentes disponibles ont ete consultees
- aucun autre outil ne peut raisonnablement trouver l'information
- une nouvelle recherche repeterait une recherche precedente
- les derniers resultats n'apportent aucune nouvelle preuve
- les outils disponibles ne couvrent pas le besoin

```json
{
  "decision": "cannotAnswer",
  "reason": "Les sources disponibles contiennent les chiffres, mais aucune explication de la baisse.",
  "answer": "Je n'ai pas trouve suffisamment d'informations pour expliquer cette baisse.",
  "evidenceIds": [
    "source-001"
  ]
}
```

Cette reponse est enregistree normalement. Elle indique clairement la limite des informations trouvees sans inventer une explication.

#### Comment le modele decide d'arreter

Les instructions d'orchestration doivent demander au modele de verifier :

- si la question possede maintenant une reponse appuyee par des preuves
- si les informations manquantes sont clairement identifiees
- si un outil non encore utilise peut trouver ces informations
- si une nouvelle recherche serait differente et utile
- si les nouveaux resultats apportent reellement quelque chose

Le modele ne doit pas continuer uniquement pour accumuler plus de sources lorsqu'une reponse suffisamment appuyee existe deja.

#### Limites de securite du backend

Il n'existe pas de nombre fonctionnel fixe de tours. Une question simple peut s'arreter apres une recherche, tandis qu'une question complexe peut demander plusieurs outils.

Le backend doit toutefois conserver des budgets configurables pour eviter une boucle infinie, un delai excessif ou un cout non controle :

- `maxExecutionTime`
- `maxToolCalls`
- `maxModelTokens`
- `maxEstimatedCost`
- `maxResultsPerTool`
- `maxContextSize`
- `maxRepeatedToolCalls`

Ces valeurs viennent de la configuration du backend et peuvent varier selon l'organisation ou le modele utilise.

Avant chaque nouveau tour, le backend verifie :

- que le budget de temps n'est pas depasse
- que le budget d'appels, de tokens et de cout est encore disponible
- que le meme outil avec les memes arguments n'a pas deja ete execute
- que le dernier tour a produit au moins une nouvelle preuve
- qu'au moins un outil demande reste disponible

Le fonctionnement attendu est donc :

```text
Le modele decide s'il est utile de continuer
                    +
Le backend decide si l'execution a encore le droit de continuer
```

Si un budget technique est atteint, le backend arrete les nouveaux outils et demande au modele de produire `answer` ou `cannotAnswer` uniquement avec les preuves deja disponibles.

### 17. Construire la reponse finale

Concretement :

- demander une reponse basee uniquement sur les preuves recuperees
- demander au modele de citer les `evidenceId`
- demander au modele de signaler les informations manquantes
- appliquer un delai maximal

Le controleur appelle le handler. Le handler orchestre les services applicatifs. Les services specialises gerent les connecteurs et les fournisseurs d'intelligence artificielle.

### 18. Verifier la reponse et les sources

Avant de retourner la reponse :

- verifier que la reponse n'est pas vide
- verifier que les sources citees existent dans les resultats recuperes
- retirer les references inconnues
- construire la liste finale `sources`

Si les informations sont insuffisantes, la reponse doit le dire clairement, par exemple :

`Je n'ai pas trouve suffisamment d'informations dans les sources disponibles pour repondre avec certitude.`

Le backend ne doit pas fabriquer une source pour rendre la reponse plus convaincante.

### 19. Enregistrer la reponse de l'assistant

Concretement :

- creer un message avec le type `Assistant`
- enregistrer le texte final
- enregistrer le modele utilise
- enregistrer les references des sources
- enregistrer les avertissements et la date
- marquer le traitement du message utilisateur comme termine

Si le traitement echoue, enregistrer un etat d'echec technique sans enregistrer une fausse reponse de l'assistant.

### 20. Retourner la reponse au frontend

Si le traitement reussit :

- retourner `200 OK`
- retourner la conversation
- retourner le message de l'assistant
- retourner le modele utilise
- retourner les sources et les avertissements

La premiere version retourne une reponse JSON complete. Elle ne fait pas de streaming.

---

## Gestion des echecs de connecteurs

### Une source echoue, mais d'autres sources fonctionnent

Le backend peut continuer si les informations restantes permettent de repondre.

Concretement :

- produire la reponse avec les sources disponibles
- ajouter un message dans `warnings`
- ne pas presenter la recherche comme complete

Exemple :

```json
{
  "warnings": [
    "Outlook n'a pas pu etre consulte."
  ]
}
```

### Toutes les sources necessaires echouent

Le backend ne doit pas demander au modele d'inventer une reponse.

Il doit retourner une erreur technique adaptee, par exemple `502 Bad Gateway` ou `504 Gateway Timeout`.

---

## Erreurs a prevoir

### 400 Bad Request

- le message est absent, vide ou trop long
- `conversationId` est invalide
- le modele demande est inconnu ou non configure

### 401 Unauthorized

- l'utilisateur n'est pas connecte
- le token est invalide ou expire

### 403 Forbidden

- l'organisation est inactive
- le membre est inactif
- l'utilisateur ne peut pas utiliser l'organisation courante

### 404 Not Found

- la conversation n'existe pas pour cet utilisateur dans l'organisation courante

### 502 Bad Gateway

- un fournisseur externe ou un connecteur retourne une erreur qui empeche de produire une reponse
- le fournisseur du modele refuse ou ne peut pas traiter la demande

### 504 Gateway Timeout

- les connecteurs ou le modele depassent le delai maximal autorise

### 500 Internal Server Error

- une erreur interne inattendue empeche le traitement

---

## Regles metier fixes

- un seul endpoint frontend orchestre tout le traitement
- `Admin` et `User` actifs peuvent envoyer un message
- une conversation appartient a un utilisateur et a une organisation
- le frontend ne choisit pas directement les connecteurs
- le backend fournit au modele seulement les outils actifs de l'organisation
- le modele peut demander un outil, mais seul le backend peut l'executer
- chaque demande d'outil doit respecter un schema strict
- un outil invente ou non autorise ne doit jamais etre execute
- Azure AI Search est utilise pour les contenus Microsoft 365 deja indexes
- un ERP ou CRM non indexe est consulte avec un connecteur direct
- le pipeline d'ingestion synchronise les droits Microsoft 365 dans l'index
- chaque recherche impose le tenant et les ACL avant de retourner les resultats
- le modele decide de continuer, de repondre ou de reconnaitre qu'il ne peut pas repondre
- aucun nombre fonctionnel fixe de tours n'est impose
- le backend impose des budgets techniques configurables de temps, d'appels, de tokens et de cout
- le backend bloque les recherches identiques et les boucles sans nouvelle preuve
- les droits du systeme source doivent toujours etre respectes
- seules les familles de modeles configurees peuvent etre utilisees
- la version precise du modele et les secrets restent dans le backend
- le message utilisateur est enregistre avant les appels externes
- la reponse de l'assistant est enregistree avant d'etre retournee
- chaque source retournee doit correspondre a une donnee reellement consultee
- aucune donnee d'une autre organisation ne doit etre exposee
- la premiere version retourne une reponse complete sans streaming

---

## Resume tres simple

`POST /api/messages` fait ce travail :

1. verifie l'utilisateur et son organisation
2. valide la question et le modele demande
3. cree ou retrouve la conversation
4. enregistre la question
5. charge les outils autorises pour l'organisation
6. envoie la question et les outils a GPT, Claude ou un autre modele configure
7. recoit les outils demandes par le modele
8. valide chaque outil et ses arguments dans le backend
9. appelle Azure AI Search, l'ERP, le CRM ou la base interne
10. normalise les resultats en preuves avec des references
11. renvoie les resultats d'outils au modele
12. lit la decision `continue`, `answer` ou `cannotAnswer` du modele
13. continue seulement si une nouvelle recherche utile est possible et que les budgets backend le permettent
14. construit une reponse appuyee par les preuves ou reconnait que l'information est insuffisante
15. verifie et enregistre la reponse et ses sources
16. retourne la reponse au frontend

---

## References techniques

- [Creer un service Azure AI Search](https://learn.microsoft.com/en-us/azure/search/search-create-service-portal)
- [Creer un index vectoriel](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-create-index)
- [Executer une recherche hybride](https://learn.microsoft.com/en-us/azure/search/hybrid-search-how-to-query)
- [Filtrer les resultats avec des identifiants de securite](https://learn.microsoft.com/en-us/azure/search/search-security-trimming-for-azure-search)
- [Suivre les changements avec les requetes delta Microsoft Graph](https://learn.microsoft.com/en-us/graph/delta-query-overview)
- [Suivre les changements SharePoint et OneDrive](https://learn.microsoft.com/en-us/graph/api/driveitem-delta?view=graph-rest-1.0)
- [Appels d'outils avec OpenAI](https://platform.openai.com/docs/guides/function-calling)
- [Appels d'outils avec Claude](https://docs.anthropic.com/en/docs/agents-and-tools/tool-use/overview)
