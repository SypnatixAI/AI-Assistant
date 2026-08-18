# Interface web du client

## Table des matières

- [Objectif](#frontend-objective)
- [Adresse du SaaS et organisation](#frontend-saas-url)
- [Parcours de connexion Microsoft](#frontend-microsoft-login)
- [Construction de la session](#frontend-session-bootstrap)
- [Écran principal](#frontend-main-screen)
- [Conversations](#frontend-conversations)
- [Messages et sources](#frontend-messages)
- [Choix du modèle](#frontend-model-selection)
- [Jetons disponibles](#frontend-token-usage)
- [Endpoints utilisés](#frontend-api-endpoints)
- [États et erreurs](#frontend-states-errors)
- [Architecture frontend](#frontend-architecture)
- [Sécurité](#frontend-security)
- [Expérience mobile](#frontend-responsive)
- [Hors périmètre](#frontend-out-of-scope)
- [Critères d'acceptation](#frontend-acceptance)
- [Documentation de référence](#frontend-references)

<a id="frontend-objective"></a>
## Objectif

Le frontend est une application web simple permettant à un employé d'utiliser
AssistantCore avec son compte professionnel Microsoft.

L'expérience reprend les repères d'un outil conversationnel moderne :

- une connexion Microsoft avant d'accéder aux données
- une barre latérale contenant les conversations
- une zone principale contenant les messages
- un champ pour écrire une question
- un sélecteur présentant les modèles autorisés
- les sources utilisées sous chaque réponse
- le nombre de jetons encore disponibles pour l'organisation

Le frontend ne contient aucune logique métier de sécurité. Il affiche les
informations autorisées par le backend et transmet le token Microsoft à chaque
appel protégé.

<a id="frontend-saas-url"></a>
## Adresse du SaaS et organisation

La première version utilise une adresse commune, par exemple :

```text
https://app.assistantcore.com
```

Après la connexion, le backend détermine l'organisation avec le claim `tid` du
token Microsoft Entra. Le frontend ne demande pas à l'utilisateur de choisir
manuellement son entreprise.

Une adresse personnalisée comme `metalpro.assistantcore.com` pourra être
ajoutée plus tard pour l'image de marque ou pour guider la connexion. Le
sous-domaine ne sera jamais une preuve d'autorisation. Même avec une telle
adresse, le backend devra valider `tid`, `oid`, les permissions OAuth et le
statut du membre.

Un domaine appartenant au client, par exemple `assistant.metalpro.com`, est
hors périmètre du MVP. Il exige une gestion supplémentaire des DNS, des
certificats et du cycle de vie de chaque domaine.

<a id="frontend-microsoft-login"></a>
## Parcours de connexion Microsoft

### Écran non connecté

L'écran public contient uniquement :

- le nom et une courte explication d'AssistantCore
- un bouton `Se connecter avec Microsoft`
- un message indiquant que le compte professionnel de l'entreprise est requis
- un lien vers l'aide ou le contact de soutien

Le bouton lance Microsoft Entra avec MSAL. Une Single-Page Application doit
utiliser le flux Authorization Code avec PKCE. Aucun client secret n'est placé
dans le navigateur.

Le frontend demande au minimum :

- les permissions OpenID nécessaires à la session Microsoft
- le scope délégué `api://<API_CLIENT_ID>/access_as_user`

La connexion peut demander une interaction pour le mot de passe, la MFA, le
choix du compte ou le consentement. Ces écrans appartiennent à Microsoft et ne
sont pas reproduits par AssistantCore.

### Retour de Microsoft

Après le retour sur l'URI enregistrée :

1. MSAL termine le traitement de la redirection.
2. Le compte Microsoft actif est sélectionné.
3. Le frontend tente d'obtenir silencieusement un access token destiné à
   l'API AssistantCore.
4. Si Microsoft exige une interaction, le frontend relance une interaction
   contrôlée.
5. Le frontend appelle `GET /api/core/authenticateUser`.
6. L'application principale est affichée seulement après une réponse valide.

<a id="frontend-session-bootstrap"></a>
## Construction de la session

`GET /api/core/authenticateUser` est la source de vérité pour la session
applicative. Une session frontend contient au minimum :

```json
{
  "user": {
    "id": "a7c2d5b1-9b5d-4a8d-8b9f-2c4d6e8f2001",
    "displayName": "Marc Tremblay",
    "email": "marc@metalpro.com"
  },
  "organization": {
    "id": "5e1d4f9a-4e68-4f35-9a9e-7d4d2f6c1001",
    "name": "MetalPro"
  },
  "roles": ["User"]
}
```

Le frontend ne construit jamais l'organisation depuis le domaine du courriel,
le sous-domaine de l'URL ou une valeur saisie par l'utilisateur.

Après la session, le frontend charge en parallèle :

- `GET /api/models`
- `GET /api/usage`
- `GET /api/conversations?limit=25`

Une route protégée ne doit pas afficher brièvement l'interface avant que la
session soit vérifiée. Pendant cette vérification, elle affiche un écran de
chargement neutre.

<a id="frontend-main-screen"></a>
## Écran principal

L'écran principal est divisé en trois zones.

### Barre latérale

Elle contient :

- le bouton `Nouvelle conversation`
- les conversations récentes
- un indicateur pendant le chargement d'une page supplémentaire
- le nom de l'utilisateur et de l'organisation
- l'action de déconnexion

### Zone de conversation

Sans conversation sélectionnée, elle affiche :

- un court message d'accueil utilisant le prénom ou le nom affiché
- quelques exemples concrets de questions
- le sélecteur de modèle
- le champ de saisie

Avec une conversation sélectionnée, elle affiche les messages dans l'ordre
chronologique et conserve le champ de saisie accessible en bas de l'écran.

### Indicateur de jetons

L'indicateur affiche `tokensRemaining` et `tokenLimit` sous une forme lisible,
par exemple `572 000 / 1 000 000 jetons disponibles`. Une infobulle précise la
date de remise à zéro du quota.

<a id="frontend-conversations"></a>
## Conversations

Le bouton `Nouvelle conversation` :

1. désélectionne la conversation courante
2. vide les messages affichés sans supprimer l'historique
3. place le curseur dans le champ de saisie
4. n'appelle pas immédiatement le backend

La conversation est créée par `POST /api/messages` lorsque le premier message
est envoyé avec `conversationId: null`.

La barre latérale utilise `GET /api/conversations`. Lorsque l'utilisateur
sélectionne une conversation, le frontend appelle
`GET /api/conversations/{conversationId}/messages`.

Les curseurs de pagination sont opaques. Le frontend les conserve et les
renvoie sans les décoder ni les modifier.

<a id="frontend-messages"></a>
## Messages et sources

Avant l'envoi, le frontend vérifie que le message contient au moins un
caractère autre qu'un espace. Le backend reste responsable de la validation
définitive et de la longueur maximale.

Pendant `POST /api/messages` :

- le bouton d'envoi est désactivé
- le texte envoyé apparaît immédiatement comme message utilisateur
- un indicateur montre que l'Assistant prépare la réponse
- un deuxième envoi dans la même conversation est bloqué

La première version reçoit une réponse complète, sans affichage mot par mot.
Une réponse réussie est ajoutée à la conversation avec ses sources et ses
avertissements.

Chaque source ayant une URL est un lien externe clairement identifié. Une
source sans URL affiche son titre et sa référence sans créer de lien vide.

Le frontend ne présente jamais un avertissement comme une source. Il explique
simplement qu'une partie des données n'a pas pu être consultée.

<a id="frontend-model-selection"></a>
## Choix du modèle

Le sélecteur utilise uniquement `GET /api/models`.

- le modèle par défaut est présélectionné pour une nouvelle conversation
- l'identifiant envoyé dans `POST /api/messages` est le champ `id`
- le nom destiné à l'utilisateur est le champ `displayName`
- un modèle désactivé ou absent de la réponse ne peut pas être sélectionné
- le frontend ne contient aucune liste de modèles codée en dur

Le modèle peut être changé avant chaque message. La réponse de
`POST /api/messages` indique le modèle réellement utilisé.

<a id="frontend-token-usage"></a>
## Jetons disponibles

Le quota appartient à l'organisation et est partagé entre ses membres. Il est
calculé sur une période mensuelle configurée par le backend.

Le frontend charge `GET /api/usage` au démarrage. Après un message réussi, il
remplace son compteur avec le bloc `usage` retourné par `POST /api/messages`.
Il ne soustrait jamais lui-même une estimation locale.

Lorsque `isExhausted` est vrai :

- le champ de saisie reste visible
- l'envoi est désactivé
- un message explique que le quota est épuisé
- la date `periodEndsAt` indique quand le quota sera renouvelé

Les jetons affichés sont des jetons facturés au quota du SaaS. Ils ne doivent
pas être confondus avec la limite technique de contexte ou le budget maximal
d'une seule orchestration.

<a id="frontend-api-endpoints"></a>
## Endpoints utilisés

| Moment | Appel | Résultat attendu |
| --- | --- | --- |
| Après la connexion | `GET /api/core/authenticateUser` | Utilisateur, organisation et rôles |
| Ouverture de l'application | `GET /api/models` | Modèles sélectionnables |
| Ouverture de l'application | `GET /api/usage` | Quota courant de l'organisation |
| Ouverture de l'application | `GET /api/conversations?limit=25` | Première page de conversations |
| Sélection d'une conversation | `GET /api/conversations/{id}/messages?limit=50` | Messages et sources |
| Envoi d'une question | `POST /api/messages` | Réponse, sources et quota actualisé |

Tous ces appels utilisent :

```http
Authorization: Bearer <access_token>
```

Le token est obtenu par le service d'authentification frontend, pas par chaque
composant visuel.

<a id="frontend-states-errors"></a>
## États et erreurs

Le frontend traite explicitement les situations suivantes :

| Statut | Comportement utilisateur |
| --- | --- |
| `400` | Afficher le problème de saisie sans perdre le message écrit |
| `401` | Tenter une acquisition silencieuse du token, puis reconnecter si nécessaire |
| `403` | Afficher que le compte ou l'organisation n'a pas accès au service |
| `404` | Retirer la conversation inaccessible de l'écran et revenir à une nouvelle conversation |
| `429` avec quota épuisé | Désactiver l'envoi et afficher la date de renouvellement |
| `429` fournisseur | Conserver le texte et proposer de réessayer plus tard |
| `502` ou `504` | Expliquer que le service externe est temporairement indisponible |
| `500` | Afficher une erreur générale sans détail technique |

Une erreur d'envoi ne doit pas effacer le texte de l'utilisateur. Un bouton
`Réessayer` peut renvoyer le même contenu seulement après une action explicite.

<a id="frontend-architecture"></a>
## Architecture frontend

Le projet frontend doit séparer au minimum :

```text
src/
  app/
    core/
      auth/         configuration MSAL et session
      guards/       protection des routes
      interceptors/ ajout du Bearer token et gestion HTTP
      api/          services HTTP et contrats backend
    features/
      chat/         interface et orchestration visuelle
      conversations/
      models/
      usage/
    domain/         modèles TypeScript indépendants d'Angular
    shared/         composants visuels réutilisables
```

- Le frontend utilise Angular CLI en mode strict.
- Les nouvelles fonctionnalités utilisent des composants standalone.
- Les composants Angular affichent les données et transmettent les actions.
- Les services Angular coordonnent les appels et les règles d'interface.
- Les signals contiennent les états locaux partagés d'une fonctionnalité.
- Les guards empêchent l'accès aux routes avant la construction de la session.
- Un interceptor HTTP obtient le token et ajoute le header Bearer.
- Un second interceptor transforme les erreurs HTTP vers le contrat frontend.
- `HttpClient` est l'unique mécanisme d'appel du backend.
- Les contrats API sont typés en TypeScript strict.
- La logique de quota, d'autorisation et de propriété reste dans le backend.

<a id="frontend-security"></a>
## Sécurité

- Aucun secret Microsoft, OpenAI ou fournisseur n'est ajouté au frontend.
- Seuls les identifiants publics de configuration peuvent être livrés au navigateur.
- Le frontend ne stocke pas l'access token dans un stockage persistant créé manuellement.
- MSAL gère l'acquisition et le cache du token.
- L'organisation provient toujours du backend.
- Le HTML provenant d'un message ou d'une source n'est jamais exécuté directement.
- Les liens externes sont ouverts avec les protections adaptées contre l'accès à la fenêtre d'origine.
- La déconnexion vide l'état applicatif et termine la session MSAL selon la stratégie retenue.
- Le backend autorise par CORS uniquement les origines frontend configurées.

<a id="frontend-responsive"></a>
## Expérience mobile

Sur un écran étroit :

- la barre latérale devient un panneau ouvrable et refermable
- le champ de saisie reste accessible sans couvrir le dernier message
- le sélecteur de modèle et le quota restent lisibles
- les longues URL de sources ne débordent pas de l'écran
- la navigation au clavier et le focus restent visibles

<a id="frontend-out-of-scope"></a>
## Hors périmètre

La première version ne couvre pas :

- le streaming mot par mot
- le partage public d'une conversation
- les pièces jointes
- la dictée vocale
- la création d'images
- les domaines personnalisés appartenant aux clients
- le changement manuel d'organisation pendant une session
- l'administration des membres dans l'interface conversationnelle

<a id="frontend-acceptance"></a>
## Critères d'acceptation

- Un utilisateur non connecté voit seulement l'écran de connexion.
- La connexion Microsoft utilise une SPA Entra et le flux PKCE.
- L'interface s'ouvre seulement après la construction de la session AssistantCore.
- L'utilisateur voit uniquement ses conversations et peut charger leur historique.
- Une nouvelle conversation est créée au premier message.
- Le modèle sélectionné vient du catalogue backend.
- La réponse affiche le modèle réellement utilisé, ses sources et ses avertissements.
- Le quota affiché vient du backend et se met à jour après un message.
- Un quota épuisé bloque l'envoi avec une explication claire.
- Les erreurs ne révèlent aucun secret ni détail technique.
- Le parcours principal fonctionne sur ordinateur et sur mobile.
- Les tests couvrent la connexion, le chargement initial, l'envoi, les erreurs et la déconnexion.

<a id="frontend-references"></a>
## Documentation de référence

- [Authentifier un utilisateur](../authentification/authenticate-user.md)
- [Lister les conversations](../conversations/list-conversations.md)
- [Charger les messages](../conversations/get-conversation-messages.md)
- [Envoyer un message](../messages/send-message.md)
- [Lister les modèles](../models/list-models.md)
- [Consulter le quota](../usage/get-token-usage.md)
- [Administrer les membres](member-administration.md)
- [Gérer le cycle de vie d'une conversation](../conversations/manage-conversation.md)
- [MSAL Browser](https://learn.microsoft.com/en-us/entra/msal/javascript/browser/about-msal-browser)
- [Préparer une SPA Angular pour Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-single-page-apps-angular-prepare-app)
- [Authorization Code avec PKCE](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow)
