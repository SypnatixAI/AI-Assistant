#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVICE_PROJECT="$ROOT_DIR/AssistantCore.Service"
WORKER_PROJECT="$ROOT_DIR/AssistantCore.Ingestion.Worker"
API_URL="https://localhost:7292"
API_HEALTH_URL="http://localhost:5043/"
CERTIF_ENVIRONMENT="Certif"
WEBHOOK_BASE_URL="${MICROSOFT365_WEBHOOK_BASE_URL:-https://mounting-product-sternness.ngrok-free.dev}"
API_PID=""
WORKER_PID=""

cleanup() {
    if [[ -n "$WORKER_PID" ]] && kill -0 "$WORKER_PID" 2>/dev/null; then
        kill "$WORKER_PID"
    fi
    if [[ -n "$API_PID" ]] && kill -0 "$API_PID" 2>/dev/null; then
        kill "$API_PID"
    fi
}

trap cleanup EXIT INT TERM

for command_name in docker dotnet curl; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Commande requise introuvable: $command_name" >&2
        exit 1
    fi
done

if [[ ! -f "$ROOT_DIR/.env.database" ]]; then
    echo "Le fichier .env.database est requis. Consulte la section de test local du README." >&2
    exit 1
fi

set -a
source "$ROOT_DIR/.env.database"
set +a

if [[ -z "${SQL_SERVER_PASSWORD:-}" ]]; then
    echo "SQL_SERVER_PASSWORD est requis dans .env.database." >&2
    exit 1
fi

DATABASE_CONNECTION_STRING="Server=localhost,1433;Database=AssistantCoreDb;User Id=sa;Password=${SQL_SERVER_PASSWORD};TrustServerCertificate=True;Encrypt=False"

cd "$ROOT_DIR"

echo "[1/5] Démarrage de SQL Server et application des migrations..."
bash scripts/start-database-stack.sh

echo "[2/5] Chargement de la configuration Certif versionnée..."

echo "[3/5] Compilation de la solution..."
dotnet build Solution.sln

echo "[4/5] Démarrage de l'API sur $API_URL..."
if curl --silent --output /dev/null --max-time 1 "$API_HEALTH_URL"; then
    echo "Un service utilise déjà le port local 5043. Arrête-le avant de relancer ce script." >&2
    exit 1
fi

ASPNETCORE_ENVIRONMENT="$CERTIF_ENVIRONMENT" \
    ASPNETCORE_URLS="https://localhost:7292;http://localhost:5043" \
    ConnectionStrings__AssistantCoreDatabase="$DATABASE_CONNECTION_STRING" \
    Microsoft365__WebhookBaseUrl="$WEBHOOK_BASE_URL" \
    dotnet run --no-build --no-launch-profile --project "$SERVICE_PROJECT" &
API_PID=$!

API_READY=false
for _ in {1..60}; do
    if ! kill -0 "$API_PID" 2>/dev/null; then
        wait "$API_PID"
    fi
    if curl --silent --fail "$API_HEALTH_URL" >/dev/null; then
        API_READY=true
        break
    fi
    sleep 1
done

if [[ "$API_READY" != true ]]; then
    echo "L'API n'a pas répondu après 60 secondes." >&2
    exit 1
fi

echo "[5/5] Démarrage du Worker local..."
DOTNET_ENVIRONMENT="$CERTIF_ENVIRONMENT" \
    DOTNET_CONTENTROOT="$WORKER_PROJECT" \
    ConnectionStrings__AssistantCoreDatabase="$DATABASE_CONNECTION_STRING" \
    Microsoft365__WebhookBaseUrl="$WEBHOOK_BASE_URL" \
    dotnet run --no-build --project "$WORKER_PROJECT" &
WORKER_PID=$!

echo
echo "Environnement connecté aux vrais services prêt. Laisse ce terminal ouvert."
echo "API:     $API_URL"
echo "Webhook: $WEBHOOK_BASE_URL/webhooks/microsoft-graph"
echo "Démarre ngrok séparément si nécessaire: ngrok http $API_URL --url $WEBHOOK_BASE_URL"
echo "SQLPad:  http://localhost:3000"
echo
echo "Dans Postman: AuthenticateUser -> Start Consent -> Register Site -> Get Drives -> Enable Drive -> Send Message"
echo "Utilise Ctrl+C pour arrêter l'API et le Worker."

while kill -0 "$API_PID" 2>/dev/null && kill -0 "$WORKER_PID" 2>/dev/null; do
    sleep 1
done

if ! kill -0 "$API_PID" 2>/dev/null; then
    wait "$API_PID"
else
    wait "$WORKER_PID"
fi
