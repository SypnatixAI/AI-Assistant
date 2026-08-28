#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

if [[ -n "${SQL_SERVER_PASSWORD:-}"
    && -n "${SQLPAD_ADMIN_EMAIL:-}"
    && -n "${SQLPAD_ADMIN_PASSWORD:-}" ]]; then
    COMPOSE=(docker compose)
elif [[ -f "$ROOT_DIR/.env.database" ]]; then
    COMPOSE=(docker compose --env-file .env.database)
else
    echo "La configuration SQL locale est absente." >&2
    echo "Configure .env.database ou fournis SQL_SERVER_PASSWORD, SQLPAD_ADMIN_EMAIL et SQLPAD_ADMIN_PASSWORD." >&2
    exit 1
fi

"${COMPOSE[@]}" up --build -d sqlserver sqlpad
if [[ "${ASSISTANTCORE_SKIP_DATABASE_MIGRATIONS:-false}" != "true" ]]; then
    "${COMPOSE[@]}" run --rm --build --no-TTY flyway
fi
