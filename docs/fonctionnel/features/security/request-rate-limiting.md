# Limiter les requêtes et les orchestrations simultanées

## Table des matières

- [But](#rate-limit-purpose)
- [Limites](#rate-limit-rules)
- [Configuration et clés](#rate-limit-configuration)
- [Réponse](#rate-limit-response)
- [Stockage](#rate-limit-storage)
- [Ordre des contrôles](#rate-limit-order)
- [Frontend](#rate-limit-frontend)
- [Critères d'acceptation](#rate-limit-acceptance)

<a id="rate-limit-purpose"></a>
## But

Protéger l'API contre les rafales et limiter les orchestrations IA coûteuses,
sans utiliser l'adresse IP comme seule identité.

<a id="rate-limit-rules"></a>
## Limites

- requêtes générales par membre sur une fenêtre courte;
- requêtes générales agrégées par organisation;
- démarrages de `POST /api/messages` par minute;
- orchestrations simultanées par organisation.

Les valeurs viennent de la configuration validée. Les endpoints techniques de
santé utilisent une politique séparée.

<a id="rate-limit-configuration"></a>
## Configuration et clés

Valeurs initiales configurables :

```json
{
  "RateLimiting": {
    "MemberRequestsPerMinute": 120,
    "OrganizationRequestsPerMinute": 1000,
    "MemberMessagesPerMinute": 10,
    "OrganizationConcurrentOrchestrations": 5,
    "LeaseSeconds": 300
  }
}
```

Les clés utilisent uniquement les identifiants internes validés :

```text
rate:member:{organizationId}:{memberId}:{policy}
rate:organization:{organizationId}:{policy}
orchestration:{organizationId}:{leaseId}
```

L’adresse IP peut protéger l’écran public avant authentification, mais ne
remplace jamais les clés membre et organisation après authentification.

<a id="rate-limit-response"></a>
## Réponse

```http
429 Too Many Requests
Retry-After: 30
```

```json
{
  "code": "request_rate_limit_exceeded",
  "message": "Trop de demandes ont été envoyées.",
  "metadata": { "retryAfterSeconds": 30 }
}
```

`request_rate_limit_exceeded` reste distinct de
`organization_token_quota_exhausted` et d'un `429` fournisseur.

Exemple d’un quota mensuel épuisé :

```json
{
  "code": "organization_token_quota_exhausted",
  "metadata": { "periodEndsAt": "2026-09-01T00:00:00Z" }
}
```

Exemple d’un fournisseur temporairement limité :

```json
{
  "code": "ai_provider_rate_limited",
  "metadata": { "retryAfterSeconds": 20 }
}
```

Angular choisit son message avec `code`, jamais uniquement avec le statut 429.

<a id="rate-limit-storage"></a>
## Stockage

Les compteurs devant être cohérents entre plusieurs instances utilisent un
stockage distribué. Une panne du stockage applique une stratégie explicite et
observable; elle ne doit pas provoquer une boucle d'appels au modèle.

Pour `POST /api/messages`, la stratégie est fermée : si le compteur distribué
ou l’acquisition d’une place est indisponible, aucun appel au modèle ne démarre
et l’API retourne `503 rate_limit_store_unavailable`. Les lectures non
coûteuses peuvent utiliser une politique dégradée distincte si elle est
documentée endpoint par endpoint.

Une place d’orchestration possède un `leaseId`, une expiration et un état de
libération. Le même `leaseId` libéré deux fois ne décrémente le compteur qu’une
fois. Un worker de récupération libère les baux expirés après un crash.

<a id="rate-limit-order"></a>
## Ordre des contrôles

1. Valider le token.
2. Identifier membre et organisation.
3. Appliquer la limite courte durée.
4. Vérifier quota et disponibilité d'une place d'orchestration.
5. Enregistrer la question puis démarrer les appels externes.
6. Libérer la place dans un `finally`, y compris après annulation ou erreur.

<a id="rate-limit-frontend"></a>
## Frontend

Angular conserve la question, bloque temporairement l'envoi et affiche le
temps de reprise. Il ne confond pas cette erreur avec un quota mensuel épuisé.

Exemple : pour `request_rate_limit_exceeded` avec `retryAfterSeconds = 30`, le
bouton affiche `Réessayer dans 30 s`, reste désactivé pendant le compte à
rebours, puis redevient disponible. Angular ne renvoie pas automatiquement la
question à la fin du délai; l’utilisateur confirme un nouvel envoi.

<a id="rate-limit-acceptance"></a>
## Critères d'acceptation

- Les limites sont isolées par membre et organisation.
- Plusieurs instances partagent les mêmes compteurs.
- Une place simultanée est toujours libérée.
- Aucun appel au modèle ne démarre après un refus.
- Les trois types de `429` sont distingués et testés.
