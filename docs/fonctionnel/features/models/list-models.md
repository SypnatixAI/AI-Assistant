# Lister les modèles disponibles

## Table des matières

- [But](#models-purpose)
- [Route](#models-route)
- [Accès](#models-access)
- [Requête](#models-request)
- [Réponse](#models-response)
- [Règles des champs](#models-fields)
- [Étapes de traitement](#models-flow)
- [Architecture](#models-architecture)
- [Erreurs](#models-errors)
- [Sécurité](#models-security)
- [Critères d'acceptation](#models-acceptance)
- [Documentation de référence](#models-references)

<a id="models-purpose"></a>
## But

`GET /api/models` retourne les modèles d'intelligence artificielle que
l'utilisateur courant peut sélectionner dans l'interface.

Le frontend ne doit pas recopier la liste provenant de la configuration du
backend. Sans cet endpoint, un modèle désactivé peut rester visible dans une
ancienne version du frontend et provoquer une erreur au moment de l'envoi.

<a id="models-route"></a>
## Route

```http
GET /api/models
```

<a id="models-access"></a>
## Accès

Un membre `Admin` ou `User` peut appeler cet endpoint si son compte et son
organisation sont actifs.

Le JWT doit contenir les permissions exigées par le flux d'authentification
AssistantCore. L'organisation est déterminée depuis l'identité authentifiée.

<a id="models-request"></a>
## Requête

L'endpoint ne reçoit aucun body ni paramètre de requête.

Le frontend ne fournit jamais :

- un fournisseur comme OpenAI ou Anthropic
- une clé API
- une URL de fournisseur
- l'identifiant d'une organisation
- une liste de modèles demandés

<a id="models-response"></a>
## Réponse

```json
{
  "defaultModelId": "gpt-5.6-luna",
  "models": [
    {
      "id": "gpt-5.6-luna",
      "displayName": "Luna",
      "description": "Modèle général recommandé pour la plupart des questions.",
      "isDefault": true
    },
    {
      "id": "gpt-5.6-terra",
      "displayName": "Terra",
      "description": "Modèle adapté aux analyses plus détaillées.",
      "isDefault": false
    }
  ]
}
```

La collection `models` est toujours présente. Si aucun modèle n'est
disponible, le backend retourne une erreur de configuration plutôt qu'une
réponse valide impossible à utiliser.

<a id="models-fields"></a>
## Règles des champs

### `defaultModelId`

- correspond exactement à un `id` de la collection
- désigne le modèle présélectionné pour une nouvelle conversation
- ne révèle pas le nom technique du fournisseur

### `id`

- est un identifiant public et stable
- est envoyé tel quel dans le champ `model` de `POST /api/messages`
- ne contient aucune clé, URL ou donnée confidentielle

### `displayName`

- est destiné à l'interface utilisateur
- est compréhensible sans connaître le nom technique du fournisseur
- n'est jamais utilisé comme identifiant dans une requête

### `description`

- explique concrètement l'usage recommandé du modèle
- reste courte pour pouvoir être affichée dans le sélecteur

### `isDefault`

- vaut `true` pour exactement un modèle
- correspond au modèle identifié par `defaultModelId`

<a id="models-flow"></a>
## Étapes de traitement

1. Vérifier le token et les permissions OAuth.
2. Retrouver l'organisation et le membre courants.
3. Vérifier que l'organisation et le membre sont actifs.
4. Charger la politique de modèles applicable à cette organisation.
5. Conserver uniquement les modèles actifs et autorisés.
6. Vérifier que le modèle par défaut appartient à cette liste.
7. Mapper les modèles vers le contrat public sans exposer leur fournisseur.
8. Retourner une liste dans un ordre stable défini par la configuration.

La première version peut utiliser la configuration globale existante si tous
les clients partagent les mêmes modèles. Le contrat doit néanmoins recevoir le
contexte de l'organisation pour permettre une politique par client plus tard
sans modifier l'API.

<a id="models-architecture"></a>
## Architecture

Le traitement respecte le flux obligatoire :

```text
ModelsController
  -> IDispatcher
  -> GetAvailableModelsCommandHandler
  -> service applicatif de catalogue
  -> interface de lecture de la politique des modèles
  -> adapter Infrastructure vers la configuration actuelle
```

Les noms servent à rendre le flux concret; le développeur peut choisir des
noms équivalents cohérents avec le projet.

- Le controller injecte uniquement `IDispatcher`.
- Le handler injecte uniquement une interface de service applicatif.
- Le service applicatif ne dépend pas des classes d'options Infrastructure.
- Un adapter Infrastructure transforme `AiModelsOptions` en contrat applicatif.
- Aucun SDK de fournisseur n'est appelé pour construire cette liste.

<a id="models-errors"></a>
## Erreurs

### `401 Unauthorized`

- token absent, invalide ou expiré

### `403 Forbidden`

- scope ou rôle Entra absent
- organisation ou membre inactif

### `500 Internal Server Error`

- aucun modèle actif
- modèle par défaut absent de la liste active
- configuration incohérente

Une configuration invalide doit aussi empêcher le démarrage du service grâce
à la validation des options déjà présente.

<a id="models-security"></a>
## Sécurité

- Ne jamais retourner l'API key, l'endpoint ou le nom interne d'un déploiement.
- Ne jamais accepter l'organisation depuis le frontend.
- Ne jamais considérer cet endpoint comme l'unique validation du modèle.
- `POST /api/messages` valide de nouveau le modèle demandé.
- Deux organisations peuvent recevoir des listes différentes.

<a id="models-acceptance"></a>
## Critères d'acceptation

- Un membre actif reçoit uniquement les modèles qu'il peut utiliser.
- Le modèle par défaut est présent et identifiable sans ambiguïté.
- Un modèle désactivé n'est pas retourné.
- Aucun secret ni endpoint fournisseur n'est présent dans la réponse.
- Une organisation ne peut pas demander la politique d'une autre organisation.
- Le controller, le handler et les dépendances respectent les tests d'architecture.
- Les tests utilisent `[Theory, AutoDomainData]` ou `[InlineAutoDomainData]`.
- La suite complète `dotnet test Solution.sln` réussit.

<a id="models-references"></a>
## Documentation de référence

- [Interface web — choix du modèle](../frontend/client-interface.md#frontend-model-selection)
- [Envoyer un message — sélection du modèle](../messages/send-message.md#messages-model-selection)
