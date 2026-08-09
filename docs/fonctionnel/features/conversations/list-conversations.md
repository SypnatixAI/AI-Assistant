# Lister les conversations

## But

`GET /api/conversations` permet a un utilisateur authentifie de retrouver
ses conversations recentes avec l'assistant.

La creation d'une conversation reste geree par `POST /api/messages` lorsque
`conversationId` est absent.

Cet endpoint est uniquement un endpoint de lecture. Il ne cree, ne renomme
et ne modifie aucune conversation.

## Route

```http
GET /api/conversations
```

Exemple avec pagination :

```http
GET /api/conversations?limit=25&cursor=eyJ1cGRhdGVkQXQiOi...
```

## Qui peut utiliser cet endpoint

Un membre authentifie peut l'utiliser si :

- son organisation existe et est active
- son compte interne existe et est actif
- son role interne est `Admin` ou `User`

Les roles `Admin` et `User` voient uniquement leurs propres conversations.
Un administrateur ne voit pas automatiquement les conversations des autres
membres.

## Donnees envoyees par le frontend

L'endpoint ne recoit aucun body.

Le frontend ne doit jamais envoyer :

- l'identifiant de l'organisation
- l'identifiant du membre
- un identifiant externe provenant du fournisseur d'identite

Ces valeurs viennent du contexte d'authentification du backend.

## Parametres de pagination

### `limit`

`limit` est optionnel.

- valeur par defaut : `25`
- valeur maximale : `100`
- une valeur inferieure a `1` ou superieure a `100` retourne `400`

### `cursor`

`cursor` est optionnel.

- il est retourne par la page precedente
- il est opaque pour le frontend
- le frontend ne doit pas le construire ou le modifier
- un curseur invalide retourne `400`

Le curseur doit permettre une pagination stable selon :

1. `updatedAt` en ordre decroissant
2. `id` en ordre decroissant pour departager deux dates identiques

Le backend ne doit pas utiliser une pagination basee uniquement sur un
numero de page.

## Exemple de reponse

```json
{
  "conversations": [
    {
      "id": "8d7df699-13f8-4c85-871f-115d049bc697",
      "title": "Politique de teletravail",
      "createdAt": "2026-08-06T20:15:00Z",
      "updatedAt": "2026-08-06T20:18:32Z",
      "lastMessagePreview": "La politique permet jusqu'a deux jours..."
    },
    {
      "id": "2b038fab-0674-46b2-bfd0-ac1cbeb2cb47",
      "title": "Commande 4587",
      "createdAt": "2026-08-05T15:10:00Z",
      "updatedAt": "2026-08-05T15:15:00Z",
      "lastMessagePreview": "La commande est actuellement..."
    }
  ],
  "nextCursor": "eyJ1cGRhdGVkQXQiOi...",
  "hasMore": true
}
```

Pour la derniere page :

```json
{
  "conversations": [],
  "nextCursor": null,
  "hasMore": false
}
```

Une absence de conversation retourne `200 OK` avec un tableau vide.

## Regles des champs retournes

### `title`

Le titre est enregistre sur la conversation.

Lors de la creation par `POST /api/messages`, un premier titre peut etre
derive du premier message utilisateur. Une generation de titre par IA pourra
etre ajoutee plus tard.

`GET /api/conversations` ne doit jamais generer ou enregistrer un titre.

### `updatedAt`

`updatedAt` represente la derniere activite de la conversation.

Il est mis a jour lorsqu'un nouveau message utilisateur ou Assistant est
enregistre. Toutes les dates sont conservees et retournees en UTC.

### `lastMessagePreview`

L'apercu vient du message le plus recent de la conversation, quel que soit
son role.

Le backend :

- retire les espaces inutiles
- retourne uniquement du texte
- limite la longueur avec une valeur configuree
- retourne `null` si aucun message n'existe

L'apercu est construit pendant la lecture. Le GET ne modifie aucune donnee.

## Etapes de traitement

### 1. Verifier l'authentification

- verifier le JWT
- lire l'identifiant externe du tenant et de l'utilisateur
- retourner `401` si l'identite est absente ou invalide

### 2. Retrouver le contexte interne

- retrouver l'organisation
- verifier qu'elle est active
- retrouver le membre selon le flux d'authentification existant
- verifier que le membre est actif

Une organisation ou un membre interdit retourne `403`.

### 3. Valider la pagination

- appliquer la limite par defaut
- refuser une limite hors des valeurs autorisees
- decoder et verifier le curseur lorsqu'il est present

Une pagination invalide retourne `400` sans interroger les conversations.

### 4. Charger les conversations autorisees

La requete doit toujours filtrer avec :

- l'identifiant interne de l'organisation
- l'identifiant interne du membre proprietaire
- le statut visible de la conversation

Les conversations archivees ou supprimees ne sont pas retournees dans la
premiere version.

### 5. Construire les resumes

Pour chaque conversation, retourner uniquement les champs necessaires a la
liste.

Le backend ne doit pas charger l'historique complet des messages pour
construire cette reponse. Il doit recuperer seulement le dernier message
necessaire a l'apercu.

### 6. Construire le prochain curseur

Si d'autres conversations existent :

- construire un curseur opaque a partir du dernier element retourne
- retourner `hasMore = true`

Sinon :

- retourner `nextCursor = null`
- retourner `hasMore = false`

## Securite

- L'organisation et le membre viennent toujours du contexte backend.
- Une conversation d'un autre membre ou tenant ne doit jamais apparaitre.
- Le role Admin ne contourne pas la regle de propriete.
- Aucun token, secret, identifiant externe ou contenu de source n'est retourne.
- Le partage de conversations est hors perimetre de la premiere version.

## Architecture attendue

- Le Controller recoit les parametres et appelle le handler.
- Le handler orchestre la validation et la lecture.
- Le service ou repository applique les filtres de securite et la pagination.
- Le Controller et le handler ne contiennent aucune requete EF Core.

La lecture doit utiliser une projection et ne pas charger toutes les entites
ou tous les messages en memoire.

## Erreurs a prevoir

### `400 Bad Request`

- `limit` est invalide
- `cursor` est invalide

### `401 Unauthorized`

- le token est absent, invalide ou expire
- un claim obligatoire est absent

### `403 Forbidden`

- l'organisation est absente ou inactive
- le membre est inactif ou interdit

### `500 Internal Server Error`

- une erreur technique empeche la lecture

## Hors perimetre

Cette premiere version ne couvre pas :

- le chargement des messages d'une conversation
- la recherche par texte
- les favoris
- l'archivage
- le renommage
- le partage
- la suppression et la purge

Ces comportements auront leurs propres endpoints et documents.

## Criteres d'acceptation

- Un utilisateur voit uniquement ses conversations.
- Les conversations sont triees par activite recente avec un ordre stable.
- La pagination ne charge pas toute la table en memoire.
- Une liste vide retourne `200`.
- Les conversations archivees ou supprimees sont exclues.
- Le dernier message est retourne sous forme d'apercu limite.
- Le GET ne modifie aucune donnee.
- Les tests couvrent plusieurs membres, plusieurs organisations, la
  pagination, les dates identiques et les curseurs invalides.

## Relation avec les autres endpoints

- `POST /api/messages` cree une conversation avec le premier message.
- Un futur `GET /api/conversations/{id}` chargera l'historique et les sources.
- Un futur `PATCH /api/conversations/{id}` permettra de renommer ou archiver.
- Un futur `DELETE /api/conversations/{id}` appliquera la politique de
  suppression et de retention.
