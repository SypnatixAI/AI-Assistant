# Authenticate User

## Table des matieres

- [But](#but)
- [Quand appeler cet endpoint](#quand-appeler-cet-endpoint)
- [Reponse du frontend](#ce-que-le-frontend-doit-recevoir)
- [Donnees de depart](#donnees-de-depart)
- [Etapes du flow complet](#auth-flow)
- [Provisionnement automatique](#auth-member-provisioning)
- [Autorisation OAuth](#auth-oauth-authorization)
- [Politique d'admission](#auth-admission-policy)
- [Configuration Microsoft Entra](#auth-entra-configuration)
- [Identite stable du membre](#auth-member-identity)
- [Derniere connexion](#auth-last-login)
- [Erreurs](#erreurs-a-prevoir)
- [Regles metier](#regles-metier-fixes-pour-cet-endpoint)
- [References](#references-techniques)

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

<a id="auth-oauth-authorization"></a>
## Autorisation OAuth de l'API

Un token valide prouve l'identite de l'appelant, mais il ne suffit pas a autoriser l'utilisation d'AssistantCore.

Tous les endpoints appeles au nom d'un utilisateur doivent verifier les deux permissions suivantes :

- le scope delegue `access_as_user`
- le role d'admission Entra `AssistantCore.Access`

Ces permissions ont des responsabilites differentes :

| Controle | Signification |
| --- | --- |
| `access_as_user` | L'application cliente peut appeler l'API au nom de l'utilisateur connecte |
| `AssistantCore.Access` | L'utilisateur a ete admis sur la plateforme par son organisation |
| Role interne `Admin` ou `User` | Le membre peut effectuer les actions metier autorisees dans AssistantCore |

Le role Entra `AssistantCore.Access` ne doit jamais etre transforme automatiquement en role interne `Admin`.

Le backend doit verifier, dans cet ordre :

1. la signature, l'emetteur, l'audience et l'expiration du token
2. la presence du scope `access_as_user` dans le claim `scp`
3. la presence de `AssistantCore.Access` dans le claim `roles`
4. la presence des claims `tid` et `oid`
5. l'existence et le statut actif de l'organisation interne
6. l'existence ou le provisionnement du membre interne
7. le statut actif et le role interne du membre

Un token absent ou invalide retourne `401 Unauthorized`.

Un token valide sans le scope ou le role d'admission attendu retourne `403 Forbidden`.

Un token applicatif sans utilisateur ne peut pas appeler les endpoints utilisateur. Les appels de workers ou de jobs utiliseront plus tard des permissions applicatives et des endpoints separes.

---

<a id="auth-admission-policy"></a>
## Politique d'admission et de provisionnement

Pour le MVP, une organisation doit etre creee et activee dans AssistantCore avant que ses utilisateurs puissent acceder a la plateforme.

L'administrateur Microsoft Entra du client decide qui peut entrer dans AssistantCore. Il affecte les utilisateurs ou groupes autorises au role Entra `AssistantCore.Access` de l'Enterprise Application.

L'administrateur AssistantCore gere ensuite les roles metier `Admin` et `User`. Il ne gere pas l'emission des tokens Microsoft.

Un utilisateur peut etre provisionne automatiquement seulement si :

- son token est valide et destine a AssistantCore
- son token contient `access_as_user`
- son token contient `AssistantCore.Access`
- son `tid` correspond a une organisation interne active
- son `oid` identifie un utilisateur supporte
- le membre interne n'existe pas encore pour cette organisation et cet `oid`

Le membre cree automatiquement recoit toujours :

- le role interne `User`
- le statut interne `Active`
- l'organisation determinee depuis `tid`
- l'identite externe determinee depuis `oid`

Les utilisateurs invites peuvent être autorisés dans la première version
seulement s’ils possèdent un objet invité dans le tenant Microsoft Entra du
client et sont affectés explicitement à `AssistantCore.Access`.

Le claim `tid` doit correspondre au tenant client qui héberge l’objet invité.
Le claim `oid` doit correspondre à l’identifiant de cet objet invité dans ce
tenant. L’adresse courriel ou le tenant d’origine de l’invité ne remplace
jamais ces identifiants.

Le retrait de `AssistantCore.Access`, la désactivation du membre interne ou la
suppression de l’objet invité retire l’accès à AssistantCore.

### Retirer l'acces

Pour retirer l'acces a un utilisateur :

1. un administrateur AssistantCore desactive le membre interne pour bloquer les appels dans la plateforme
2. un administrateur Entra retire l'affectation `AssistantCore.Access`
3. l'utilisateur ne peut plus obtenir de nouveau token autorise
4. le membre reste en base pour conserver son historique

Pour un retrait urgent, la desactivation interne doit etre faite en premier, car un token deja emis peut rester valide jusqu'a son expiration.

La désactivation interne utilise
`PATCH /api/members/{memberId}/status`, décrit dans
[Gérer le statut d'un membre](../membres/manage-member-status.md).

---

<a id="auth-entra-configuration"></a>
## Configuration Microsoft Entra etape par etape

Cette configuration comporte une partie geree par Sypnatix dans l'App Registration principale et une partie realisee dans chaque tenant client.

### A. Creer le role d'admission dans l'App Registration

Dans le tenant qui possede l'App Registration AssistantCore :

1. ouvrir `Microsoft Entra admin center`
2. ouvrir `Entra ID`
3. ouvrir `App registrations`
4. selectionner l'App Registration de l'API AssistantCore
5. ouvrir `App roles`
6. cliquer sur `Create app role`
7. utiliser `AssistantCore Access` comme nom affiche
8. selectionner `Users/Groups` dans les types de membres autorises
9. utiliser exactement `AssistantCore.Access` comme valeur
10. ajouter une description indiquant que ce role autorise l'acces a la plateforme sans donner un role metier interne
11. activer le role puis enregistrer

Le role est defini sur l'API afin d'apparaitre dans le token d'acces destine a AssistantCore.

### B. Verifier le scope delegue

Dans la meme App Registration :

1. ouvrir `Expose an API`
2. verifier que l'Application ID URI est configure
3. verifier que le scope `access_as_user` existe et est actif
4. verifier que l'application cliente demande `api://<API_CLIENT_ID>/access_as_user`
5. accorder le consentement administrateur lorsque la politique du tenant l'exige

### C. Activer l'affectation obligatoire dans le tenant client

Un administrateur du tenant client effectue les operations suivantes :

1. ouvrir `Microsoft Entra admin center`
2. ouvrir `Entra ID`
3. ouvrir `Enterprise applications`
4. rechercher et selectionner `AssistantCore`
5. ouvrir `Properties`
6. configurer `Assignment required?` a `Yes`
7. enregistrer

Cette operation evite qu'un utilisateur non affecte utilise l'Enterprise Application uniquement parce qu'il appartient au tenant.

### D. Affecter les utilisateurs ou groupes autorises

Dans l'Enterprise Application AssistantCore du tenant client :

1. ouvrir `Users and groups`
2. cliquer sur `Add user/group`
3. selectionner les utilisateurs ou groupes autorises
4. selectionner le role `AssistantCore.Access`
5. cliquer sur `Assign`
6. verifier que chaque affectation apparait avec le bon role

Ne pas affecter de service principal ou de groupe dont le périmètre n’est pas
maîtrisé. Un compte invité doit être affecté individuellement ou par un groupe
explicitement approuvé.

### E. Verifier le token et le premier acces

Avec un utilisateur de test affecte :

1. demander un token pour le scope `access_as_user`
2. verifier que le token vise l'audience de l'API AssistantCore
3. verifier que `scp` contient `access_as_user`
4. verifier que `roles` contient `AssistantCore.Access`
5. appeler `authenticateUser`
6. verifier que le membre est cree avec le role interne `User`
7. rappeler l'endpoint et verifier qu'aucun doublon n'est cree

Avec un utilisateur non affecte, verifier que l'acces est refuse et qu'aucun membre interne n'est cree.

---

<a id="auth-member-identity"></a>
## Identite stable et profil du membre

La cle d'identite stable d'un membre est composee de :

- l'identifiant interne de l'organisation
- le fournisseur d'identite
- le claim Entra `oid`

Le claim `tid` determine l'organisation candidate. Le claim `oid` identifie l'utilisateur dans ce tenant.

L'email et le nom affiche sont des donnees de profil. Ils ne doivent jamais servir a prendre une decision d'autorisation ou a retrouver seuls un membre.

Lors d'une authentification reussie, le backend peut actualiser le nom et l'email d'un membre existant lorsque les nouvelles valeurs sont valides. Cette synchronisation ne doit jamais modifier :

- le role interne
- le statut interne
- l'organisation
- l'identifiant externe

Un changement d'email ne doit pas creer un second membre. Deux identites externes differentes ne doivent pas etre fusionnees uniquement parce qu'elles presentent le meme email.

La contrainte d'unicite principale doit porter sur l'organisation, le fournisseur d'identite et l'identifiant utilisateur externe.

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

## References techniques

- [Autorisation des API avec Microsoft.Identity.Web](https://learn.microsoft.com/en-us/entra/msidweb/authentication/authorization)
- [Verifier les scopes et roles d'une API protegee](https://learn.microsoft.com/en-us/entra/identity-platform/scenario-protected-web-api-verification-scope-app-roles)
- [Restreindre une application Entra a des utilisateurs affectes](https://learn.microsoft.com/en-us/entra/identity-platform/howto-restrict-your-app-to-a-set-of-users)
- [Ajouter des app roles dans Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity-platform/howto-add-app-roles-in-apps)
- [Claims des access tokens Microsoft Entra](https://learn.microsoft.com/en-us/entra/identity-platform/access-token-claims-reference)
