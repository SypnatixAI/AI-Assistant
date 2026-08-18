# Documentation fonctionnelle

Ce dossier contient la documentation fonctionnelle du projet.

## Table des matieres

- [Authentifier un utilisateur](features/authentification/authenticate-user.md)
- [Gerer les membres](features/membres/manage-members.md)
- [Gerer le statut d'un membre](features/membres/manage-member-status.md)
- [Lister les conversations](features/conversations/list-conversations.md)
- [Charger les messages d'une conversation](features/conversations/get-conversation-messages.md)
- [Envoyer un message](features/messages/send-message.md)
- [Interface web du client](features/frontend/client-interface.md)
- [Lister les modèles disponibles](features/models/list-models.md)
- [Consulter le quota de jetons](features/usage/get-token-usage.md)
- [Gerer la politique de quota](features/usage/manage-usage-policy.md)
- [Administrer les membres dans Angular](features/frontend/member-administration.md)
- [Gerer le cycle de vie d'une conversation](features/conversations/manage-conversation.md)
- [Journaliser les actions administratives](features/audit/administrative-audit-log.md)
- [Appliquer la retention et la suppression des donnees](features/data/data-retention-and-deletion.md)
- [Limiter les requetes et les orchestrations simultanees](features/security/request-rate-limiting.md)
- [Preparer les environnements de production](../operations/production-readiness.md)
- [Indexer les documents SharePoint dans Azure AI Search](features/microsoft365/index-sharepoint-content.md)
- [Feuille de route Azure AI Search](../tickets/azure-ai-search/README.md)

## Objectif

Decrire clairement ce que le produit doit faire, pour aider a cadrer et construire les fonctionnalites attendues.

## Organisation des documents

- chaque document possede une table des matieres cliquable
- les sections importantes possedent une ancre stable utilisable dans les tickets
- les tickets doivent pointer vers la section exacte qui decrit le comportement attendu
- les liens entre documents utilisent des chemins relatifs pour fonctionner localement et sur GitHub
