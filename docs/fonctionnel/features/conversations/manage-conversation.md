# Gérer le cycle de vie d'une conversation

## Table des matières

- [But](#conversation-management-purpose)
- [Renommer ou archiver](#conversation-management-patch)
- [Supprimer](#conversation-management-delete)
- [Exemples de contrats](#conversation-management-examples)
- [Accès](#conversation-management-access)
- [Règles](#conversation-management-rules)
- [Traitement](#conversation-management-flow)
- [Erreurs](#conversation-management-errors)
- [Critères d'acceptation](#conversation-management-acceptance)

<a id="conversation-management-purpose"></a>
## But

Permettre au propriétaire de renommer, archiver, restaurer ou supprimer une
conversation sans exposer les conversations des autres membres.

<a id="conversation-management-patch"></a>
## Renommer ou archiver

```http
PATCH /api/conversations/{conversationId}
```

```json
{ "title": "Politique de télétravail", "status": "Archived" }
```

Chaque champ est optionnel, mais au moins un doit être fourni. `status` accepte
`Active` ou `Archived`. Un titre est nettoyé, non vide et limité par une
configuration backend.

La version connue du client voyage dans l'en-tête HTTP `If-Match`, pas dans le
corps : le corps ne contient que des champs modifiables, et tous y sont
optionnels.

```http
PATCH /api/conversations/conversation-123
If-Match: "7"
```

L'en-tête est optionnel. Absent, aucune vérification de concurrence n'est
demandée et la dernière écriture gagne. Présent mais illisible, la demande est
refusée avec `400` plutôt que d'écraser silencieusement une version plus
récente.

La réponse `200 OK` retourne la conversation actualisée :

```json
{
  "id": "conversation-123",
  "title": "Politique de télétravail",
  "status": "Archived",
  "updatedAt": "2026-08-18T14:30:00Z",
  "version": 7
}
```

<a id="conversation-management-delete"></a>
## Supprimer

```http
DELETE /api/conversations/{conversationId}
```

La réponse est `204 No Content`. La première étape est une suppression logique
avec date UTC. La purge physique est asynchrone et suit la politique de rétention.

<a id="conversation-management-examples"></a>
## Exemples de contrats

### Renommer seulement

```http
PATCH /api/conversations/conversation-123
Content-Type: application/json
```

```json
{ "title": "Budget marketing 2027" }
```

Le statut reste inchangé. Après normalisation des espaces, un titre identique
est un no-op : aucune nouvelle version et aucun nouvel audit.

### Archiver puis restaurer

```json
{ "status": "Archived" }
```

Une conversation archivée reste visible dans la section Archives, mais
`POST /api/messages` la refuse avec le code stable
`conversation_archived`. Pour la restaurer :

```json
{ "status": "Active" }
```

### Suppression logique

Après `DELETE`, les lectures ordinaires se comportent comme si la conversation
n’existait plus. Répéter le même `DELETE` retourne encore `204` sans révéler
son ancien titre ni créer un deuxième travail de purge.

### Conflit concurrent

Deux onglets utilisent la version 7. Le premier renommage produit la version 8.
Le second reçoit :

```json
{
  "code": "conversation_version_conflict",
  "message": "La conversation a été modifiée dans une autre session."
}
```

Le frontend recharge alors la conversation au lieu d’écraser la version 8.

<a id="conversation-management-access"></a>
## Accès

Seul le propriétaire actif dans l'organisation courante peut agir. Un Admin
n'obtient aucun accès automatique aux conversations d'un collègue.

<a id="conversation-management-rules"></a>
## Règles

- Une conversation archivée reste consultable mais refuse un nouveau message.
- Une conversation peut être restaurée vers `Active` avant sa suppression.
- Une conversation supprimée disparaît des listes et lectures ordinaires.
- Une répétition de DELETE reste idempotente sans révéler l'existence passée.
- Chaque modification réelle est auditée.

<a id="conversation-management-flow"></a>
## Traitement

1. Construire le contexte membre et organisation.
2. Charger avec `conversationId + organizationId + ownerMemberId`.
3. Valider le patch ou la suppression.
4. Appliquer une protection de concurrence.
5. Enregistrer la conversation et l'audit dans une unité cohérente.
6. Pour DELETE, publier ou rendre disponible le travail de purge après commit.

<a id="conversation-management-errors"></a>
## Erreurs

- `400` : patch vide, titre ou statut invalide.
- `401` : token invalide.
- `403` : membre ou organisation inactive.
- `404` : conversation absente ou étrangère.
- `409` : modification concurrente.

<a id="conversation-management-acceptance"></a>
## Critères d'acceptation

- Le propriétaire peut renommer, archiver, restaurer et supprimer.
- Une conversation archivée refuse `POST /api/messages`.
- Une suppression disparaît immédiatement des lectures normales.
- Les données physiques sont purgées selon la rétention.
- Les actions sont isolées, idempotentes, auditées et testées.
