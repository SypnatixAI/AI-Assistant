# Appliquer la rétention et la suppression des données

## Table des matières

- [But](#retention-purpose)
- [Catégories](#retention-categories)
- [Configuration initiale](#retention-configuration)
- [Suppression d'une conversation](#retention-conversation)
- [Départ d'un client](#retention-offboarding)
- [Worker de purge](#retention-worker)
- [Preuve et sécurité](#retention-proof)
- [Critères d'acceptation](#retention-acceptance)

<a id="retention-purpose"></a>
## But

Définir quand les données deviennent invisibles, quand elles sont physiquement
supprimées et comment prouver la purge sans conserver leur contenu.

<a id="retention-categories"></a>
## Catégories

La configuration distingue conversations/messages/sources, consommations de
jetons, contenus Azure AI Search, états d'ingestion, logs techniques et audits.
Chaque catégorie possède une durée explicite par environnement. Aucune durée
n'est cachée dans le code.

<a id="retention-configuration"></a>
## Configuration initiale

Les valeurs suivantes sont des valeurs de départ configurables, pas des
constantes métier :

```json
{
  "Retention": {
    "ConversationRecoveryDays": 30,
    "UsageMonths": 24,
    "AuditMonths": 84,
    "TechnicalLogDays": 30,
    "WorkerBatchSize": 100,
    "MaximumAttempts": 8
  }
}
```

Toutes les durées sont validées au démarrage. Une durée négative, une taille
de lot nulle ou une durée d’audit plus courte que l’exigence commerciale fait
échouer la configuration.

<a id="retention-conversation"></a>
## Suppression d'une conversation

DELETE enregistre `DeletedAt` et retire immédiatement la conversation des
lectures. Après le délai de récupération configuré, un worker supprime dans
l'ordre les sources, messages puis conversation. Les audits et agrégats de
facturation nécessaires sont conservés sans texte utilisateur.

<a id="retention-offboarding"></a>
## Départ d'un client

Pour une organisation existante, une demande de purge contrôlée :

1. bloque les nouvelles écritures;
2. crée un identifiant d'opération idempotent;
3. purge SQL par lots;
4. supprime les documents Azure AI Search;
5. retire les états et tokens d'ingestion;
6. vérifie chaque stockage;
7. produit un résumé sans contenu supprimé.

Cette procédure ne crée pas d'organisation et ne définit pas le premier Admin.

<a id="retention-worker"></a>
## Worker de purge

Le worker est reprenable, traite des lots limités, respecte le
`CancellationToken`, classe les erreurs temporaires et permanentes et ne marque
une opération terminée qu'après vérification de tous les stockages.

Une opération suit les étapes persistées suivantes :

```text
Pending
  -> DeleteSearchContent
  -> DeleteMessageSources
  -> DeleteMessages
  -> DeleteConversations
  -> DeleteIngestionState
  -> Verify
  -> Completed
```

Chaque étape enregistre sa dernière clé traitée. Après un crash pendant
`DeleteMessages`, le worker reprend ce lot au lieu de recommencer les étapes
déjà confirmées. Une suppression d’un contenu déjà absent est un succès
idempotent.

Exemple d’opération :

```json
{
  "operationId": "purge-2026-0081",
  "organizationId": "organization-123",
  "scope": "Conversation",
  "targetId": "conversation-456",
  "status": "Running",
  "currentStep": "DeleteMessages",
  "attempt": 2,
  "nextAttemptAt": "2026-08-18T16:15:00Z"
}
```

<a id="retention-proof"></a>
## Preuve et sécurité

La preuve conserve identifiant d'opération, organisation, catégories, nombres,
dates et résultat. Elle ne conserve aucun titre, message ou document. Les
sauvegardes suivent une expiration documentée et un accès restreint.

Exemple de preuve finale :

```json
{
  "operationId": "purge-2026-0081",
  "completedAt": "2026-08-18T16:42:00Z",
  "result": "Completed",
  "deletedCounts": {
    "conversations": 1,
    "messages": 18,
    "sources": 6,
    "searchDocuments": 42
  },
  "verifiedStores": ["SqlServer", "AzureAiSearch", "IngestionState"]
}
```

<a id="retention-acceptance"></a>
## Critères d'acceptation

- Une suppression logique est immédiate pour l'utilisateur.
- La purge est idempotente et reprenable.
- SQL, Search et ingestion sont vérifiés.
- Les audits et données de facturation restantes ne contiennent aucun contenu.
- Les sauvegardes et délais sont documentés et testés.
