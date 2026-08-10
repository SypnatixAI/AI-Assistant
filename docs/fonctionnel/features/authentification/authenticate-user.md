# Authenticate User

## Table des matieres

- [But](#but)
- [Quand appeler cet endpoint](#quand-appeler-cet-endpoint)
- [Reponse du frontend](#ce-que-le-frontend-doit-recevoir)
- [Donnees de depart](#donnees-de-depart)
- [Etapes du flow complet](#auth-flow)
- [Provisionnement automatique](#auth-member-provisioning)
- [Derniere connexion](#auth-last-login)
- [Erreurs](#erreurs-a-prevoir)
- [Regles metier](#regles-metier-fixes-pour-cet-endpoint)

## But

`authenticateUser` sert a construire la session applicative complete d'un utilisateur deja connecte.

Le but est simple :
quand le frontend appelle cet endpoint, il doit recevoir toutes les informations necessaires pour ouvrir l'application dans le bon contexte.

Cet endpoint ne fait pas la connexion elle-meme.
Il travaille apres la connexion.

---

## Quand appeler cet endpoint

Le frontend appelle `authenticateUser` :
- juste apres la connexion
- au chargement initial de l'application
- apres un refresh
- quand il faut reconstruire la session utilisateur

---

## Ce que le frontend doit recevoir

La reponse doit permettre au frontend de savoir :
- quel utilisateur est connecte
- a quelle entreprise il appartient
- quel role il a

Exemple de retour :

```json
{
  "user": {
    "id": "a7c2d5b1-9b5d-4a8d-8b9f-2c4d6e8f2001",
    "displayName": "Marc Tremblay",
    "email": "marc.tremblay@josetchibozo2hotmail.onmicrosoft.com"
  },
  "organization": {
    "id": "5e1d4f9a-4e68-4f35-9a9e-7d4d2f6c1001",
    "name": "MetalPro"
  },
  "roles": [
    "User"
  ]
}
```

---

## Donnees de depart

Cet endpoint ne recoit pas de body metier.

Il lit les informations deja presentes dans la session ou dans le token d'authentification.

Le backend doit pouvoir recuperer au minimum :
- l'identifiant externe de l'utilisateur
- l'email
- le nom ou nom affiche
- l'identifiant externe de l'entreprise ou du tenant

---

<a id="auth-flow"></a>
## Etapes de construction fonctionnelle

### 1. Verifier que l'utilisateur est bien connecte

Le backend commence par verifier que la requete vient d'un utilisateur authentifie.

Concretement :
- verifier qu'un token ou une session existe
- verifier que cette session est valide
- verifier que le contexte de securite contient bien une identite utilisateur

Si ce controle echoue :
- retourner `401 Unauthorized`
- ne rien faire de plus

Cette etape sert a eviter de construire une session applicative pour un utilisateur inconnu.

---

### 2. Lire les informations d'identite dans la session

Le backend lit les informations deja disponibles dans le contexte d'authentification.

Concretement, il faut extraire :
- `externalUserId`
- `externalTenantId`
- `email`
- `displayName` ou les champs de nom disponibles

Le but est de recuperer une identite fiable sans demander ces informations au frontend.

Si `externalUserId` manque :
- impossible d'identifier l'utilisateur
- il faut refuser la demande

Si `externalTenantId` manque :
- impossible de savoir a quelle entreprise rattacher la session
- il faut refuser la demande

Le frontend ne doit pas envoyer lui-meme ces valeurs comme source de verite.

---

### 3. Retrouver l'entreprise interne

Le backend utilise `externalTenantId` pour retrouver l'entreprise correspondante dans la plateforme.

Concretement :
- chercher dans la base l'entreprise qui correspond a cet identifiant externe
- charger sa fiche interne
- recuperer son identifiant interne et son nom

Si aucune entreprise n'est trouvee :
- retourner `403 Forbidden`

Cela veut dire :
l'utilisateur est bien connecte chez le fournisseur d'identite, mais son entreprise n'existe pas encore dans la plateforme.

---

### 4. Verifier que l'entreprise est active

Le backend doit confirmer que l'entreprise a le droit d'utiliser la plateforme.

Concretement :
- lire le statut de l'entreprise
- verifier qu'il autorise l'acces

Exemples de statuts qui bloquent :
- suspendue
- desactivee
- archivee

Si l'entreprise n'est pas active :
- retourner `403 Forbidden`
- arreter le traitement

Cette verification doit arriver tot dans le flux pour ne pas charger inutilement le reste.

---

### 5. Chercher l'utilisateur interne avec son identite externe

Le backend verifie si un utilisateur interne existe deja pour cette entreprise.

Concretement :
- chercher un utilisateur avec `externalUserId`
- verifier qu'il appartient bien a l'entreprise trouvee a l'etape precedente

Il faut faire cette recherche dans la bonne organisation pour eviter tout melange entre entreprises.

Si l'utilisateur existe :
- charger son compte interne
- continuer

Si l'utilisateur n'existe pas :
- passer a l'etape suivante pour le creer automatiquement

---

<a id="auth-member-provisioning"></a>
### 6. Creer automatiquement l'utilisateur si le compte n'existe pas

Dans ce projet, la regle est simple :
si l'utilisateur n'existe pas, le backend doit le creer automatiquement.

Concretement, il faut :
- creer un nouvel utilisateur interne
- rattacher cet utilisateur a l'organisation trouvee
- enregistrer son `externalUserId`
- enregistrer son email
- enregistrer son `displayName`
- definir son statut initial a `Actif`
- lui attribuer le role `User` par defaut

Pourquoi le role `User` :
- c'est le role par defaut du systeme
- un utilisateur ne doit pas devenir `Admin` automatiquement a sa premiere connexion

Le point important :
un utilisateur authentifie peut entrer dans la plateforme meme s'il n'existait pas encore en base, parce que la creation automatique est autorisee.

---

### 7. Verifier que le compte utilisateur est actif

Meme si le compte existe ou vient d'etre cree, le backend doit verifier son statut interne.

Concretement :
- lire le statut du compte utilisateur
- verifier qu'il est actif

Si le compte est suspendu ou desactive :
- retourner `403 Forbidden`

Cette etape permet de bloquer un utilisateur qui serait valide dans le systeme externe mais interdit localement dans la plateforme.

---

### 8. Charger le role interne de l'utilisateur

Dans ce systeme, les roles viennent uniquement de la base interne.

Concretement :
- lire le ou les roles stockes pour l'utilisateur
- verifier qu'ils font partie des roles autorises

Les roles possibles sont seulement :
- `Admin`
- `User`

La base interne est la seule source de verite pour les roles.

---

### 9. Construire la reponse finale

Le backend construit ensuite une reponse stable et simple a consommer.

Concretement, la reponse doit contenir :
- `user`
- `organization`
- `roles`

Le champ `user` contient les infos utiles pour afficher et identifier l'utilisateur.
Le champ `organization` contient les infos utiles sur l'entreprise active.
Le champ `roles` contient la liste des roles internes, meme si pour l'instant le systeme reste simple.

Le backend ne doit jamais retourner :
- mot de passe
- secret
- token sensible
- cle API
- donnees d'une autre entreprise

---

<a id="auth-last-login"></a>
### 10. Enregistrer la derniere connexion

Une fois `authenticateUser` termine avec succes, le backend peut enregistrer des informations de suivi.

Concretement :
- mettre a jour la date de derniere connexion
- enregistrer la date du dernier `authenticateUser` reussi
- garder une trace utile pour l'administration ou le support

Cette etape est secondaire, mais utile.

---

### 11. Retourner la reponse au frontend

Si toutes les etapes se passent bien :
- retourner `200 OK`
- envoyer la reponse complete

Le frontend peut alors :
- afficher le nom de l'utilisateur
- savoir quelle entreprise est active
- savoir si l'utilisateur est `Admin` ou `User`

---

## Resume tres simple

`authenticateUser` fait ce travail :
1. verifier la session
2. lire l'identite externe
3. retrouver l'entreprise
4. verifier qu'elle est active
5. retrouver l'utilisateur interne
6. le creer automatiquement s'il n'existe pas
7. verifier qu'il est actif
8. lire son role interne
9. construire la reponse
10. retourner le contexte complet au frontend

---

## Erreurs a prevoir

### 401 Unauthorized

A retourner si :
- l'utilisateur n'est pas connecte
- la session est invalide
- la session est expiree

### 403 Forbidden

A retourner si :
- l'entreprise n'existe pas dans la plateforme
- l'entreprise est suspendue
- l'utilisateur est suspendu
- l'utilisateur ne peut pas etre utilise localement

### 500 Internal Server Error

A retourner si une erreur technique empeche la construction de la session.

---

## Regles metier fixes pour cet endpoint

- le nom fonctionnel de l'endpoint est `authenticateUser`
- l'utilisateur doit deja etre authentifie avant l'appel
- le compte utilisateur est cree automatiquement s'il n'existe pas
- le role par defaut a la creation est `User`
- les roles viennent uniquement de la base interne
- il existe seulement deux roles : `Admin` et `User`
- le frontend ne choisit jamais librement l'organisation
- la reponse ne doit contenir aucune donnee sensible
- toutes les donnees retournees doivent appartenir a l'organisation courante

---
