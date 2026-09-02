# Préparer les environnements de production

## Table des matières

- [But](#production-purpose)
- [Composants](#production-components)
- [Configuration et secrets](#production-configuration)
- [Santé](#production-health)
- [Exemples de santé](#production-health-examples)
- [Déploiement](#production-deployment)
  - [Structure versionnée](#production-deployment-structure)
  - [Environnements](#production-deployment-environments)
  - [Images immuables](#production-deployment-images)
  - [Déploiement DEV simulé](#production-deployment-dev)
  - [Déploiement CERTIF réel et manuel](#production-deployment-certification)
  - [Secrets](#production-deployment-secrets)
  - [Échecs et retour à la version précédente](#production-deployment-rollback)
- [Observabilité](#production-observability)
- [Sauvegarde et reprise](#production-backup)
- [Sécurité web](#production-web-security)
- [Critères d'acceptation](#production-acceptance)

<a id="production-purpose"></a>
## But

Rendre Angular, l'API, les workers, les webhooks et leurs données déployables,
observables et récupérables sans dépendre d'une procédure implicite.

<a id="production-components"></a>
## Composants

Chaque environnement définit les hôtes, régions, dépendances et responsabilités
pour Angular, API, worker d'ingestion, webhooks, SQL Server, Azure AI Search et
stockage distribué. Les données de production restent séparées des tests.

La première étape d'hébergement couvre uniquement DEV et CERTIF. La production
reste hors périmètre jusqu'à ce que ces deux environnements soient validés.

<a id="production-configuration"></a>
## Configuration et secrets

Les valeurs non sensibles sont versionnées par environnement. Les secrets sont
chargés depuis Key Vault avec identité managée. Le démarrage échoue clairement
si une valeur obligatoire manque. Aucun secret n'est présent dans le bundle Angular.

<a id="production-health"></a>
## Santé

```http
GET /health/live
GET /health/ready
```

`live` vérifie le processus sans appeler Internet. `ready` vérifie uniquement
les dépendances indispensables pour recevoir du trafic, avec délais courts.
Les détails sensibles ne sont pas publics.

<a id="production-health-examples"></a>
## Exemples de santé

Réponse publique lorsque l’API peut recevoir du trafic :

```json
{
  "status": "Healthy",
  "checks": {
    "sql": "Healthy",
    "distributedStore": "Healthy"
  }
}
```

Une panne SQL retourne `503 Service Unavailable`. La réponse publique ne
contient ni chaîne de connexion, ni nom de serveur, ni exception. Azure AI
Search et les fournisseurs IA sont observés séparément et ne rendent pas l’API
non prête si le chat peut retourner une erreur dégradée contrôlée.

Valeurs initiales : délai maximal de deux secondes par check et cinq secondes
pour la sonde complète. Ces valeurs sont configurables.

<a id="production-deployment"></a>
## Déploiement

Le pipeline construit des artefacts immuables, exécute les tests, applique
Flyway une seule fois, déploie l'application et vérifie sa santé avant de
considérer le déploiement comme réussi.

Une migration destructive incompatible avec l'ancienne version interdit un
retour sûr à la version précédente. Elle doit être découpée en plusieurs
migrations additives compatibles avec les deux versions de l'application.

<a id="production-deployment-structure"></a>
### Structure versionnée

Le dépôt utilise une définition commune pour les ressources Azure et un fichier
de paramètres par environnement :

```text
deploy/
├── docker/
│   ├── api.Dockerfile
│   ├── worker.Dockerfile
│   └── wiremock.Dockerfile
├── infra/
│   ├── bootstrap-shared.bicep
│   ├── bootstrap-environment.bicep
│   ├── main.bicep
│   └── modules/
└── environments/
    ├── shared.bicepparam
    ├── dev.bootstrap.bicepparam
    ├── dev.bicepparam
    ├── certif.bootstrap.bicepparam
    └── certif.bicepparam

.github/workflows/
├── provision-azure.yml
├── deploy-dev.yml
├── promote-certif.yml
├── control-certif.yml
└── release-candidate.yml
```

`main.bicep` décrit la structure commune. Les fichiers `.bicepparam`
contiennent uniquement les différences non sensibles entre DEV et CERTIF.
Aucun fichier ou workflow PROD n'est créé pendant cette étape.

<a id="production-deployment-environments"></a>
### Environnements

DEV et CERTIF possèdent chacun :

- leur propre base Azure SQL;
- leur propre configuration;
- leur propre Key Vault;
- leur propre identité managée;
- leurs propres données;
- leur propre déploiement de l'API et du worker.

Chaque base utilise aussi son propre serveur logique Azure SQL. Les chaînes de
connexion et les droits restent séparés : une application ne peut accéder qu'à
la base de son environnement. Les deux bases utilisent la limite gratuite
serverless et se mettent en pause automatiquement après une heure d'inactivité.

Le développement local conserve SQL Server dans Docker. Dans Azure, DEV et
CERTIF utilisent de vraies bases Azure SQL afin de valider Flyway, les
contraintes, les transactions, les repositories et la persistance après un
redémarrage.

<a id="production-deployment-images"></a>
### Images immuables

Un ACR Basic partagé stocke les images de DEV et CERTIF. Son compte
administrateur est désactivé. Les Container Apps tirent les images avec une
identité managée ayant seulement le rôle `AcrPull`.

L'API, le worker, Flyway, WireMock et la SPA sont publiés avec un tag basé sur
le SHA complet du commit :

```text
acrassistant<suffix>.azurecr.io/assistant-api:sha-<40 caractères>
acrassistant<suffix>.azurecr.io/assistant-worker:sha-<40 caractères>
acrassistant<suffix>.azurecr.io/assistant-migrations:sha-<40 caractères>
acrassistant<suffix>.azurecr.io/assistant-wiremock:sha-<40 caractères>
acrassistant<suffix>.azurecr.io/assistant-spa:sha-<40 caractères>
```

Une image n'est jamais reconstruite lors de sa promotion. Le même SHA passe de
DEV à CERTIF. Le tag `latest` ne sert jamais de référence de déploiement.

<a id="production-deployment-dev"></a>
### Déploiement DEV simulé

DEV reprend le comportement de `scripts/start-local-wiremock.sh`. Les services
externes sont appelés à travers une instance WireMock déployée uniquement dans
l'environnement DEV.

WireMock simule :

- l'authentification locale avec JWT;
- l'autorité Microsoft;
- Microsoft Graph;
- OpenAI;
- les embeddings;
- Azure AI Search.

L'API et le worker utilisent l'adresse interne de WireMock, jamais
`localhost`. Azure SQL n'est pas simulé : Flyway et l'application utilisent
la vraie base `AssistantCoreDb` du serveur DEV.

Le mode d'authentification locale, les réponses WireMock et les données DEV
restent exclusivement fictifs. La configuration doit empêcher le mode simulé
d'être activé dans CERTIF.

Une pull request exécute la compilation et les tests, sans déployer
d'environnement. Un push dans `master`, normalement produit par un merge,
déclenche :

1. la compilation et les tests;
2. la construction des images immuables;
3. leur publication avec le SHA du commit;
4. la migration de `assistantcore-dev`;
5. le déploiement automatique de DEV;
6. les appels à `/health/live` et `/health/ready`;
7. les smoke tests contre les services simulés.

Si Flyway échoue, la nouvelle version de l'API et du worker n'est pas déployée.

<a id="production-deployment-certification"></a>
### Déploiement CERTIF réel et manuel

CERTIF utilise les véritables services Microsoft Entra, Microsoft Graph,
OpenAI et Azure AI Search. Il utilise la base `AssistantCoreDb` de son serveur
Azure SQL CERTIF et uniquement des données de certification autorisées.

Le déploiement CERTIF est déclenché manuellement avec `workflow_dispatch`.
L'utilisateur fournit le tag d'une image déjà déployée et validée dans DEV.

Le workflow CERTIF ne reconstruit aucune image. Il :

1. vérifie que le tag immuable existe;
2. vérifie que l'image a déjà été déployée avec succès dans DEV;
3. exécute Flyway sur `assistantcore-certif`;
4. déploie exactement les mêmes images;
5. appelle `/health/live` et `/health/ready`;
6. exécute les smoke tests avec les véritables intégrations.

Le déclenchement manuel constitue la décision de promotion. Une protection
GitHub Environment peut exiger une approbation supplémentaire si l'équipe le
souhaite.

Le workflow `control-certif.yml` met le worker à une réplique seulement pendant
une séance de certification et le remet à zéro après. Pour arrêter tout
l'environnement, il désactive aussi les ingress publics de l'API et de la SPA.

Le workflow `release-candidate.yml` produit un manifeste YAML contenant les
digests ACR exacts déjà validés en DEV. Ce manifeste est l'entrée d'une future
promotion PROD; il ne reconstruit aucune image.

<a id="production-deployment-secrets"></a>
### Secrets

DEV et CERTIF possèdent des Key Vault séparés.

DEV conserve dans son coffre la chaîne Azure SQL et la clé de signature du JWT
de développement. Les valeurs factices utilisées avec WireMock ne sont jamais
utilisables dans CERTIF.

CERTIF conserve les véritables secrets Microsoft 365, OpenAI, Azure AI Search
et Azure SQL.

Les Container Apps accèdent uniquement au coffre de leur environnement avec
une identité managée et le rôle minimal nécessaire pour lire les secrets. Aucun
mot de passe, token, Client Secret, API key ou chaîne de connexion n'est stocké
dans Git, les fichiers Bicep, les workflows ou les images.

GitHub Actions se connecte à Azure avec OIDC et des jetons temporaires. Aucun
Client Secret Azure permanent n'est stocké dans GitHub.

<a id="production-deployment-rollback"></a>
### Échecs et retour à la version précédente

La nouvelle révision ne doit pas être considérée comme valide avant la réussite
des migrations, des sondes et des smoke tests. En cas d'échec après le
déploiement, le workflow réactive ou redéploie le dernier SHA fonctionnel.

Le retour applicatif utilise une image déjà publiée. Il ne reconstruit pas
l'ancien commit. Flyway n'exécute jamais automatiquement une migration
destructive inverse.

<a id="production-observability"></a>
## Observabilité

Logs structurés, métriques et traces partagent un correlation ID. Mesurer au
minimum erreurs HTTP, latence, disponibilité, files de worker, appels externes,
rate limits et échecs de purge. Aucun contenu utilisateur ou token n'est journalisé.

Tableau de bord minimal :

| Signal | Alerte initiale | Action attendue |
| --- | --- | --- |
| réponses HTTP 5xx | plus de 5 % pendant 5 minutes | vérifier traces et dépendances |
| file worker | plus de 1 000 messages pendant 10 minutes | vérifier workers et throttling |
| oldest message age | plus de 15 minutes | augmenter capacité ou corriger le blocage |
| échecs de purge permanents | au moins 1 | intervention sécurité/opérations |
| readiness | 3 échecs consécutifs | retirer l’instance du trafic |

Les seuils sont configurables par environnement. Les labels de métriques ne
contiennent ni utilisateur, ni courriel, ni texte de conversation.

<a id="production-backup"></a>
## Sauvegarde et reprise

Définir RPO/RTO, sauvegardes SQL chiffrées, rétention, restauration testée et
procédure de reconstruction des index. Une sauvegarde non restaurée en test
n'est pas considérée comme validée.

Valeurs initiales à confirmer avant production : RPO SQL de 15 minutes et RTO
de 4 heures. Un exercice restaure une sauvegarde dans un environnement isolé,
applique les migrations, vérifie les nombres d’organisations, membres et
conversations, puis exécute les smoke tests. Azure AI Search est reconstruit à
partir des sources et checkpoints; il n’est pas considéré comme la copie
unique des données.

<a id="production-web-security"></a>
## Sécurité web

Forcer HTTPS, HSTS en production, CSP adaptée à Angular/MSAL, en-têtes contre
le sniffing et framing, CORS restrictif et dépendances scannées. Les redirect
URIs Entra correspondent exactement aux domaines déployés.

<a id="production-acceptance"></a>
## Critères d'acceptation

- DEV et CERTIF peuvent être recréés depuis une définition versionnée.
- DEV utilise les services externes simulés et une vraie base Azure SQL.
- CERTIF utilise ses véritables intégrations et une base Azure SQL séparée.
- Un merge dans `master` déploie uniquement DEV.
- CERTIF est déployé manuellement avec le même SHA validé dans DEV.
- Aucun fichier ou workflow PROD n'est créé pendant cette étape.
- Secrets, migrations, health checks et retour à la version précédente sont automatisés.
- Alertes et tableaux de bord couvrent les pannes importantes.
- Une restauration est exécutée et mesurée.
- Les contrôles de sécurité web sont vérifiés après déploiement.
