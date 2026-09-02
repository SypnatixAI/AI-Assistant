# Installer DEV et CERTIF dans Azure Container Apps

## Table des matières

- [But](#azure-setup-purpose)
- [Architecture obtenue](#azure-setup-architecture)
- [Coût attendu](#azure-setup-cost)
- [Informations à choisir](#azure-setup-names)
- [Étape 1 — Créer l’identité GitHub dans Microsoft Entra](#azure-setup-oidc)
- [Étape 2 — Préparer GitHub](#azure-setup-github)
- [Étape 3 — Créer ACR Basic](#azure-setup-acr)
- [Étape 4 — Créer les Key Vault](#azure-setup-key-vault-bootstrap)
- [Étape 5 — Déposer les secrets](#azure-setup-secrets)
- [Étape 6 — Publier les premières images](#azure-setup-first-images)
- [Étape 7 — Créer DEV](#azure-setup-dev)
- [Étape 8 — Créer CERTIF](#azure-setup-certif)
- [Étape 9 — Utiliser les déploiements courants](#azure-setup-deployments)
- [Étape 10 — Démarrer et arrêter CERTIF](#azure-setup-control-certif)
- [Étape 11 — Produire un release candidate](#azure-setup-release-candidate)
- [Vérifications et problèmes fréquents](#azure-setup-troubleshooting)
- [Références](#azure-setup-references)

<a id="azure-setup-purpose"></a>
## But

Créer deux environnements isolés et reproductibles :

- DEV utilise WireMock et l’authentification JWT locale;
- CERTIF utilise Microsoft Entra, Microsoft Graph, OpenAI et Azure AI Search;
- chaque environnement possède son serveur et sa base Azure SQL, son Key Vault,
  ses identités managées et ses Container Apps;
- les deux environnements partagent uniquement un ACR Basic;
- un merge déploie automatiquement DEV après la réussite du CI;
- une action manuelle promeut vers CERTIF exactement les images validées en DEV;
- CERTIF peut être arrêté entre deux séances;
- un manifeste de release candidate peut figer les digests destinés à une future PROD.

<a id="azure-setup-architecture"></a>
## Architecture obtenue

```text
ACR Basic partagé
├── assistant-api:sha-...
├── assistant-worker:sha-...
├── assistant-migrations:sha-...
├── assistant-wiremock:sha-...
└── assistant-spa:sha-...

DEV
├── Container Apps Environment
│   ├── SPA ───────────────┐
│   ├── API                ├── WireMock
│   └── Worker ────────────┘
├── Job Flyway
├── Key Vault DEV
└── Serveur Azure SQL DEV / AssistantCoreDb

CERTIF
├── Container Apps Environment
│   ├── SPA
│   ├── API
│   └── Worker, 0 réplique hors utilisation
├── Job Flyway
├── Key Vault CERTIF
└── Serveur Azure SQL CERTIF / AssistantCoreDb
```

Les images ne contiennent aucune configuration d’environnement. Les variables
des Container Apps alimentent .NET et génèrent le fichier public Angular au
démarrage du conteneur. Les secrets restent dans Key Vault et sont lus par
identité managée.

<a id="azure-setup-cost"></a>
## Coût attendu

- ACR Basic est le coût fixe principal : environ 0,1666 USD par jour, donc
  autour de 5 USD par mois avant taxes et conversion. Vérifier le prix affiché
  pour la région et l’abonnement avant de créer la ressource.
- Azure Container Apps offre une allocation mensuelle gratuite au niveau de
  l’abonnement. Les applications sont configurées avec `minReplicas: 0` et une
  seule réplique maximale afin de rester adaptées à un environnement de test.
- Les bases utilisent Azure SQL serverless avec `useFreeLimit: true`, une pause
  après 60 minutes et `freeLimitExhaustionBehavior: AutoPause`. La création
  échouera si l’abonnement n’est pas admissible à cette offre.
- Key Vault facture surtout les opérations. Le volume d’un environnement de
  test est normalement faible.
- Les appels réels de CERTIF à OpenAI, Azure AI Search et Microsoft 365 ne font
  pas partie de l’allocation Container Apps.

Mettre `minReplicas` à zéro arrête la consommation de calcul de l’application,
mais ne supprime pas ACR, SQL, Key Vault ni leurs éventuels coûts fixes.

<a id="azure-setup-names"></a>
## Informations à choisir

Choisir un suffixe unique de 3 à 8 caractères, en minuscules et sans tiret. Il
sert notamment au nom global de l’ACR. Exemple : `gioai01`.

Les noms recommandés sont :

| Élément | Valeur d’exemple |
| --- | --- |
| Région | `canadacentral` |
| Suffixe | `gioai01` |
| Groupe partagé | `rg-assistant-shared` |
| Groupe DEV | `rg-assistant-dev` |
| Groupe CERTIF | `rg-assistant-certif` |
| ACR | `acrassistantgioai01` |
| Key Vault DEV | `kv-assistant-dev-gioai01` |
| Key Vault CERTIF | `kv-assistant-certif-gioai01` |

Le suffixe doit rester identique dans les deux dépôts et tous les workflows.

<a id="azure-setup-oidc"></a>
## Étape 1 — Créer l’identité GitHub dans Microsoft Entra

Cette identité permet à GitHub Actions de recevoir un jeton Azure temporaire.
Aucun Client Secret Azure permanent n’est créé.

1. Dans Azure Portal, ouvrir **Microsoft Entra ID**.
2. Ouvrir **App registrations**, puis **New registration**.
3. Nommer l’application `github-assistant-deploy`.
4. Garder **Accounts in this organizational directory only**.
5. Ne pas saisir de Redirect URI, puis sélectionner **Register**.
6. Dans **Overview**, conserver :
   - **Application (client) ID**;
   - **Directory (tenant) ID**.
7. Ouvrir **Certificates & secrets**, puis **Federated credentials**.
8. Sélectionner **Add credential** puis **GitHub Actions deploying Azure resources**.
9. Créer quatre identifiants fédérés :

| Dépôt | Entity type | GitHub environment |
| --- | --- | --- |
| dépôt BFF | Environment | `dev` |
| dépôt BFF | Environment | `certif` |
| dépôt SPA | Environment | `dev` |
| dépôt SPA | Environment | `certif` |

GitHub génère un sujet distinct pour chaque combinaison dépôt/environnement.
Ne pas choisir le type `Branch`, car les jobs utilisent les GitHub Environments
`dev` et `certif`.

### Attribuer les rôles Azure

Au niveau de chaque groupe de ressources, ouvrir **Access control (IAM)**,
**Add role assignment**, puis sélectionner l’application
`github-assistant-deploy`.

Attribuer :

- `Contributor` sur les groupes partagé, DEV et CERTIF;
- `User Access Administrator` sur les mêmes groupes, car Bicep crée les rôles
  `AcrPull` et `Key Vault Secrets User` des identités managées;
- après création de l’ACR, `AcrPush` sur l’ACR;
- après création de chaque Key Vault, `Key Vault Secrets User` sur ce coffre.

Dans une organisation plus stricte, remplacer ces rôles larges de bootstrap par
des rôles personnalisés une fois l’installation terminée.

<a id="azure-setup-github"></a>
## Étape 2 — Préparer GitHub

Répéter cette préparation dans le dépôt BFF et le dépôt SPA.

### Créer les environnements

1. Ouvrir le dépôt GitHub.
2. Aller dans **Settings**, **Environments**.
3. Créer `dev`.
4. Créer `certif`.
5. Dans `certif`, ajouter les approbateurs voulus dans **Required reviewers**.
6. Limiter les branches de déploiement à la branche par défaut si la politique
   de l’organisation le permet.

### Créer les variables

Dans **Settings**, **Secrets and variables**, **Actions**, onglet **Variables**,
ajouter les variables suivantes dans les deux dépôts :

| Variable | Valeur |
| --- | --- |
| `AZURE_CLIENT_ID` | Application ID créée à l’étape 1 |
| `AZURE_TENANT_ID` | Directory ID |
| `AZURE_SUBSCRIPTION_ID` | ID de l’abonnement Azure |
| `AZURE_NAME_SUFFIX` | suffixe choisi, par exemple `gioai01` |
| `AZURE_ACR_NAME` | par exemple `acrassistantgioai01` |
| `AZURE_SHARED_RESOURCE_GROUP` | `rg-assistant-shared` |
| `AZURE_DEV_RESOURCE_GROUP` | `rg-assistant-dev` |
| `AZURE_CERTIF_RESOURCE_GROUP` | `rg-assistant-certif` |

Ces identifiants et noms ne sont pas des secrets. Ne créer aucun
`AZURE_CLIENT_SECRET`.

Le BFF déploie automatiquement depuis `master`; la SPA déploie automatiquement
depuis `main`, conformément aux branches par défaut actuelles.

<a id="azure-setup-acr"></a>
## Étape 3 — Créer ACR Basic

Dans le dépôt BFF :

1. Ouvrir **Actions**.
2. Choisir **Provision Azure infrastructure**.
3. Sélectionner **Run workflow**.
4. Choisir :
   - operation : `bootstrap-shared`;
   - environment_name : `dev` — cette valeur sert seulement à sélectionner
     l’identité OIDC pendant ce bootstrap.
5. Lancer le workflow.

Le workflow crée le groupe partagé s’il manque, puis applique
`deploy/infra/bootstrap-shared.bicep`. Le registre obtenu est en SKU Basic,
sans compte administrateur et sans accès anonyme.

Quand le workflow est terminé :

1. Dans Azure Portal, ouvrir **Container registries** puis le nouvel ACR.
2. Ouvrir **Access control (IAM)**.
3. Attribuer `AcrPush` à `github-assistant-deploy`.

Les Container Apps n’utilisent pas ce rôle. Bicep leur crée une identité
distincte limitée à `AcrPull`.

<a id="azure-setup-key-vault-bootstrap"></a>
## Étape 4 — Créer les Key Vault

Dans **Actions**, **Provision Azure infrastructure**, lancer une première fois :

- operation : `bootstrap-environment`;
- environment_name : `dev`.

Relancer ensuite avec `certif`.

Pour chaque coffre :

1. Azure Portal, **Key vaults**, ouvrir le coffre.
2. Ouvrir **Access control (IAM)**.
3. Attribuer `Key Vault Secrets User` à `github-assistant-deploy`.
4. Attribuer `Key Vault Secrets Officer` à la personne qui déposera et fera
   tourner les secrets.

Le paramètre **Azure Resource Manager for template deployment** est activé par
Bicep. Il permet au déploiement de transmettre le mot de passe SQL à un module
sécurisé sans l’écrire dans GitHub ou dans un fichier de paramètres.

<a id="azure-setup-secrets"></a>
## Étape 5 — Déposer les secrets

Dans Azure Portal, ouvrir le Key Vault, puis **Objects**, **Secrets**,
**Generate/Import**. Le nom doit correspondre exactement aux tableaux suivants.

### Secrets DEV

| Nom | Contenu |
| --- | --- |
| `sql-admin-password` | mot de passe SQL robuste et unique pour DEV |
| `dev-jwt-signing-key` | valeur aléatoire longue, réservée au JWT fictif DEV |

### Secrets CERTIF

| Nom | Contenu |
| --- | --- |
| `sql-admin-password` | mot de passe SQL différent de DEV |
| `microsoft365-client-secret` | secret de l’application Microsoft 365 CERTIF |
| `openai-api-key` | clé OpenAI autorisée pour CERTIF |
| `azure-search-api-key` | clé Azure AI Search autorisée pour CERTIF |

Ne jamais copier ces valeurs dans :

- un fichier `appsettings*.json`;
- un fichier `.bicepparam`;
- une variable ou un secret GitHub;
- une commande enregistrée dans l’historique du terminal;
- une image Docker.

Le déploiement crée ensuite automatiquement
`database-connection-string` dans le même Key Vault. Ce secret est calculé à
partir du serveur SQL de l’environnement.

<a id="azure-setup-first-images"></a>
## Étape 6 — Publier les premières images

Les Container Apps exigent une image existante lors de leur première création.
Il faut donc publier une première version avant de lancer `main.bicep`.

1. Fusionner ou pousser une modification validée sur `master` dans le BFF.
2. Attendre la réussite du workflow **CI**.
3. Le workflow **Deploy DEV** construit et publie quatre images dans ACR.
4. Comme l’API n’existe pas encore, il s’arrête proprement après la publication
   et affiche le tag `sha-...` dans le résumé.
5. Fusionner ou pousser une modification validée sur `main` dans la SPA.
6. Attendre **CI**, puis **Deploy SPA DEV**.
7. Noter son tag `sha-...` dans le résumé.

Les deux tags utilisent 40 caractères de SHA. Ils peuvent être différents,
car le BFF et la SPA sont dans des dépôts distincts.

<a id="azure-setup-dev"></a>
## Étape 7 — Créer DEV

Dans le dépôt BFF, ouvrir **Provision Azure infrastructure** et saisir :

- operation : `deploy-environment`;
- environment_name : `dev`;
- backend_image_tag : le tag BFF noté à l’étape 6;
- spa_image_tag : le tag SPA noté à l’étape 6.

Le workflow exécute d’abord `what-if`, puis crée :

- le serveur et la base Azure SQL DEV;
- le Container Apps Environment;
- les identités managées;
- l’API, la SPA, le worker et WireMock;
- le job manuel Flyway;
- les références vers les secrets du Key Vault.

Une fois le workflow réussi :

1. Relancer **Deploy DEV** dans le BFF. Il exécute Flyway, puis déploie et sonde
   l’API.
2. Relancer **Deploy SPA DEV** dans la SPA. Il déploie la SPA et vérifie le
   fichier `assets/config/config.json` généré au démarrage.
3. Dans Azure Portal, ouvrir **Container Apps**, puis
   `ca-assistant-spa-dev`, **Application Url**.

Dans DEV, le fichier Angular généré contient :

```json
{
  "authenticationMode": "LocalJwt",
  "launchMode": "Dev"
}
```

L’URL API et l’URL WireMock sont injectées par Bicep. Aucun `localhost` n’est
présent dans la configuration Azure.

<a id="azure-setup-certif"></a>
## Étape 8 — Créer CERTIF

Utiliser des images déjà validées en DEV. Dans **Provision Azure
infrastructure**, saisir :

- operation : `deploy-environment`;
- environment_name : `certif`;
- backend_image_tag : le tag BFF actuellement validé en DEV;
- spa_image_tag : le tag SPA actuellement validé en DEV.

Après la création, lancer manuellement :

1. **Promote backend to CERTIF** dans le BFF avec le tag backend;
2. **Promote SPA to CERTIF** dans la SPA avec le tag SPA.

Les workflows vérifient que chaque image existe dans ACR et correspond à la
révision courante de DEV. Ils ne reconstruisent rien.

Dans CERTIF, le fichier Angular généré contient :

```json
{
  "authenticationMode": "MicrosoftEntra",
  "launchMode": "Certification"
}
```

WireMock n’est pas créé dans cet environnement. Les URL réelles viennent de
`appsettings.Certif.json` et des variables non sensibles Bicep; les clés et le
Client Secret viennent de Key Vault.

<a id="azure-setup-deployments"></a>
## Étape 9 — Utiliser les déploiements courants

### DEV automatique

Après chaque CI réussi sur la branche par défaut :

- le BFF construit les images API, worker, migrations et WireMock;
- la SPA construit son image;
- les images reçoivent `sha-<Git SHA complet>`;
- Flyway doit réussir avant la mise à jour du BFF;
- la santé de l’API et le chargement de la SPA sont vérifiés.

### CERTIF manuel

Dans **Actions**, choisir le workflow de promotion du dépôt concerné et saisir
le tag déjà validé en DEV. L’environnement GitHub `certif` peut demander une
approbation humaine avant le début du job.

Une promotion ne déplace pas un tag et ne crée pas `latest`. Elle réutilise le
même manifeste d’image ACR.

<a id="azure-setup-control-certif"></a>
## Étape 10 — Démarrer et arrêter CERTIF

Dans le dépôt BFF, ouvrir **Start or stop CERTIF**.

Avant une séance complète :

- action : `start`;
- scope : `all`.

Le workflow active les ingress de l’API et de la SPA et met le worker à une
réplique.

Après la séance :

- action : `stop`;
- scope : `all`.

Le workflow désactive les ingress publics et remet le worker, l’API et la SPA à
zéro réplique. Pour ne contrôler que le coût continu du worker, choisir le
scope `worker`.

Azure SQL se remet en pause après sa période d’inactivité. Le premier appel
suivant peut donc prendre plus de temps pendant la reprise de la base et le
démarrage à froid des conteneurs.

<a id="azure-setup-release-candidate"></a>
## Étape 11 — Produire un release candidate

Dans le dépôt BFF, ouvrir **Create release candidate** et fournir :

- le tag backend validé en DEV;
- le tag SPA validé en DEV;
- un nom comme `rc-2026.09.1`.

Le workflow résout chaque tag en digest ACR et produit un artefact YAML :

```yaml
schemaVersion: 1
releaseCandidate: rc-2026.09.1
sourceEnvironment: dev
images:
  api: acrassistant...azurecr.io/assistant-api@sha256:...
  worker: acrassistant...azurecr.io/assistant-worker@sha256:...
  migrations: acrassistant...azurecr.io/assistant-migrations@sha256:...
  spa: acrassistant...azurecr.io/assistant-spa@sha256:...
```

Une future PROD devra consommer ces digests exacts. Elle ne devra pas
reconstruire les images du candidat.

<a id="azure-setup-troubleshooting"></a>
## Vérifications et problèmes fréquents

### Échec OIDC

Vérifier que le credential fédéré correspond au bon dépôt et au bon GitHub
Environment. Un credential de type `Branch` ne correspond pas à un job qui
déclare `environment: dev`.

### Refus lors du push ACR

Vérifier le rôle `AcrPush` de `github-assistant-deploy` sur l’ACR. Ne pas activer
le compte administrateur pour contourner l’erreur.

### Secret Key Vault introuvable

Vérifier le nom exact, le coffre du bon environnement et le rôle
`Key Vault Secrets User`. Une référence de secret sans version récupère la
version active la plus récente lors d’un redémarrage ou d’une nouvelle révision.

### Échec Bicep pendant `getSecret`

Vérifier :

- que `sql-admin-password` existe;
- que **Azure Resource Manager for template deployment** est activé;
- que l’identité GitHub a le droit de lire les secrets du coffre.

### Échec Flyway

La nouvelle révision applicative n’est pas déployée. Ouvrir le job
`caj-assistant-migrations-<environnement>`, puis **Execution history** et ses
logs. Corriger la migration avec une nouvelle migration additive; ne pas
modifier une migration déjà appliquée.

### CERTIF ne répond pas après un arrêt

Lancer **Start or stop CERTIF** avec `start` et `all`. Un simple passage à zéro
ne réactive pas un ingress qui a été explicitement désactivé.

### Validation locale des fichiers Bicep

Installer Azure CLI avec le composant Bicep, puis exécuter :

```bash
az bicep build --file deploy/infra/bootstrap-shared.bicep
az bicep build --file deploy/infra/bootstrap-environment.bicep
az bicep build --file deploy/infra/main.bicep
```

Les trois templates doivent compiler sans erreur avant le premier `what-if`
Azure. Cette validation a aussi été effectuée avec Bicep CLI 0.46.1 lors de leur
mise en place initiale.

<a id="azure-setup-references"></a>
## Références

- [Azure Container Apps avec Bicep](https://learn.microsoft.com/azure/container-apps/azure-resource-manager-api-spec)
- [Secrets Key Vault dans Container Apps](https://learn.microsoft.com/azure/container-apps/manage-secrets)
- [Tirer une image avec une identité managée](https://learn.microsoft.com/azure/container-apps/managed-identity-image-pull)
- [Mise à l’échelle de Container Apps](https://learn.microsoft.com/azure/container-apps/scale-app)
- [Jobs Azure Container Apps](https://learn.microsoft.com/azure/container-apps/jobs)
- [Images et tags ACR](https://learn.microsoft.com/azure/container-registry/container-registry-image-tag-version)
- [OIDC GitHub vers Azure](https://docs.github.com/actions/security-for-github-actions/security-hardening-your-deployments/configuring-openid-connect-in-azure)
- [GitHub Environments](https://docs.github.com/actions/reference/workflows-and-actions/deployments-and-environments)
