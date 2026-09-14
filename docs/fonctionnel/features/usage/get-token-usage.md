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

L’agent Foundry peut effectuer plusieurs appels au modèle pour demander des
outils et produire la réponse finale. Les jetons de tous ces appels comptent.

Le quota commercial est différent :

- de la taille maximale du contexte d'un modèle
- de `Messages:AgentRuntime:MaximumExecutionTimeSeconds`, qui borne la durée
  technique d’une seule demande
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
- `tokensUsed` est le total actualisé connu par l'instance API.
- `tokensRemaining` est calculé par le backend et ne devient jamais négatif.

La consommation est persistée avec le message Assistant dans le même
`SaveChanges` EF. Il n'y a donc pas d'écriture SQL supplémentaire dédiée au
quota sur le chemin critique du message.

Après cette écriture durable, le cache local de quota est incrémenté en mémoire
et fournit immédiatement le solde retourné par `POST /api/messages`.

Le frontend remplace son ancien compteur par ces valeurs. Il ne fait pas une
soustraction locale.

`GET /api/usage` reste la lecture autoritative depuis les consommations
persistées en base.

<a id="usage-exhausted"></a>
## Quota épuisé

Avant de démarrer une nouvelle orchestration, le backend vérifie que
l'organisation possède encore des jetons.

Cette vérification est effectuée uniquement dans le cache local en mémoire de
l'instance API. `POST /api/messages` ne fait aucun appel SQL, Redis ou réseau
supplémentaire pour vérifier le quota avant Foundry.

Le cache est chargé depuis les consommations persistées au démarrage de
l'application puis resynchronisé périodiquement en arrière-plan. La
resynchronisation n'est pas effectuée dans la requête `/api/messages`.

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
- les demandes suivantes de la même instance sont refusées immédiatement
- les autres instances convergent lors de leur prochaine resynchronisation
- la réponse déjà produite n'est pas supprimée

<a id="usage-concurrency"></a>
## Concurrence et cohérence

Plusieurs membres peuvent envoyer une question simultanément.

- Chaque consommation possède un identifiant unique lié au message Assistant.
- Le même message ne peut pas être facturé deux fois dans le journal persistant.
- La consommation et le message Assistant sont écrits dans le même `SaveChanges`.
- Le cache local est thread-safe et ne recompte pas deux fois le même message pendant la vie de l'instance.
- Le cache est initialisé depuis le journal durable avant de servir les demandes.
- Les instances API se resynchronisent périodiquement depuis la base en dehors du chemin `/messages`.
- Une resynchronisation ne remplace jamais un compteur local plus récent par une valeur durable plus faible.
- `GET /api/usage` calcule son total depuis la persistance et ne dépend pas uniquement du cache local.
- La période utilisée pour l'écriture est la même que celle retournée au frontend.

Une légère surconsommation due à des demandes simultanées ou à la courte fenêtre
de convergence entre plusieurs instances est acceptée pour le MVP. Une
réservation préalable stricte est hors périmètre tant que le besoin commercial
n'est pas confirmé.

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

Le chemin critique d'un message respecte :

```text
POST /api/messages
  -> lecture du quota en mémoire
  -> orchestration / Foundry
  -> persistance du message Assistant + TokenConsumption dans le même SaveChanges
  -> mise à jour du cache local en mémoire
  -> réponse
```

La synchronisation durable du cache est séparée :

```text
Démarrage / worker périodique
  -> repository de consommation
  -> agrégation SQL de la période
  -> fusion dans le cache local
```

Le handler orchestre les services et ne calcule pas le quota. Aucun appel
externe supplémentaire de quota n'est ajouté avant l'appel Foundry.

La persistance permet de retrouver au minimum :

- l'organisation
- le message Assistant à l'origine de la consommation
- le début et la fin de la période
- les jetons d'entrée
- les jetons de sortie
- le total
- la date d'enregistrement

Le journal de consommation reste la source durable et auditable. Le cache local
sert uniquement à appliquer rapidement le quota sur le chemin de
`POST /api/messages`.

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

Une erreur de persistance de la consommation ne doit pas être ignorée. Comme la
consommation est écrite avec la réponse Assistant, le cache n'est actualisé
qu'après une persistance réussie.

Une erreur de resynchronisation en arrière-plan conserve le dernier snapshot
local valide et est journalisée. Elle n'ajoute pas de dépendance réseau à la
requête `/api/messages`.

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
- `POST /api/messages` n'effectue aucune lecture SQL ou Redis dédiée au quota.
- La consommation est persistée avec le message Assistant sans round-trip SQL supplémentaire.
- Les appels externes autres que les modèles IA ne sont pas comptés comme jetons.
- Les tests couvrent le renouvellement de période, la concurrence et l'isolation.
- Les tests respectent les conventions du projet et `dotnet test Solution.sln` réussit.

<a id="usage-references"></a>
## Documentation de référence

- [Interface web — jetons disponibles](../frontend/client-interface.md#frontend-token-usage)
- [Envoyer un message](../messages/send-message.md)
- [Gérer la politique de quota](manage-usage-policy.md)
