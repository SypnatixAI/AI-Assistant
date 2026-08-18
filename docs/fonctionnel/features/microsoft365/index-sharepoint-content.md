# Indexer les documents SharePoint dans Azure AI Search

## Table des matières

- [But](#m365-sharepoint-purpose)
- [Résultat attendu](#m365-sharepoint-result)
- [Périmètre de la première version](#m365-sharepoint-scope)
- [Notions importantes](#m365-sharepoint-concepts)
- [Architecture générale](#m365-sharepoint-architecture)
- [Responsabilités des composants](#m365-sharepoint-components)
- [Connexion du tenant Microsoft 365](#m365-sharepoint-onboarding)
- [Permissions Microsoft nécessaires](#m365-sharepoint-permissions)
- [Sites, bibliothèques et listes autorisés](#m365-sharepoint-sources)
- [Types de contenus Microsoft 365](#m365-sharepoint-content-types)
- [Données conservées dans la base](#m365-sharepoint-persistence)
- [Création des webhooks](#m365-sharepoint-webhooks)
- [Renouvellement des webhooks](#m365-sharepoint-webhook-renewal)
- [Synchronisation initiale](#m365-sharepoint-initial-sync)
- [Synchronisation des changements](#m365-sharepoint-delta-sync)
- [Traitement par le worker](#m365-sharepoint-worker)
- [Téléchargement et extraction](#m365-sharepoint-extraction)
- [Traitement des archives](#m365-sharepoint-archives)
- [Traitement des pages et listes](#m365-sharepoint-pages-lists)
- [Traitement de OneDrive](#m365-sharepoint-onedrive)
- [Découpage en passages](#m365-sharepoint-chunking)
- [Création des embeddings](#m365-sharepoint-embeddings)
- [Structure de l’index Azure AI Search](#m365-sharepoint-search-index)
- [Mise à jour et suppression](#m365-sharepoint-index-updates)
- [Respect des permissions](#m365-sharepoint-document-security)
- [Connecteur de recherche Microsoft 365](#m365-sharepoint-search-connector)
- [Gestion des erreurs](#m365-sharepoint-errors)
- [Configuration et secrets](#m365-sharepoint-configuration)
- [Développement local](#m365-sharepoint-local-development)
- [Test complet avec le tenant fictif](#m365-sharepoint-end-to-end-test)
- [Stratégie des tickets et des tests](#m365-sharepoint-test-tickets)
- [Ordre d’implémentation](#m365-sharepoint-implementation-order)
- [Définition de terminé](#m365-sharepoint-definition-of-done)
- [Références](#m365-sharepoint-references)

<a id="m365-sharepoint-purpose"></a>
## But

Cette fonctionnalité permet à une organisation de connecter son environnement
Microsoft 365 à AssistantCore.

AssistantCore récupère les documents SharePoint autorisés, extrait leur texte
et les ajoute dans Azure AI Search. Les utilisateurs peuvent ensuite poser une
question à l’assistant et retrouver les documents auxquels ils ont accès.

L’ingestion des documents est indépendante de `POST /api/messages`. Une
question utilisateur ne doit jamais déclencher une synchronisation complète
de SharePoint.

<a id="m365-sharepoint-result"></a>
## Résultat attendu

À la fin de l’implémentation :

1. un administrateur connecte le tenant Microsoft 365 fictif;
2. AssistantCore découvre les sites et bibliothèques SharePoint;
3. un administrateur choisit les sites à indexer;
4. un worker récupère les documents;
5. le texte est découpé en passages;
6. un embedding est créé pour chaque passage;
7. les passages sont ajoutés dans Azure AI Search;
8. un nouveau fichier est détecté et indexé;
9. un fichier modifié remplace son ancienne version;
10. un fichier supprimé disparaît de l’index;
11. une modification de permissions met à jour l’accès au document;
12. le connecteur `search_microsoft_365` retrouve les passages autorisés;
13. la réponse de l’assistant cite le document SharePoint utilisé.

<a id="m365-sharepoint-scope"></a>
## Périmètre de la première version

La première version couvre :

- SharePoint Online;
- les bibliothèques de documents;
- les pages SharePoint modernes;
- les listes SharePoint et leurs éléments;
- le OneDrive professionnel individuel des utilisateurs du tenant;
- les documents Word;
- les classeurs Excel;
- les présentations PowerPoint;
- les PDF contenant du texte;
- les PDF ou images qui demandent de l’OCR;
- les fichiers texte, CSV, JSON, XML, HTML et Markdown;
- les formats OpenDocument;
- les courriels enregistrés comme fichiers EML ou MSG;
- les archives ZIP et GZ;
- les autres formats contenant du texte ou des images extractibles;
- la synchronisation initiale;
- la détection des créations, modifications et suppressions;
- la détection des changements de partage;
- la recherche textuelle et vectorielle;
- les permissions accordées à des utilisateurs Microsoft Entra;
- les permissions accordées à des groupes Microsoft Entra;
- les groupes SharePoint qui ne correspondent pas à un groupe Microsoft Entra;
- les utilisateurs invités externes autorisés dans le tenant du client;
- les liens « Toute personne disposant du lien »;
- les tests avec le tenant Microsoft 365 fictif.

La première version ne couvre pas :

- les fichiers audio;
- les fichiers vidéo;
- les exécutables et fichiers binaires sans texte ou image extractible;
- le OneDrive grand public d’un compte Outlook.com ou Hotmail;
- Outlook et les messages Teams.

Un type de fichier non audio ou vidéo doit être accepté par le pipeline, puis
orienté vers l’extracteur approprié. Si aucun texte ni aucune image utile ne
peut en être extrait, le fichier est enregistré comme traité sans contenu
indexable. Il ne doit pas faire échouer la synchronisation complète.

Un document dont les permissions ne peuvent pas être comprises de manière
fiable ne doit jamais devenir visible dans la recherche.

<a id="m365-sharepoint-concepts"></a>
## Notions importantes

### Site SharePoint

Un site est un espace SharePoint appartenant à une organisation.

Exemple :

```text
https://contoso.sharepoint.com/sites/ressources-humaines
```

### Bibliothèque

Une bibliothèque est un espace de fichiers à l’intérieur d’un site. Microsoft
Graph la représente comme un `drive`.

### Worker

Le worker est une application backend séparée qui exécute les tâches longues :

- lire les changements SharePoint;
- télécharger les documents;
- extraire le texte;
- créer les passages;
- demander les embeddings;
- écrire dans Azure AI Search.

### Webhook

Le webhook est une URL publique appelée par Microsoft lorsqu’une bibliothèque
change. La notification ne contient pas nécessairement le fichier complet.
Elle indique au worker qu’il doit vérifier les changements.

### Passage ou chunk

Un passage est une petite partie du texte d’un document. Un document long
produit plusieurs passages.

### Embedding

Un embedding est une représentation numérique d’un passage. Il permet de
retrouver un texte qui exprime une idée proche même si les mots ne sont pas
exactement les mêmes.

### Delta link

Le `deltaLink` est une URL opaque retournée par Microsoft Graph. Elle permet de
demander uniquement les changements survenus depuis la dernière
synchronisation.

<a id="m365-sharepoint-architecture"></a>
## Architecture générale

```text
Microsoft Graph
      |
      | notification HTTPS
      v
AssistantCore Webhooks
      |
      | message court
      v
Azure Service Bus
      |
      v
AssistantCore Ingestion Worker
      |
      +--> Microsoft Graph : fichiers et permissions
      |
      +--> Extracteur : texte lisible
      |
      +--> Fournisseur d’embeddings
      |
      +--> Azure AI Search : passages indexés

Utilisateur
      |
      v
POST /api/messages
      |
      v
Connecteur search_microsoft_365
      |
      v
Azure AI Search
```

Les appels vers Microsoft Graph, Azure Service Bus, le fournisseur
d’embeddings et Azure AI Search suivent obligatoirement la chaîne :

```text
Service applicatif
  -> interface applicative
  -> adaptateur Infrastructure
  -> client AssistantCore.ExternalServices
  -> SDK ou API externe
```

Le worker ne doit pas appeler directement un SDK externe.

<a id="m365-sharepoint-components"></a>
## Responsabilités des composants

### AssistantCore.Service

Le backend principal :

- expose les actions d’administration du connecteur;
- retourne les sites disponibles;
- permet d’activer ou désactiver un site;
- expose le connecteur de recherche Microsoft 365;
- applique l’organisation et les permissions lors des recherches.

Les controllers injectent uniquement `IDispatcher`.

### AssistantCore Webhooks

Un petit hôte HTTP séparé reçoit les notifications Microsoft Graph.

Il doit seulement :

- répondre à la validation initiale de Microsoft;
- reconnaître la souscription;
- valider le `clientState`;
- placer une demande de synchronisation dans Azure Service Bus;
- retourner rapidement `200 OK` ou `202 Accepted`.

Il ne télécharge aucun document et ne crée aucun embedding.

### AssistantCore.Ingestion.Worker

Le worker séparé :

- consomme les messages Azure Service Bus;
- exécute les synchronisations;
- traite les documents;
- renouvelle les souscriptions Microsoft Graph;
- effectue les vérifications périodiques de sécurité;
- reprend les travaux qui ont échoué temporairement.

### AssistantCore.ExternalServices

Ce projet contient les clients responsables des appels externes :

- client Microsoft Graph;
- client Azure AI Search;
- client du fournisseur d’embeddings;
- client Azure Service Bus.

Les types propres aux SDK externes ne doivent pas remonter dans la couche
Application.

### AssistantCore.Repository

La persistence conserve la configuration et les états de synchronisation. Elle
ne conserve pas le texte complet des documents.

<a id="m365-sharepoint-onboarding"></a>
## Connexion du tenant Microsoft 365

### But du consentement

Le consentement administrateur autorise le Worker AssistantCore à obtenir un
token technique Microsoft Graph pour le tenant du client. Le Worker peut alors
lire les sources SharePoint et OneDrive que l'organisation a choisi d'activer,
puis injecter leur contenu dans Azure AI Search.

Sans ce consentement, Azure AI Search reste disponible, mais le Worker ne peut
obtenir aucun contenu Microsoft 365 :

```text
Microsoft Graph refuse l'accès au Worker
  -> aucune source SharePoint ou OneDrive ne peut être lue
  -> aucun document Microsoft 365 ne peut être traité
  -> aucun passage Microsoft 365 ne peut être injecté dans Azure AI Search
```

Le consentement n'active pas automatiquement tous les sites du tenant. Il
autorise les appels Graph; AssistantCore applique ensuite sa propre liste de
sites et de sources explicitement activés.

### Flow détaillé de connexion

```text
Admin connecté
  -> POST /api/microsoft365/consent
  -> Controller -> Dispatcher -> Handler -> Service
  -> création d'une connexion PendingConsent et d'un state protégé
  <- authorizationUrl Microsoft

Admin chez Microsoft
  -> accepte les permissions
  -> GET /api/microsoft365/consent/callback?code=...&state=...
  -> Controller -> Dispatcher -> Handler -> Service
  -> validation du state et identification du tenant
  -> connexion Active
  -> Worker autorisé à obtenir un token Graph et à lire les sources activées
```

#### Démarrer le consentement

L'administrateur authentifié appelle :

```http
POST /api/microsoft365/consent
Authorization: Bearer <JWT AssistantCore>
```

Le controller envoie une commande au `IDispatcher` et ne contient aucune
logique métier. Le handler appelle uniquement le service applicatif de
connexion Microsoft 365.

Le service retrouve l'utilisateur et son organisation depuis le JWT. Aucun
`organizationId` n'est accepté depuis la requête. Il vérifie ensuite que le
membre possède le rôle `Admin`; sinon, il retourne `403 Forbidden`.

Le service génère un `state` contenant l'organisation, un nonce aléatoire et
une expiration courte. Le `state` est chiffré et signé. Seule son empreinte est
persistée afin de reconnaître le callback sans conserver sa valeur brute.

La connexion passe à l'état suivant :

```text
Status: PendingConsent
TenantId: inconnu
Consentement: pas encore accordé
```

Le client Microsoft construit ensuite l'URL multitenant et l'endpoint la
retourne au frontend :

```json
{
  "authorizationUrl": "https://login.microsoftonline.com/organizations/..."
}
```

Le frontend redirige le navigateur vers cette URL. À la fin de cette première
opération, aucune source Microsoft 365 ne peut encore être lue.

#### Terminer le consentement

Après la décision de l'administrateur, Microsoft rappelle AssistantCore :

```http
GET /api/microsoft365/consent/callback?code=<code>&state=<state>
```

Le service valide la signature, l'expiration, l'organisation et l'usage unique
du `state`. Il échange ensuite le code auprès de Microsoft, identifie le tenant
consenti et refuse ce tenant s'il est déjà associé à une autre organisation.

Après un retour valide, la connexion devient :

```text
Status: Active
TenantId: tenant Microsoft validé
ConsentValidatedAt: date du callback
```

Le token technique est protégé et n'est jamais écrit dans les logs. Le Worker
dispose alors de la condition nécessaire pour demander un token Graph et lire
les sources qui seront activées dans les étapes suivantes de l'ingestion.

Le callback ne découvre pas encore les sites, ne télécharge aucun document et
n'écrit rien dans Azure AI Search. Ces traitements appartiennent aux étapes de
découverte et de synchronisation.

#### Révoquer la connexion

Un administrateur peut retirer l'accès d'AssistantCore avec :

```http
DELETE /api/microsoft365/connections/{connectionId}
Authorization: Bearer <JWT AssistantCore>
```

Le controller transmet une commande au `IDispatcher`. Le handler appelle le
service de connexion, qui retrouve l'organisation depuis le JWT et refuse un
membre non administrateur. Aucun `organizationId` n'est accepté dans la
requête.

Le service recherche la connexion avec son identifiant et l'organisation
courante. Une connexion appartenant à une autre organisation est traitée comme
absente. Lorsqu'elle est trouvée, la connexion et son connecteur deviennent
inactifs et le jeton technique conservé est supprimé :

```text
Admin connecté
  -> DELETE /api/microsoft365/connections/{connectionId}
  -> Controller -> Dispatcher -> Handler -> Service
  -> connexion Revoked
  -> connecteur Inactive
  -> suppression du jeton technique
  -> Worker refuse les nouveaux traitements
```

Un nouvel appel de consentement est nécessaire pour remettre cette connexion
en service. Une connexion révoquée ne peut pas passer directement à `Active`
ou `Error`.

Le travail demandé au client doit être limité à une séance d’onboarding.

L’administrateur :

1. ouvre l’écran de connexion Microsoft 365;
2. se connecte avec un compte administrateur;
3. accepte les permissions présentées par Microsoft;
4. choisit les sites autorisés;
5. confirme l’activation.

AssistantCore effectue ensuite automatiquement :

- la validation du tenant;
- l’enregistrement du connecteur;
- la découverte des bibliothèques;
- la création des souscriptions;
- la synchronisation initiale;
- les synchronisations suivantes.

Le client ne doit pas fournir de secret, certificat ou token à AssistantCore.

<a id="m365-sharepoint-permissions"></a>
## Permissions Microsoft nécessaires

Pour le premier test dans le tenant fictif, utiliser des permissions
applicatives documentées pour les webhooks et les requêtes delta :

- `Files.Read.All`;
- `Sites.Read.All`;
- la permission minimale nécessaire pour lire les groupes du membre lors
  d’une recherche.

Ces permissions demandent un consentement administrateur.

Elles donnent une visibilité étendue dans le tenant. AssistantCore doit donc
conserver sa propre liste de sites activés et refuser toute ingestion provenant
d’un autre site.

Avant d’utiliser la fonctionnalité avec un vrai client, effectuer une revue de
sécurité pour décider entre :

- conserver ces permissions tenant-wide;
- utiliser `Sites.Selected` avec une synchronisation différente;
- utiliser un autre mécanisme Microsoft officiellement compatible avec les
  webhooks, les deltas et les ACL nécessaires.

Cette décision de production ne doit pas être cachée dans le code.

<a id="m365-sharepoint-sources"></a>
## Sites, bibliothèques et listes autorisés

Découvrir un site ne signifie pas qu’il doit être indexé.

Chaque site possède un état :

- `Discovered` : trouvé mais pas encore autorisé;
- `Enabled` : autorisé et synchronisé;
- `Disabled` : désactivé volontairement;
- `Error` : configuration ou accès invalide.

Un nouveau site est ajouté comme `Discovered`. Il n’est pas indexé avant une
approbation dans AssistantCore.

Lorsqu’un site est activé :

1. lire ses bibliothèques;
2. lire ses listes avec Microsoft Graph;
3. enregistrer les bibliothèques et listes comme sources découvertes;
4. attendre qu’un administrateur active chaque source séparément;
5. créer une synchronisation initiale pour chaque source activée;
6. créer une souscription par bibliothèque ou liste lorsque cela est supporté.

Une liste n’est jamais activée automatiquement parce que son site est actif.
Cette règle évite d’indexer par erreur une liste contenant des données internes,
par exemple une liste de salaires ou de demandes disciplinaires.

### Découvrir les listes d’un site

Le client Microsoft Graph de `AssistantCore.ExternalServices` appelle :

```http
GET https://graph.microsoft.com/v1.0/sites/{siteId}/lists
```

Il suit chaque `@odata.nextLink` jusqu’à la dernière page. Les bibliothèques de
documents, les listes système et les listes supprimées ne sont pas proposées
comme listes de contenu. Chaque autre liste est enregistrée avec l’état
`Discovered`.

Exemple de source découverte :

```json
{
  "siteId": "contoso.sharepoint.com,site-123,web-456",
  "listId": "8f25d1d1-7e5c-4a3f-8d3e-4ed78a4a1720",
  "displayName": "Demandes d'achat",
  "webUrl": "https://contoso.sharepoint.com/sites/finance/Lists/Achats",
  "status": "Discovered",
  "isIndexed": false
}
```

### Endpoints administratifs

Ces endpoints sont réservés aux administrateurs de l’organisation connectée :

```http
GET /api/microsoft365/sites/{siteId}/lists
PATCH /api/microsoft365/sites/{siteId}/lists/{listId}
```

Le `GET` retourne les listes découvertes avec leur état. Le `PATCH` accepte
uniquement l’activation ou la désactivation de l’indexation :

```json
{
  "isIndexed": true
}
```

Le controller obtient l’organisation depuis l’identité authentifiée et jamais
depuis le corps ou l’URL. Le traitement suit obligatoirement
`Controller -> IDispatcher -> Handler -> service applicatif -> repository`.
Une activation est refusée si le site, la liste ou le connecteur n’appartient
pas à l’organisation courante.

Après une activation réussie, le backend crée une demande de synchronisation
initiale. Après une désactivation, il arrête la souscription, interdit les
nouvelles synchronisations et supprime de l’index tous les passages de cette
liste.

<a id="m365-sharepoint-content-types"></a>
## Types de contenus Microsoft 365

La première version distingue les sources suivantes :

| Source | Contenu indexé |
| --- | --- |
| Bibliothèque SharePoint | fichiers et dossiers |
| Pages SharePoint | titre, texte et contenu utile des composants |
| Listes SharePoint | titre de la liste, colonnes et valeurs des éléments |
| OneDrive professionnel | fichiers du OneDrive individuel autorisé |
| Archive | fichiers supportés contenus dans l’archive |

### Tous les fichiers sauf audio et vidéo

« Tous les fichiers sauf audio et vidéo » signifie que le pipeline accepte le
fichier et tente de déterminer comment en extraire une information utile.

Le résultat peut être :

- texte extrait directement;
- texte extrait avec OCR;
- données structurées transformées en texte lisible;
- contenu de fichiers extraits d’une archive;
- métadonnées seulement lorsqu’aucun contenu lisible n’existe.

Les fichiers audio et vidéo sont ignorés avant le téléchargement lorsque leur
type est connu.

Les fichiers exécutables, bibliothèques, images disque et autres binaires sans
contenu documentaire sont enregistrés comme `NoIndexableContent`.

### Formats Office

Le traitement doit couvrir :

- Word : DOC, DOCX et DOCM;
- Excel : XLS, XLSX et XLSM;
- PowerPoint : PPT, PPTX et PPTM.

Pour Excel, conserver les noms des feuilles et transformer les cellules utiles
en texte structuré. Les formules peuvent être indexées avec leur dernière
valeur enregistrée. Le worker ne doit pas recalculer un classeur.

Pour PowerPoint, conserver le numéro de diapositive, le titre, le texte et les
notes lorsque celles-ci sont disponibles.

Pour Word, conserver les titres, paragraphes, tableaux et numéros de page
lorsqu’ils peuvent être déterminés.

### Images et documents scannés

Les images et PDF scannés passent par un service OCR.

Le texte obtenu doit conserver autant que possible :

- le numéro de page;
- l’ordre de lecture;
- les titres;
- les tableaux;
- la langue détectée.

Un résultat OCR vide est enregistré comme `NoIndexableContent`.

<a id="m365-sharepoint-persistence"></a>
## Données conservées dans la base

La persistence doit représenter au minimum :

### Connexion Microsoft 365

- organisation interne;
- identifiant du tenant Microsoft;
- état du consentement;
- date de dernière validation;
- état général du connecteur.

Les états persistés par le socle d’ingestion sont :

| Élément | États | Signification |
| --- | --- | --- |
| Connexion | `PendingConsent`, `Active`, `Error`, `Revoked` | Consentement en cours, connexion utilisable, erreur contrôlée ou accès révoqué. |
| Source | `Discovered`, `Enabled`, `Disabled`, `Error` | Source trouvée, autorisée, désactivée ou invalide. |
| Souscription | `Pending`, `Active`, `RenewalRequired`, `Error`, `Revoked`, `Expired` | Cycle de vie d’un webhook Microsoft Graph. |
| Synchronisation | `Pending`, `Running`, `Succeeded`, `TemporaryFailure`, `PermanentFailure`, `Cancelled` | Cycle de vie d’une exécution d’ingestion. |

Une seule connexion Microsoft 365 existe par organisation. Un tenant Microsoft
ne peut être associé qu’à une seule organisation AssistantCore. Le démarrage du
consentement conserve uniquement l’empreinte du `state`; sa valeur signée et
expirante revient dans le callback et ne peut être consommée qu’une fois.

### Site SharePoint

- identifiant Microsoft du site;
- URL;
- nom affiché;
- état;
- date de découverte;
- date d’activation.

### Bibliothèque

- identifiant du `drive`;
- site parent;
- nom;
- état;
- `deltaLink`;
- dernière synchronisation réussie;
- prochaine vérification prévue;
- dernière erreur.

### Liste SharePoint

- organisation et connecteur parents;
- identifiant Microsoft du site;
- identifiant Microsoft de la liste;
- nom affiché et URL;
- état `Discovered`, `Enabled`, `Disabled` ou `Error`;
- indicateur d’indexation;
- empreinte du schéma de colonnes;
- `deltaLink` opaque retourné par Microsoft Graph;
- dernière synchronisation réussie;
- prochaine réconciliation prévue;
- dernière erreur.

La combinaison `organisation + connecteur + siteId + listId` est unique.

### Élément de liste

- organisation, site et liste parents;
- `listItemId` Microsoft;
- titre calculé;
- URL;
- `eTag` ou version;
- dates de création et de modification;
- empreinte des permissions;
- nombre de passages indexés;
- état de traitement et dernière erreur.

La combinaison `organisation + siteId + listId + listItemId` est unique. Elle
sert aussi de base à la clé déterministe des passages Azure AI Search.

### Souscription

- identifiant de souscription Microsoft;
- bibliothèque ou liste surveillée;
- date d’expiration;
- `clientState` protégé;
- dernier renouvellement;
- état.

### Document

- organisation;
- site;
- bibliothèque;
- identifiant Microsoft du fichier;
- nom;
- URL;
- type de fichier;
- `eTag` ou version;
- date de modification;
- nombre de passages indexés;
- état de traitement;
- dernière erreur;
- dernière date d’indexation.

Les états de traitement possibles doivent distinguer :

- en attente;
- en traitement;
- indexé;
- ignoré;
- en erreur temporaire;
- en erreur permanente;
- supprimé.

<a id="m365-sharepoint-webhooks"></a>
## Création des webhooks

Après l’activation d’une bibliothèque, le gestionnaire de souscriptions appelle
Microsoft Graph pour surveiller :

```text
/drives/{driveId}/root
```

Après l’activation d’une liste, il surveille :

```text
/sites/{siteId}/lists/{listId}
```

La souscription contient :

- l’URL HTTPS publique du webhook;
- la ressource surveillée;
- sa date d’expiration;
- un `clientState` aléatoire et secret.

Pour recevoir aussi les événements de sécurité pris en charge, la création
utilise l’en-tête :

```http
Prefer: includesecuritywebhooks
```

### Validation initiale

Microsoft appelle :

```http
POST /webhooks/microsoft-graph?validationToken=<token>
```

L’endpoint retourne le token décodé :

```http
HTTP 200 OK
Content-Type: text/plain

<token>
```

Cette réponse doit être envoyée sans lancer de traitement long.

### Notification normale

Pour chaque notification :

1. retrouver la souscription avec `subscriptionId`;
2. vérifier que son tenant correspond;
3. comparer le `clientState`;
4. ignorer les notifications invalides;
5. créer une demande `SynchronizeDrive` ou `SynchronizeList` selon la source;
6. retourner `202 Accepted`.

Le webhook ne fait jamais confiance à un `organizationId` fourni dans la
notification. Il retrouve l’organisation depuis la souscription enregistrée.

<a id="m365-sharepoint-webhook-renewal"></a>
## Renouvellement des webhooks

Les souscriptions Microsoft Graph expirent.

Un traitement planifié du worker :

1. recherche les souscriptions qui approchent de leur expiration;
2. tente de les renouveler;
3. enregistre la nouvelle expiration;
4. recrée une souscription absente ou invalide;
5. demande une synchronisation delta après une interruption.

Le renouvellement doit avoir lieu plusieurs jours avant l’expiration. Une
alerte doit être produite si une souscription reste impossible à renouveler.

<a id="m365-sharepoint-initial-sync"></a>
## Synchronisation initiale

Lors de la première synchronisation d’une bibliothèque :

1. appeler la requête delta sans ancien token;
2. lire toutes les pages `@odata.nextLink`;
3. enregistrer chaque fichier supporté comme travail à traiter;
4. ignorer les dossiers comme documents, mais continuer leur parcours;
5. enregistrer les suppressions reçues;
6. conserver le `@odata.deltaLink` final seulement lorsque les changements ont
   été enregistrés durablement.

La synchronisation initiale doit pouvoir être relancée sans créer de doublons.

Lors de la première synchronisation d’une liste :

1. charger ses colonnes avec
   `GET /sites/{siteId}/lists/{listId}/columns`;
2. construire la sélection des colonnes indexables;
3. appeler
   `GET /sites/{siteId}/lists/{listId}/items/delta?$expand=fields`;
4. suivre chaque `@odata.nextLink` sans charger toute la liste en mémoire;
5. créer un travail `ProcessListItem` pour chaque élément créé ou modifié;
6. créer un travail `DeleteListItem` pour chaque élément portant la facette
   `deleted`;
7. enregistrer le `@odata.deltaLink` seulement lorsque toutes les pages et tous
   les travaux ont été enregistrés durablement.

Exemple : si la deuxième page Graph échoue, le nouveau `deltaLink` n’est pas
enregistré. La prochaine exécution reprend depuis le dernier point confirmé et
les clés déterministes empêchent les doublons.

<a id="m365-sharepoint-delta-sync"></a>
## Synchronisation des changements

Après la synchronisation initiale, le worker réutilise le `deltaLink`.

Le worker doit gérer :

- un fichier créé;
- un fichier modifié;
- un fichier renommé;
- un fichier déplacé;
- un fichier supprimé;
- plusieurs apparitions du même fichier;
- plusieurs pages de résultats;
- un token delta expiré;
- une demande Microsoft de recommencer une synchronisation complète.

Le webhook sert à démarrer rapidement cette synchronisation. Une tâche
planifiée exécute aussi périodiquement le delta afin de récupérer un changement
dont la notification aurait été perdue.

Une seule synchronisation d’une bibliothèque peut s’exécuter à la fois.
La même règle s’applique à une liste : une seule synchronisation peut modifier
son checkpoint à la fois.

Pour une liste, le webhook ne contient pas toutes les nouvelles valeurs. Il
réveille uniquement la synchronisation delta. Une réconciliation planifiée
relance aussi le delta afin de couvrir une notification perdue.

<a id="m365-sharepoint-worker"></a>
## Traitement par le worker

Le projet `AssistantCore.Ingestion.Worker` possède son propre démarrage et sa
propre injection de dépendances. Le socle du ticket 45 permet de vérifier une
connexion fournie par son identifiant interne au démarrage. Seule une connexion
`Active` est acceptée; les états `PendingConsent`, `Error` et `Revoked` sont
refusés avant tout appel à Microsoft.

Cette vérification est volontairement minimale. Elle confirme que le processus
séparé démarre, accède à la persistence et applique l'état de la connexion. La
consommation des files Service Bus et la synchronisation réelle sont ajoutées
par les tickets suivants.

Utiliser au minimum deux files de travail :

```text
sharepoint-drive-sync
sharepoint-document-process
sharepoint-list-sync
sharepoint-list-item-process
```

La première demande de découvrir les changements d’une bibliothèque.

La seconde demande de traiter un document précis.

La troisième demande de lire les changements d’une liste. La quatrième demande
de transformer ou supprimer un élément de liste précis.

Chaque message doit être petit. Il contient des identifiants et jamais le texte
complet du document.

Le worker doit être idempotent :

- recevoir deux fois le même message ne crée pas de doublon;
- retraiter la même version produit les mêmes clés;
- une ancienne version ne remplace pas une version plus récente;
- une suppression répétée reste sans effet secondaire.

Les messages qui échouent plusieurs fois sont placés dans une file d’erreurs
afin de pouvoir être examinés.

<a id="m365-sharepoint-extraction"></a>
## Téléchargement et extraction

Pour chaque fichier :

1. vérifier que sa version n’est pas déjà indexée;
2. vérifier son extension et sa taille;
3. lire ses permissions;
4. refuser l’indexation si les permissions ne sont pas comprises;
5. télécharger le contenu en flux;
6. extraire le texte;
7. supprimer les données temporaires;
8. transmettre le texte au service de découpage.

Le contenu complet ne doit jamais apparaître dans les logs.

### Formats initiaux

| Format | Traitement |
| --- | --- |
| TXT | lire le texte avec un encodage contrôlé |
| DOCX | extraire les paragraphes dans leur ordre |
| PDF texte | extraire le texte page par page |

Un PDF sans texte lisible est marqué `Unsupported` dans la première version.

La taille maximale d’un fichier vient de la configuration. Un fichier trop
grand est ignoré avec une raison explicite.

<a id="m365-sharepoint-archives"></a>
## Traitement des archives

La première version traite ZIP et GZ. D’autres formats peuvent être ajoutés
derrière la même abstraction.

Avant d’extraire une archive, vérifier :

- la taille du fichier compressé;
- la taille totale annoncée après décompression;
- le nombre de fichiers;
- la profondeur des archives imbriquées;
- les chemins des fichiers;
- la présence d’un mot de passe;
- les extensions audio et vidéo à exclure.

Le worker doit bloquer :

- les chemins qui sortent du dossier temporaire;
- les archives récursives sans limite;
- les archives dont la taille décompressée dépasse la limite;
- les archives protégées par mot de passe;
- les archives corrompues.

Chaque fichier contenu produit ses propres passages, mais conserve :

- l’identifiant du document SharePoint parent;
- le nom de l’archive;
- son chemin à l’intérieur de l’archive;
- l’URL SharePoint de l’archive.

Les fichiers contenus héritent des permissions de l’archive. Ils ne possèdent
jamais une ACL plus large que leur document parent.

<a id="m365-sharepoint-pages-lists"></a>
## Traitement des pages et listes

### Pages SharePoint

Pour chaque page moderne :

1. lire son identifiant, titre, URL et date de modification;
2. lire les composants de contenu;
3. extraire uniquement le texte visible et utile;
4. ignorer les scripts et informations de mise en page;
5. découper le texte en passages;
6. conserver les permissions de la page;
7. supprimer les passages lorsque la page est supprimée.

### Listes SharePoint

Chaque élément de liste est un document logique distinct.

Le traitement d’un élément commence uniquement si sa liste est `Enabled` et
`isIndexed = true`. Il reçoit du synchroniseur `siteId`, `listId`,
`listItemId`, `eTag`, `webUrl`, les dates et le dictionnaire `fields`.

#### 1. Charger et filtrer le schéma

Le worker charge le schéma avec :

```http
GET https://graph.microsoft.com/v1.0/sites/{siteId}/lists/{listId}/columns
```

Il suit `@odata.nextLink`, puis conserve le nom interne, le nom affiché et le
type de chaque colonne. Une colonne est exclue du texte lorsque :

- elle est masquée;
- elle appartient à la liste configurable des colonnes interdites;
- elle contient uniquement une donnée technique déjà conservée comme
  métadonnée;
- son type n’est pas supporté et aucune représentation sûre n’existe.

Une colonne inconnue est ignorée avec un diagnostic contenant seulement son
nom et son type. Sa valeur ne doit pas apparaître dans les logs.

#### 2. Convertir les valeurs

| Type SharePoint | Texte produit | Exemple |
| --- | --- | --- |
| texte | valeur normalisée | `Description : Écran 27 pouces` |
| nombre ou devise | nombre avec le nom de colonne | `Montant : 1250 CAD` |
| date | date ISO 8601 en UTC | `Échéance : 2026-09-01T14:00:00Z` |
| booléen | `Oui` ou `Non` | `Urgent : Oui` |
| choix | libellés séparés par une virgule | `Statut : Approuvé, Commandé` |
| personne | nom affiché résolu | `Demandeur : Alice Tremblay` |
| lien | libellé puis URL | `Bon de commande : BC-1042 — https://...` |
| lookup | valeur affichée, jamais l’identifiant seul | `Département : Finance` |

Un identifiant de personne ou de lookup n’est jamais présenté comme s’il était
un identifiant Entra. Lorsque la valeur lisible ne peut pas être résolue, la
colonne est omise et le traitement continue.

#### 3. Construire le document logique

Exemple de valeurs reçues :

```json
{
  "Title": "Ordinateur portable",
  "Status": "Approved",
  "Amount": 1250,
  "Urgent": true
}
```

Exemple de texte envoyé au découpage :

```text
Liste : Demandes d’achat
Élément : Ordinateur portable
Statut : Approuvé
Montant : 1250 CAD
Urgent : Oui
URL : https://contoso.sharepoint.com/sites/finance/Lists/Achats/42
```

Le titre utilise la colonne `Title` lorsqu’elle contient une valeur. Sinon, il
devient `Élément {listItemId} de {nom de la liste}`.

Le document logique transmis au pipeline contient au minimum :

```json
{
  "organizationId": "organisation-interne",
  "contentType": "listItem",
  "siteId": "site-123",
  "listId": "list-456",
  "listItemId": "42",
  "documentVersion": "etag-7",
  "title": "Ordinateur portable",
  "url": "https://contoso.sharepoint.com/sites/finance/Lists/Achats/42",
  "modifiedAt": "2026-08-17T18:30:00Z",
  "content": "Liste : Demandes d’achat\nÉlément : Ordinateur portable\n..."
}
```

#### 4. Vérifier les permissions avant l’indexation

Le worker appelle le résolveur de permissions livré par la fonctionnalité de
sécurité Microsoft 365. Il ne lit pas lui-même les membres ou les groupes.

Le résultat doit être soit une ACL normalisée, soit `Unresolved`. Dans le
second cas, aucun passage, titre ou embedding de l’élément n’est envoyé à Azure
AI Search. Une ACL vide ne signifie jamais « accessible à tous ».

L’API Microsoft Graph v1.0 est utilisée pour les listes, colonnes, valeurs,
deltas et notifications. Les endpoints Graph `/beta` de permissions des
éléments de liste ne sont pas utilisés en production. Le résolveur de
permissions utilise l’API REST SharePoint derrière un client situé dans
`AssistantCore.ExternalServices` :

```http
GET {siteUrl}/_api/web/lists(guid'{listId}')/items({itemId})
    ?$select=HasUniqueRoleAssignments
```

Si l’élément possède des permissions uniques, le client charge ses
`RoleAssignments` et leurs `RoleDefinitionBindings`. Sinon, il remonte vers les
permissions de la liste puis du site. Une erreur, un principal inconnu ou une
réponse partielle produit `Unresolved`.

#### 5. Mettre à jour ou supprimer

- création : produire les passages et utiliser `mergeOrUpload`;
- modification : comparer le `eTag`, reconstruire les passages et supprimer
  ceux qui n’existent plus;
- suppression : supprimer tous les passages correspondant à
  `organizationId + siteId + listId + listItemId`;
- changement de schéma : recalculer l’empreinte des colonnes et reprogrammer
  les éléments affectés;
- changement de permissions : mettre à jour les ACL avant de rendre le nouveau
  contenu recherchable.

#### 6. Limites initiales

Les limites sont configurables et validées au démarrage :

- 100 colonnes indexables par liste;
- 20 000 caractères par valeur;
- 100 000 caractères produits par élément avant découpage;
- 5 000 éléments traités par exécution du worker;
- durée maximale et `CancellationToken` appliqués à chaque appel externe.

Un dépassement n’arrête pas toute la liste. L’élément reçoit un état explicite
comme `ContentTooLarge` ou `TooManyColumns`, sans enregistrer son contenu dans
les logs.

<a id="m365-sharepoint-onedrive"></a>
## Traitement de OneDrive

Dans cette documentation, « OneDrive personnel » désigne le OneDrive
professionnel individuel d’un utilisateur du tenant Microsoft 365.

L’administrateur choisit les OneDrive qui peuvent être indexés. Le fait qu’un
utilisateur appartienne au tenant ne suffit pas pour indexer automatiquement
son contenu.

Chaque OneDrive autorisé possède :

- l’identifiant de son propriétaire;
- son `driveId`;
- son état d’activation;
- son `deltaLink`;
- sa souscription;
- sa dernière synchronisation.

Le même pipeline de fichiers, archives, OCR, chunks, embeddings et permissions
est utilisé pour SharePoint et OneDrive.

Le OneDrive grand public d’un compte Outlook.com ou Hotmail est hors périmètre.
Microsoft exige une connexion déléguée propre à chaque utilisateur et ne
supporte pas les souscriptions applicatives en arrière-plan pour ce scénario.

<a id="m365-sharepoint-chunking"></a>
## Découpage en passages

Un document ne doit pas être envoyé en un seul bloc.

Configuration initiale proposée :

- environ 800 tokens par passage;
- chevauchement d’environ 100 tokens;
- ne pas couper au milieu d’un paragraphe lorsque cela est évitable;
- conserver le titre du document;
- conserver le numéro de page lorsqu’il est connu;
- ignorer les passages vides.

Ces valeurs restent configurables.

Chaque passage possède une clé déterministe construite à partir de :

```text
organisation + site + bibliothèque + document + numéro du passage
```

<a id="m365-sharepoint-embeddings"></a>
## Création des embeddings

Le fournisseur d’embeddings est accessible par une interface applicative.

Le worker lui transmet seulement :

- le titre utile;
- le texte du passage.

La configuration indique :

- le fournisseur;
- le modèle;
- le nombre de dimensions;
- la taille maximale d’une demande;
- le nombre maximal de tentatives;
- le délai maximal d’attente.

Le nombre de dimensions du champ Azure AI Search doit correspondre exactement
au modèle utilisé.

Changer de modèle ou de dimensions demande la création d’un nouvel index ou
une réindexation complète contrôlée.

<a id="m365-sharepoint-search-index"></a>
## Structure de l’index Azure AI Search

La première version utilise un index partagé par environnement. Chaque passage
porte obligatoirement l’identifiant interne de son organisation.

Champs proposés :

| Champ | Utilité |
| --- | --- |
| `chunkId` | clé unique du passage |
| `organizationId` | isolation de l’organisation |
| `sourceType` | valeur `sharepoint` |
| `contentType` | fichier, page, élément de liste ou archive |
| `siteId` | site SharePoint source |
| `driveId` | bibliothèque source |
| `documentId` | identifiant Microsoft du fichier |
| `listId` | liste SharePoint lorsque nécessaire |
| `listItemId` | élément de liste lorsque nécessaire |
| `oneDriveOwnerId` | propriétaire du OneDrive lorsque nécessaire |
| `archivePath` | chemin du fichier dans une archive |
| `documentVersion` | version ou `eTag` |
| `chunkNumber` | position du passage |
| `title` | titre du document |
| `content` | texte du passage |
| `url` | lien SharePoint |
| `fileType` | type du fichier |
| `pageNumber` | page lorsque disponible |
| `modifiedAt` | date de modification SharePoint |
| `indexedAt` | date d’indexation |
| `allowedUserIds` | utilisateurs Entra autorisés |
| `allowedGroupIds` | groupes Entra autorisés |
| `allowedSharePointGroupIds` | groupes SharePoint autorisés |
| `hasAnonymousLink` | présence d’un lien anonyme |
| `contentVector` | embedding du passage |

`title` et `content` sont recherchables.

`organizationId`, `sourceType`, `siteId`, `driveId`, `documentId`,
`allowedUserIds` et `allowedGroupIds` sont filtrables.

Les champs de permissions ne sont pas retournés dans les résultats normaux.

`contentVector` utilise un profil vectoriel compatible avec le modèle
d’embeddings.

<a id="m365-sharepoint-index-updates"></a>
## Mise à jour et suppression

### Nouveau document

Tous les passages sont ajoutés avec `mergeOrUpload`.

### Document modifié

1. traiter la nouvelle version;
2. remplacer les passages qui gardent la même clé;
3. supprimer les anciens passages devenus inutiles;
4. enregistrer le nouveau nombre de passages.

### Document supprimé

Supprimer tous ses `chunkId` de l’index et marquer le document supprimé dans la
base.

### Changement de permissions

Mettre à jour les champs de permissions de tous les passages. Une révocation
d’accès doit être traitée en priorité.

<a id="m365-sharepoint-document-security"></a>
## Respect des permissions

Chaque recherche combine obligatoirement :

1. l’organisation interne;
2. l’identifiant Entra de l’utilisateur;
3. les groupes Entra auxquels il appartient.

Exemple conceptuel :

```text
organizationId correspond à l’organisation courante
ET
(
  allowedUserIds contient l’utilisateur
  OU
  allowedGroupIds contient un de ses groupes
)
```

Ces filtres sont construits par le backend. Ils ne sont jamais fournis par le
frontend ou par le modèle d’intelligence artificielle.

Si les permissions du document sont absentes ou incompréhensibles, le document
est invisible.

Si les groupes du membre ne peuvent pas être obtenus, la recherche Microsoft
365 échoue de manière contrôlée. Elle ne retire jamais le filtre.

Une tâche planifiée vérifie périodiquement les permissions afin de couvrir les
changements hérités qui pourraient ne pas produire une notification fiable.

### Groupes SharePoint

Un groupe SharePoint reçoit un identifiant stable composé du site et de
l’identifiant du groupe :

```text
spg:<siteId>:<sharePointGroupId>
```

Le système conserve les groupes SharePoint autorisés sur chaque contenu.

Lors d’une recherche, il détermine les groupes SharePoint auxquels appartient
le membre pour le site concerné. Cette résolution doit être mise en cache
pendant une courte durée, mais une panne du cache ou de SharePoint ferme
l’accès.

Les modifications de membres d’un groupe SharePoint doivent être détectées par
une réconciliation planifiée et testées séparément.

### Utilisateurs invités externes

Un invité doit avoir un objet utilisateur invité dans le tenant Microsoft
Entra du client. L’identifiant utilisé est l’`oid` de cet objet invité dans le
tenant du client.

Pour utiliser AssistantCore, l’invité doit aussi :

- être affecté explicitement à `AssistantCore.Access`;
- appartenir à l’organisation interne correspondante;
- posséder un membre AssistantCore actif;
- réussir les mêmes contrôles que les utilisateurs internes.

Une adresse courriel externe seule ne constitue jamais une autorisation.

Une invitation SharePoint non encore acceptée ne donne pas accès au document
dans AssistantCore.

### Liens « Toute personne disposant du lien »

Un lien anonyme est une autorisation fondée sur la possession du lien. Il ne
doit pas être transformé en permission générale de découverte.

Le document est indexé avec `hasAnonymousLink=true`, mais ce champ ne permet
pas à lui seul de le retourner dans une recherche normale.

Le document devient accessible dans AssistantCore seulement si :

- le membre possède aussi une permission utilisateur ou groupe normale; ou
- il fournit le lien de partage exact et le backend le valide auprès de
  Microsoft avant de retourner le contenu.

Le lien complet, qui peut contenir un secret d’accès, ne doit jamais être
stocké dans les logs, retourné au modèle ou exposé comme métadonnée de
recherche.

<a id="m365-sharepoint-search-connector"></a>
## Connecteur de recherche Microsoft 365

Le registre contient déjà la définition logique de
`search_microsoft_365`, mais son exécution complète doit être ajoutée.

Le connecteur :

1. reçoit une question de recherche validée;
2. obtient l’organisation et l’utilisateur courants;
3. obtient les groupes autorisés;
4. crée l’embedding de la question;
5. effectue une recherche hybride textuelle et vectorielle;
6. ajoute les filtres d’organisation et de permissions;
7. limite le nombre de résultats;
8. normalise les preuves;
9. retourne le titre, le passage, l’URL et la provenance;
10. ne retourne jamais les ACL au modèle.

Une recherche sans résultat retourne une collection vide. Une panne du
connecteur retourne un échec contrôlé pouvant devenir un avertissement dans la
réponse de l’assistant.

<a id="m365-sharepoint-errors"></a>
## Gestion des erreurs

### Erreurs temporaires

Réessayer avec une attente progressive :

- limitation Microsoft Graph `429`;
- erreur Microsoft `5xx`;
- délai d’attente réseau;
- erreur temporaire d’embedding;
- erreur temporaire Azure AI Search;
- indisponibilité Azure Service Bus.

Respecter `Retry-After` lorsqu’il est fourni.

### Erreurs permanentes

Ne pas réessayer indéfiniment :

- format non supporté;
- document chiffré;
- permissions impossibles à représenter;
- site retiré;
- consentement supprimé;
- configuration invalide.

### Informations de suivi

Les logs peuvent contenir :

- organisation interne;
- site, bibliothèque, liste et contenu techniques;
- étape du traitement;
- durée;
- code d’erreur;
- numéro de tentative.

Ils ne contiennent jamais :

- token Microsoft;
- secret;
- `clientState`;
- texte complet du document;
- embedding complet;
- contenu de la question utilisateur.

<a id="m365-sharepoint-configuration"></a>
## Configuration et secrets

Configuration non secrète attendue :

```json
{
  "Microsoft365": {
    "ClientId": "<application-multitenant>",
    "AuthorityBaseUrl": "https://login.microsoftonline.com",
    "GraphBaseUrl": "https://graph.microsoft.com",
    "ConsentCallbackUrl": "https://<api>/api/microsoft365/consent/callback",
    "ConsentStateLifetimeMinutes": 10,
    "WebhookBaseUrl": "https://<url-publique>",
    "SynchronizationIntervalMinutes": 15,
    "PermissionReconciliationIntervalHours": 6,
    "MaximumFileSizeBytes": 20971520,
    "MaximumListColumns": 100,
    "MaximumListFieldCharacters": 20000,
    "MaximumListItemCharacters": 100000,
    "MaximumListItemsPerRun": 5000
  },
  "ServiceBus": {
    "FullyQualifiedNamespace": "<namespace>.servicebus.windows.net",
    "DriveSyncQueue": "sharepoint-drive-sync",
    "DocumentProcessQueue": "sharepoint-document-process",
    "ListSyncQueue": "sharepoint-list-sync",
    "ListItemProcessQueue": "sharepoint-list-item-process"
  },
  "AzureSearch": {
    "Endpoint": "https://<service>.search.windows.net",
    "IndexName": "microsoft-content-dev"
  },
  "Embeddings": {
    "Provider": "<provider>",
    "Model": "<model>",
    "Dimensions": 1536,
    "ChunkTokenLimit": 800,
    "ChunkTokenOverlap": 100
  }
}
```

Le démarrage refuse une URL non HTTPS, un `ClientId` vide, un secret absent ou
une durée de `state` hors de la plage de 1 à 60 minutes. Le secret reste absent
des fichiers versionnés et provient de `user-secrets` en local.

La valeur `Dimensions` est un exemple et doit correspondre au modèle
réellement choisi.

En local, les secrets utilisent `dotnet user-secrets`.

Le secret de l’App Registration Microsoft 365 est configuré sans être ajouté à
`appsettings.json` :

```bash
dotnet user-secrets --project AssistantCore.Service set "Microsoft365:ClientSecret" "<secret>"
```

Dans Azure :

- utiliser les identités managées pour Service Bus et Azure AI Search;
- utiliser un certificat ou une identité fédérée pour Microsoft Graph;
- utiliser Key Vault seulement lorsqu’un secret reste nécessaire;
- ne jamais placer un secret dans Git ou dans `appsettings.json`.

<a id="m365-sharepoint-local-development"></a>
## Développement local

Le développeur a besoin :

- du tenant Microsoft 365 fictif;
- d’un site SharePoint de test;
- du service Azure AI Search de développement;
- du fournisseur d’embeddings configuré;
- d’Azure Service Bus ou de son émulateur;
- de ngrok ou d’un tunnel HTTPS équivalent.

Ordre de démarrage :

1. démarrer SQL Server;
2. démarrer Service Bus ou son émulateur;
3. démarrer AssistantCore.Service;
4. démarrer l’hôte de webhooks;
5. démarrer le worker;
6. ouvrir le tunnel HTTPS vers le port du webhook;
7. configurer l’URL publique;
8. créer ou recréer les souscriptions;
9. vérifier la validation Microsoft;
10. ajouter un fichier de test.

Une URL de tunnel modifiée demande de mettre à jour ou recréer les
souscriptions concernées.

<a id="m365-sharepoint-end-to-end-test"></a>
## Test complet avec le tenant fictif

### Préparation Microsoft 365

Créer dans le tenant fictif :

- un utilisateur `Alice`;
- un utilisateur `Bob`;
- un groupe Entra `EmployesTest` contenant Alice et Bob;
- un groupe Entra `FinanceTest` contenant seulement Alice;
- un site SharePoint `AssistantCore Test`;
- une bibliothèque `Documents`.

Ajouter :

- `guide-general.txt`, accessible à `EmployesTest`;
- `manuel-employe.pdf`, accessible à `EmployesTest`;
- `budget-confidentiel.docx`, accessible seulement à `FinanceTest`.

Ne jamais utiliser de données réelles dans ce test.

### Test 1 — Connexion

1. connecter le tenant fictif;
2. vérifier le consentement;
3. découvrir le site;
4. activer le site;
5. découvrir la bibliothèque;
6. vérifier que la souscription existe.

### Test 2 — Synchronisation initiale

1. lancer la synchronisation;
2. attendre la fin du worker;
3. vérifier que chaque document supporté possède des passages;
4. vérifier que chaque passage possède un vecteur;
5. vérifier que les URL SharePoint sont correctes;
6. relancer la synchronisation;
7. vérifier qu’aucun doublon n’est créé.

### Test 3 — Recherche

1. rechercher un mot exact;
2. rechercher la même idée avec des mots différents;
3. vérifier que la recherche hybride retrouve le bon document;
4. poser une question avec `POST /api/messages`;
5. vérifier que la réponse cite le document.

### Test 4 — Permissions

1. se connecter comme Alice;
2. vérifier qu’Alice trouve le budget;
3. se connecter comme Bob;
4. vérifier que Bob ne trouve jamais le budget;
5. rechercher comme Bob le titre exact du budget;
6. vérifier que le document reste absent des résultats, preuves et sources.

### Test 5 — Nouveau fichier

1. ajouter un fichier dans SharePoint;
2. vérifier que Microsoft appelle le webhook;
3. vérifier qu’un message est placé en file;
4. vérifier que le worker exécute le delta;
5. vérifier que le document devient recherchable.

### Test 6 — Modification

1. modifier le contenu d’un fichier;
2. attendre la synchronisation;
3. vérifier que le nouveau texte est trouvé;
4. vérifier que l’ancien texte a disparu;
5. vérifier qu’aucun ancien passage inutile ne reste.

### Test 7 — Suppression

1. supprimer un fichier;
2. attendre la synchronisation;
3. vérifier que tous ses passages disparaissent;
4. vérifier qu’il ne peut plus être cité.

### Test 8 — Changement de permissions

1. donner à Bob l’accès au budget;
2. attendre la synchronisation des permissions;
3. vérifier que Bob trouve le budget;
4. retirer ensuite son accès;
5. attendre la synchronisation;
6. vérifier que Bob ne trouve plus le budget.

### Test 9 — Webhook indisponible

1. arrêter l’hôte de webhooks;
2. modifier un fichier;
3. redémarrer l’hôte;
4. exécuter la réconciliation planifiée;
5. vérifier que la modification est tout de même indexée.

### Test 10 — Isolation

1. créer une deuxième organisation interne de test;
2. ajouter un passage fictif pour cette organisation;
3. rechercher depuis le tenant Microsoft 365 fictif principal;
4. vérifier que le passage de l’autre organisation n’est jamais retourné.

### Test 11 — Documents Office

1. ajouter un Word, un Excel et un PowerPoint;
2. vérifier que le texte et la structure utile sont extraits;
3. vérifier que chaque fichier est recherchable;
4. vérifier les numéros de feuilles, pages ou diapositives disponibles.

### Test 12 — Autres formats

1. tester TXT, CSV, JSON, XML, HTML, Markdown et OpenDocument;
2. vérifier chaque extracteur;
3. vérifier qu’un fichier binaire sans texte ne bloque pas le worker;
4. vérifier que les fichiers audio et vidéo sont ignorés.

### Test 13 — OCR

1. ajouter une image contenant du texte;
2. ajouter un PDF scanné;
3. vérifier le texte OCR;
4. vérifier la recherche textuelle et vectorielle;
5. vérifier le comportement d’un résultat OCR vide.

### Test 14 — Archives

1. ajouter une archive contenant plusieurs formats;
2. vérifier chaque fichier extrait;
3. tester une archive imbriquée;
4. tester une archive corrompue;
5. tester une archive trop volumineuse;
6. tester un chemin d’extraction dangereux;
7. vérifier l’héritage des permissions.

### Test 15 — Pages SharePoint

1. créer une page moderne;
2. vérifier son indexation;
3. modifier son texte;
4. modifier ses permissions;
5. supprimer la page;
6. vérifier chaque changement dans Azure AI Search.

### Test 16 — Listes SharePoint

1. créer `Demandes d’achat` avec les colonnes `Title` (texte), `Amount`
   (nombre), `DueDate` (date), `Urgent` (booléen), `Status` (choix),
   `Requester` (personne) et `PurchaseOrder` (lien);
2. vérifier que la découverte enregistre la liste comme `Discovered` sans
   indexer ses éléments;
3. appeler l’endpoint d’activation et vérifier la création de la souscription
   et de la première synchronisation;
4. ajouter l’élément `Ordinateur portable`, puis vérifier que son texte contient
   les noms affichés et les valeurs lisibles;
5. ajouter une colonne masquée `InternalApprovalCode` et vérifier que son nom et
   sa valeur sont absents de l’index et des logs;
6. modifier `Amount` et `Status`, exécuter le delta et vérifier que l’ancien
   contenu n’est plus recherchable;
7. donner une permission unique à Alice, puis vérifier qu’Alice trouve
   l’élément et que Bob ne le trouve pas même avec le titre exact;
8. provoquer une erreur du résolveur de permissions et vérifier que l’élément
   devient invisible au lieu d’être rendu public;
9. supprimer l’élément et vérifier que tous ses `chunkId` disparaissent;
10. désactiver la liste et vérifier l’arrêt de la souscription et la suppression
    de tous les passages de cette liste.

### Test 17 — OneDrive professionnel

1. autoriser le OneDrive d’Alice;
2. ajouter, modifier et supprimer un fichier;
3. vérifier les webhooks et le delta;
4. vérifier que le OneDrive non autorisé de Bob n’est pas indexé.

### Test 18 — Invité externe

1. inviter un utilisateur externe dans le tenant fictif;
2. lui attribuer `AssistantCore.Access`;
3. partager un document avec cet invité;
4. vérifier qu’il trouve le document;
5. retirer le partage;
6. vérifier qu’il ne trouve plus le document.

### Test 19 — Lien anonyme

1. créer un lien « Toute personne »;
2. vérifier que le document n’est pas découvrable par son titre;
3. fournir le lien exact;
4. vérifier que le backend valide le lien;
5. révoquer le lien;
6. vérifier que l’accès disparaît.

### Test 20 — Groupe SharePoint

1. créer un groupe SharePoint sans groupe Entra correspondant;
2. y ajouter Alice;
3. lui accorder un document;
4. vérifier qu’Alice trouve le document et que Bob ne le trouve pas;
5. remplacer Alice par Bob;
6. vérifier que les accès sont inversés après réconciliation.

<a id="m365-sharepoint-test-tickets"></a>
## Stratégie des tickets et des tests

Chaque capacité possède un seul ticket d'implémentation. Ce ticket contient
directement :

1. les étapes techniques ordonnées;
2. les tests unitaires, d'intégration et d'architecture propres à la capacité;
3. les cas normaux, limites, erreurs et contrôles de sécurité;
4. les critères d'acceptation nécessaires pour considérer l'implémentation
   terminée.

Une capacité ne doit pas être considérée terminée avant la réussite de ses
tests. Un second ticket qui répète uniquement ces contrôles n'est pas créé.

Un seul ticket final couvre la validation manuelle de bout en bout dans le
tenant Microsoft 365 fictif. Il vérifie les relations entre plusieurs
capacités qui ne peuvent pas être prouvées isolément : consentement, webhook,
delta, extraction, permissions, isolation, recherche et réponse citée de
`/api/messages`.

Cette validation finale ne remplace pas les tests de chaque ticket
d'implémentation et ne sert pas à reporter leur définition de terminé.

<a id="m365-sharepoint-implementation-order"></a>
## Ordre d’implémentation

Implémenter dans cet ordre :

1. modèle de persistence Microsoft 365;
2. clients externes Microsoft Graph;
3. connexion et découverte des sites;
4. Azure Service Bus;
5. hôte public de webhooks;
6. gestion des souscriptions;
7. worker et synchronisation delta;
8. téléchargement et extraction;
9. découpage en passages;
10. fournisseur d’embeddings;
11. écriture Azure AI Search;
12. synchronisation des permissions;
13. connecteur `search_microsoft_365`;
14. intégration avec `/api/messages`;
15. tests automatisés;
16. test manuel complet dans le tenant fictif.

Chaque étape doit être utilisable ou testable avant de commencer la suivante.

<a id="m365-sharepoint-definition-of-done"></a>
## Définition de terminé

La fonctionnalité est terminée seulement si :

- le tenant fictif peut être connecté;
- le site pilote peut être activé;
- les souscriptions sont créées et renouvelées;
- le worker fonctionne séparément de l’API;
- les formats annoncés sont traités;
- les passages et embeddings sont présents dans Azure AI Search;
- une relance ne crée pas de doublon;
- créations, modifications et suppressions sont synchronisées;
- les changements de permissions sont appliqués;
- Bob ne peut jamais retrouver le document réservé à Alice;
- les documents d’une autre organisation sont invisibles;
- `/api/messages` peut utiliser `search_microsoft_365`;
- la réponse cite l’URL SharePoint;
- aucun secret ou contenu complet n’apparaît dans les logs;
- les erreurs temporaires sont réessayées;
- les erreurs permanentes sont visibles et compréhensibles;
- tous les nouveaux tests respectent les conventions du projet;
- les tests d’architecture réussissent;
- `dotnet test Solution.sln` réussit.

<a id="m365-sharepoint-references"></a>
## Références

- [Notifications Microsoft Graph](https://learn.microsoft.com/en-us/graph/change-notifications-overview)
- [Recevoir les notifications par webhook](https://learn.microsoft.com/en-us/graph/change-notifications-delivery-webhooks)
- [Créer une souscription](https://learn.microsoft.com/en-us/graph/api/subscription-post-subscriptions)
- [Renouveler une souscription](https://learn.microsoft.com/en-us/graph/api/subscription-update)
- [Suivre les changements d’une bibliothèque](https://learn.microsoft.com/en-us/graph/api/driveitem-delta)
- [Lister les listes d’un site](https://learn.microsoft.com/en-us/graph/api/list-list)
- [Lister les colonnes d’une liste](https://learn.microsoft.com/en-us/graph/api/list-list-columns)
- [Suivre les changements des éléments d’une liste](https://learn.microsoft.com/en-us/graph/api/listitem-delta)
- [API REST SharePoint](https://learn.microsoft.com/en-us/sharepoint/dev/sp-add-ins/get-to-know-the-sharepoint-rest-service)
- [Permissions d’un élément de liste — Graph beta, non utilisé en production](https://learn.microsoft.com/en-us/graph/api/listitem-get-permissions?view=graph-rest-beta)
- [Lire les permissions d’un fichier](https://learn.microsoft.com/en-us/graph/api/driveitem-list-permissions)
- [Créer un index vectoriel](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-create-index)
- [Exécuter une recherche vectorielle](https://learn.microsoft.com/en-us/azure/search/vector-search-how-to-query)
- [Exécuter une recherche hybride](https://learn.microsoft.com/en-us/azure/search/hybrid-search-how-to-query)
- [Émulateur Azure Service Bus](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)
