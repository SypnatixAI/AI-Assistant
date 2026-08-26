#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVICE_PROJECT="$ROOT_DIR/AssistantCore.Service"
WORKER_PROJECT="$ROOT_DIR/AssistantCore.Ingestion.Worker"
API_URL="https://localhost:7292"
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

echo "[2/5] Configuration des valeurs locales non sensibles..."
dotnet user-secrets --project "$SERVICE_PROJECT" set \
    "Microsoft365:ConsentCallbackUrl" \
    "$API_URL/api/microsoft365/consent/callback" >/dev/null
dotnet user-secrets --project "$SERVICE_PROJECT" set \
    "Microsoft365Worker:MaintenanceIntervalSeconds" \
    "5" >/dev/null
dotnet user-secrets --project "$SERVICE_PROJECT" set \
    "AzureSearch:EnsureIndexOnStartup" \
    "true" >/dev/null

echo "[3/5] Compilation de la solution..."
dotnet build Solution.sln

echo "[4/5] Démarrage de l'API sur $API_URL..."
ConnectionStrings__AssistantCoreDatabase="$DATABASE_CONNECTION_STRING" \
    dotnet run --no-build --project "$SERVICE_PROJECT" --launch-profile https &
API_PID=$!

API_READY=false
for _ in {1..60}; do
    if curl --silent --fail --insecure "$API_URL/swagger/index.html" >/dev/null; then
        API_READY=true
        break
    fi
    if ! kill -0 "$API_PID" 2>/dev/null; then
        wait "$API_PID"
    fi
    sleep 1
done

if [[ "$API_READY" != true ]]; then
    echo "L'API n'a pas répondu après 60 secondes." >&2
    exit 1
fi

echo "[5/5] Démarrage du Worker local..."
DOTNET_ENVIRONMENT=Development \
    DOTNET_CONTENTROOT="$WORKER_PROJECT" \
    ConnectionStrings__AssistantCoreDatabase="$DATABASE_CONNECTION_STRING" \
    dotnet run --no-build --project "$WORKER_PROJECT" &
WORKER_PID=$!

echo
echo "Environnement prêt. Laisse ce terminal ouvert."
echo "API:     $API_URL"
echo "Swagger: $API_URL/swagger"
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
