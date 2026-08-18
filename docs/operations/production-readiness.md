# Préparer les environnements de production

## Table des matières

- [But](#production-purpose)
- [Composants](#production-components)
- [Configuration et secrets](#production-configuration)
- [Santé](#production-health)
- [Exemples de santé](#production-health-examples)
- [Déploiement](#production-deployment)
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

Le pipeline construit des artefacts immuables, exécute tests et architecture,
applique Flyway une seule fois, déploie progressivement, effectue les smoke
tests puis permet un rollback compatible avec les migrations.

Ordre obligatoire d’un déploiement :

1. compiler Angular et chaque exécutable .NET avec le SHA du commit;
2. exécuter tests backend, tests Angular, lint et tests d’architecture;
3. publier des artefacts immuables sans secret;
4. valider les références Key Vault et les options de l’environnement;
5. exécuter Flyway une seule fois avec un verrou de déploiement;
6. déployer une instance sans lui envoyer tout le trafic;
7. appeler `live`, `ready`, l’authentification et un smoke test sans contenu client;
8. augmenter progressivement le trafic;
9. arrêter et revenir à l’artefact précédent si une sonde échoue.

Une migration destructive incompatible avec l’ancienne version interdit un
rollback sûr et doit être découpée en plusieurs déploiements additifs.

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

- Un environnement peut être recréé depuis une définition versionnée.
- Secrets, migrations, health checks et rollback sont automatisés.
- Alertes et tableaux de bord couvrent les pannes importantes.
- Une restauration est exécutée et mesurée.
- Les contrôles de sécurité web sont vérifiés après déploiement.
