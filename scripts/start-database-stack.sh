#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$ROOT_DIR"

COMPOSE=(docker compose --env-file .env.database)

"${COMPOSE[@]}" up --build -d sqlserver sqlpad
"${COMPOSE[@]}" run --rm --build --no-TTY flyway
