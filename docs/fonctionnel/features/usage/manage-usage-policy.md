# Gérer la politique de quota d'une organisation

## Table des matières

- [But](#usage-policy-purpose)
- [Contrat interne](#usage-policy-contract)
- [Authentification](#usage-policy-auth)
- [Règles](#usage-policy-rules)
- [Historique](#usage-policy-history)
- [Exemples de sélection](#usage-policy-examples)
- [Erreurs](#usage-policy-errors)
- [Critères d'acceptation](#usage-policy-acceptance)

<a id="usage-policy-purpose"></a>
## But

Configurer la limite mensuelle d'une organisation existante sans créer ni
provisionner cette organisation. Pour le MVP, les jetons bruts restent l'unité
commerciale, quel que soit le modèle.

<a id="usage-policy-contract"></a>
## Contrat interne

```http
PUT /api/internal/organizations/{organizationId}/usage-policy
```

```json
{
  "monthlyTokenLimit": 1000000,
  "status": "Active",
  "effectiveAt": "2026-09-01T00:00:00Z"
}
```

`status` accepte `Active` ou `Suspended`. La réponse retourne la politique
enregistrée, sa version et sa date d'effet.

Exemple de réponse :

```json
{
  "organizationId": "organization-123",
  "version": 3,
  "monthlyTokenLimit": 1000000,
  "status": "Active",
  "effectiveAt": "2026-09-01T00:00:00Z",
  "createdAt": "2026-08-18T15:00:00Z"
}
```

<a id="usage-policy-auth"></a>
## Authentification

L'endpoint n'est jamais appelé par Angular. Il exige un token applicatif Entra
destiné aux opérations Sypnatix et une permission dédiée. Un token utilisateur
avec `access_as_user` ne suffit pas.

<a id="usage-policy-rules"></a>
## Règles

- L'organisation doit déjà exister.
- La limite est un entier 64 bits positif ou nul.
- Une politique future s'applique à `effectiveAt`.
- Une réduction immédiate ne supprime aucune consommation existante.
- Si la nouvelle limite est sous l'usage courant, le solde devient zéro.
- `Suspended` bloque les nouveaux messages avec un code distinct du quota épuisé.
- Les périodes passées ne sont jamais recalculées.

<a id="usage-policy-history"></a>
## Historique

Chaque version conserve organisation, limite, statut, date d'effet, acteur
applicatif et date de création. Toute modification produit aussi un audit.

<a id="usage-policy-examples"></a>
## Exemples de sélection

Une organisation possède :

| Version | Limite | Statut | Date d’effet |
| --- | ---: | --- | --- |
| 1 | 500 000 | Active | 2026-08-01 00:00 UTC |
| 2 | 1 000 000 | Active | 2026-09-01 00:00 UTC |

Le 18 août, `GET /api/usage` utilise la version 1. Le 1er septembre à
00:00 UTC, il utilise la version 2. Les consommations d’août ne sont pas
recalculées.

Si la consommation courante vaut 700 000 et qu’une politique immédiate fixe la
limite à 600 000, la nouvelle réponse possède `tokensRemaining = 0`. Les
700 000 jetons déjà consommés restent enregistrés.

Une politique `Suspended` refuse un nouveau message avec :

```json
{
  "code": "organization_usage_suspended",
  "message": "L’utilisation des modèles est suspendue pour cette organisation."
}
```

Ce code est distinct de `organization_token_quota_exhausted`.

<a id="usage-policy-errors"></a>
## Erreurs

- `400` : limite négative, statut inconnu, date sans fuseau ou période ambiguë;
- `401` : token applicatif absent ou invalide;
- `403` : permission applicative d’opérations absente ou token utilisateur;
- `404` : organisation inexistante;
- `409` : version concurrente ou politique incompatible à la même date d’effet.

L’endpoint ne crée jamais automatiquement une organisation lorsqu’il reçoit
un `organizationId` inconnu.

<a id="usage-policy-acceptance"></a>
## Critères d'acceptation

- Une identité applicative autorisée peut planifier une politique.
- Un utilisateur client ne peut pas appeler l'endpoint.
- La bonne version est choisie pour une date UTC.
- Les changements n'altèrent pas l'historique de consommation.
- Isolation, concurrence, audit et validations sont testés.
