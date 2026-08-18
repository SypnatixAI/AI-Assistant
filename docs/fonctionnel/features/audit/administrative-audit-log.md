# Journaliser les actions administratives

## Table des matières

- [But](#audit-purpose)
- [Actions couvertes](#audit-actions)
- [Données](#audit-data)
- [Exemples d’entrées](#audit-examples)
- [Écriture](#audit-write)
- [Lecture et rétention](#audit-read-retention)
- [Sécurité](#audit-security)
- [Critères d'acceptation](#audit-acceptance)

<a id="audit-purpose"></a>
## But

Conserver une preuve exploitable des changements sensibles sans enregistrer le
contenu des conversations, documents, tokens ou secrets.

<a id="audit-actions"></a>
## Actions couvertes

- rôle ou statut d'un membre;
- politique de quota;
- archivage ou suppression de conversation;
- activation, désactivation ou configuration d'un connecteur.

<a id="audit-data"></a>
## Données

Chaque entrée immuable contient `organizationId`, `actorType`, `actorId`,
`action`, `targetType`, `targetId`, `occurredAt`, `oldValues`, `newValues` et
`correlationId`. Les changements JSON utilisent une liste blanche propre à
l'action.

Les valeurs initiales stables sont :

| Action | Cible | Champs autorisés dans old/new |
| --- | --- | --- |
| `MemberRoleChanged` | membre | `role` |
| `MemberStatusChanged` | membre | `status` |
| `UsagePolicyChanged` | organisation | `monthlyTokenLimit`, `status`, `effectiveAt`, `version` |
| `ConversationArchived` | conversation | `status` |
| `ConversationDeleted` | conversation | `deletedAt` |
| `ConnectorStatusChanged` | connecteur | `status`, `isIndexed` |

Un champ absent de cette liste blanche est rejeté avant l’écriture.

<a id="audit-examples"></a>
## Exemples d’entrées

Exemple de changement de statut :

```json
{
  "organizationId": "organization-123",
  "actorType": "Member",
  "actorId": "member-alice",
  "action": "MemberStatusChanged",
  "targetType": "Member",
  "targetId": "member-bob",
  "occurredAt": "2026-08-18T15:10:00Z",
  "oldValues": { "status": "Active" },
  "newValues": { "status": "Inactive" },
  "correlationId": "request-8f812"
}
```

L’entrée ne contient ni nom, ni courriel, ni token. Les identifiants permettent
au support autorisé de corréler l’action sans copier les données personnelles.

Exemple d’opération idempotente : Bob est déjà `Inactive` et Alice redemande
`Inactive`. Aucune donnée métier ne change, donc aucune entrée d’audit n’est
ajoutée.

Exemple de rollback : l’écriture de l’audit échoue dans la transaction SQL. Le
statut de Bob reste `Active` et l’API ne retourne pas un faux succès.

<a id="audit-write"></a>
## Écriture

L'audit est écrit dans la même transaction que la modification lorsque les
données sont dans la même base. Si l'audit échoue, l'action administrative
n'est pas présentée comme réussie. Une répétition idempotente sans changement
ne crée pas une nouvelle entrée.

Le repository ordinaire expose uniquement l’ajout et la lecture contrôlée. Il
n’expose aucune méthode de modification ou de suppression. Une éventuelle
purge réglementaire utilise un mécanisme d’administration distinct et audité.

<a id="audit-read-retention"></a>
## Lecture et rétention

La première version ne fournit pas d'endpoint client de lecture. Le support
autorisé utilise une procédure contrôlée. La durée de conservation est plus
longue que celle des conversations et est définie par la politique de rétention.

<a id="audit-security"></a>
## Sécurité

Les entrées ne contiennent jamais access token, claims complets, API key,
message, réponse, preuve documentaire ou secret de connecteur. Une organisation
ne peut pas modifier ou supprimer directement ses audits.

<a id="audit-acceptance"></a>
## Critères d'acceptation

- Chaque action sensible produit une entrée avec acteur, cible et changement.
- Une transaction échouée ne laisse pas un audit mensonger.
- Les champs sensibles sont filtrés par liste blanche.
- Les audits sont immuables, isolés par organisation et testés.
