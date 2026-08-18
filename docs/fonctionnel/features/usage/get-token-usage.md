# Consulter le quota de jetons

## Table des matières

- [But](#usage-purpose)
- [Définition du quota](#usage-definition)
- [Route](#usage-route)
- [Accès](#usage-access)
- [Requête](#usage-request)
- [Réponse](#usage-response)
- [Calcul des jetons](#usage-calculation)
- [Mise à jour après un message](#usage-message-update)
- [Quota épuisé](#usage-exhausted)
- [Concurrence et cohérence](#usage-concurrency)
- [Architecture et persistance](#usage-architecture)
- [Erreurs](#usage-errors)
- [Sécurité](#usage-security)
- [Critères d'acceptation](#usage-acceptance)
- [Documentation de référence](#usage-references)

<a id="usage-purpose"></a>
## But

`GET /api/usage` permet au frontend d'afficher le quota de jetons de
l'organisation courante pour la période de facturation active.

Il répond clairement à trois questions :

- combien de jetons l'organisation peut utiliser pendant la période
- combien ont déjà été consommés
- quand le quota sera renouvelé

<a id="usage-definition"></a>
## Définition du quota

Pour le MVP, le quota est :

- défini par organisation
- partagé entre tous les membres actifs de cette organisation
- renouvelé mensuellement
- exprimé en jetons réellement déclarés par les fournisseurs de modèles
- consommé par tous les appels au modèle nécessaires à une question

Une orchestration peut appeler le modèle plusieurs fois pour demander des
outils et produire la réponse finale. Les jetons de tous ces appels comptent.

Le quota commercial est différent :

- de la taille maximale du contexte d'un modèle
- de `Messages:Orchestration:MaximumModelTokens`, qui protège une seule demande
- d'une estimation affichée avant l'envoi

Le montant exact du quota et le jour de renouvellement proviennent de la
configuration commerciale ou de l'abonnement de l'organisation. Ils ne sont
pas codés en dur dans le frontend.

<a id="usage-route"></a>
## Route

```http
GET /api/usage
```

<a id="usage-access"></a>
## Accès

Un membre `Admin` ou `User` actif peut consulter le résumé de son organisation.
Il ne peut pas demander l'usage d'une autre organisation.

<a id="usage-request"></a>
## Requête

L'endpoint ne reçoit aucun body ni identifiant d'organisation.

La période courante est déterminée par le backend à partir de l'heure UTC et
de la configuration de l'abonnement.

<a id="usage-response"></a>
## Réponse

```json
{
  "periodStartsAt": "2026-08-01T00:00:00Z",
  "periodEndsAt": "2026-09-01T00:00:00Z",
  "tokenLimit": 1000000,
  "tokensUsed": 428000,
  "tokensRemaining": 572000,
  "isExhausted": false
}
```

### Règles

- Toutes les dates sont en UTC.
- `periodStartsAt` est inclus dans la période.
- `periodEndsAt` est exclu de la période et représente le renouvellement.
- `tokenLimit` est supérieur ou égal à zéro.
- `tokensUsed` est supérieur ou égal à zéro.
- `tokensRemaining` vaut `max(0, tokenLimit - tokensUsed)`.
- `isExhausted` vaut `true` lorsque `tokensRemaining` vaut zéro.
- Les compteurs utilisent un entier 64 bits pour éviter un dépassement.

<a id="usage-calculation"></a>
## Calcul des jetons

Le backend possède déjà les valeurs `InputTokens` et `OutputTokens` retournées
par le fournisseur pour chaque appel au modèle.

Pour une question :

```text
jetons de la question
  = somme des InputTokens de chaque appel
  + somme des OutputTokens de chaque appel
```

Les appels échoués sont traités ainsi :

- si le fournisseur retourne un usage fiable, cet usage est enregistré
- si aucun usage fiable n'est retourné, le backend n'invente pas une valeur
- l'absence d'usage fournisseur est journalisée pour permettre une vérification

Les appels aux bases de données, Azure AI Search ou Microsoft Graph ne sont pas
comptés comme jetons de modèle.

<a id="usage-message-update"></a>
## Mise à jour après un message

Après une réponse réussie, `POST /api/messages` retourne également :

```json
{
  "usage": {
    "requestTokens": 8460,
    "tokenLimit": 1000000,
    "tokensUsed": 436460,
    "tokensRemaining": 563540,
    "periodEndsAt": "2026-09-01T00:00:00Z",
    "isExhausted": false
  }
}
```

- `requestTokens` est le total facturé pour cette demande.
- `tokensUsed` est le total actualisé de la période.
- `tokensRemaining` vient du backend après enregistrement.

Le frontend remplace son ancien compteur par ces valeurs. Il ne fait pas une
soustraction locale, car un autre membre de l'organisation peut consommer des
jetons en même temps.

<a id="usage-exhausted"></a>
## Quota épuisé

Avant de démarrer une nouvelle orchestration, le backend vérifie que
l'organisation possède encore des jetons.

Lorsque le quota est déjà épuisé, `POST /api/messages` retourne :

```http
429 Too Many Requests
```

```json
{
  "code": "organization_token_quota_exhausted",
  "message": "Le quota de jetons de l'organisation est épuisé.",
  "detail": null,
  "metadata": {
    "periodEndsAt": "2026-09-01T00:00:00Z"
  }
}
```

Le code stable distingue le quota AssistantCore d'une limitation temporaire
du fournisseur IA.

Le contrôle avant l'appel ne peut pas connaître exactement le coût de la
future réponse. Une demande commencée avec un solde positif peut donc dépasser
légèrement le quota. Dans ce cas :

- la consommation réelle est enregistrée
- `tokensRemaining` reste à zéro et ne devient jamais négatif dans le contrat
- les demandes suivantes sont refusées
- la réponse déjà produite n'est pas supprimée

<a id="usage-concurrency"></a>
## Concurrence et cohérence

Plusieurs membres peuvent envoyer une question simultanément.

- Chaque consommation possède un identifiant unique lié au message Assistant.
- Le même message ne peut pas être facturé deux fois après une reprise.
- L'écriture utilise une transaction ou un mécanisme atomique adapté à SQL Server.
- Le total lu ne doit pas dépendre d'une valeur conservée uniquement en mémoire.
- La période utilisée pour l'écriture est la même que celle retournée au frontend.

Une légère surconsommation due à deux demandes simultanées est acceptée pour le
MVP. Une réservation préalable stricte est hors périmètre tant que le besoin
commercial n'est pas confirmé.

<a id="usage-architecture"></a>
## Architecture et persistance

Le traitement de lecture respecte :

```text
UsageController
  -> IDispatcher
  -> GetTokenUsageCommandHandler
  -> service applicatif de quota
  -> repository de consommation
```

L'enregistrement après un message suit le flow existant du message :

```text
SendMessageCommandHandler
  -> service applicatif d'orchestration
  -> service applicatif de quota
  -> repository de consommation
```

Le handler orchestre les services et ne calcule pas le quota.

La persistance doit permettre de retrouver au minimum :

- l'organisation
- le message Assistant à l'origine de la consommation
- le début et la fin de la période
- les jetons d'entrée
- les jetons de sortie
- le total
- la date d'enregistrement

Le choix entre un journal de consommation et un compteur agrégé est laissé au
développeur. Le résultat doit rester auditable et empêcher un double comptage.

<a id="usage-errors"></a>
## Erreurs

### `401 Unauthorized`

- token absent, invalide ou expiré

### `403 Forbidden`

- organisation ou membre inactif
- permissions OAuth insuffisantes

### `429 Too Many Requests`

- quota de l'organisation épuisé lors de l'envoi d'un message

### `500 Internal Server Error`

- abonnement ou période introuvable
- limite de jetons invalide
- impossibilité de lire ou d'enregistrer la consommation

Une erreur de persistance de la consommation ne doit pas être ignorée. La
réponse ne doit pas prétendre que le compteur est actualisé si l'écriture a
échoué.

<a id="usage-security"></a>
## Sécurité

- L'organisation vient uniquement du contexte authentifié.
- Un membre voit le résumé partagé, pas le détail d'utilisation des collègues.
- Aucun prix fournisseur, secret ou coût interne n'est retourné.
- Les valeurs envoyées par le frontend ne participent jamais au calcul.
- Les logs ne contiennent pas le texte complet des messages pour expliquer une consommation.

<a id="usage-acceptance"></a>
## Critères d'acceptation

- Le résumé retourne la période mensuelle active de l'organisation courante.
- La somme des jetons d'entrée et de sortie de tous les appels est enregistrée.
- Deux organisations possèdent des compteurs complètement séparés.
- Un même message ne peut pas être compté deux fois.
- La réponse de message contient le total de la demande et le solde actualisé.
- Un quota épuisé retourne le code stable `organization_token_quota_exhausted`.
- Le contrat ne retourne jamais un nombre négatif de jetons restants.
- Les appels externes autres que les modèles IA ne sont pas comptés comme jetons.
- Les tests couvrent le renouvellement de période, la concurrence et l'isolation.
- Les tests respectent les conventions du projet et `dotnet test Solution.sln` réussit.

<a id="usage-references"></a>
## Documentation de référence

- [Interface web — jetons disponibles](../frontend/client-interface.md#frontend-token-usage)
- [Envoyer un message](../messages/send-message.md)
- [Gérer la politique de quota](manage-usage-policy.md)
