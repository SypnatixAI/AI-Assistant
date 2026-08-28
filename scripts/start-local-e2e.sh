#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Ce script est conservé pour compatibilité et lance maintenant start-local-live.sh."
exec bash "$ROOT_DIR/scripts/start-local-live.sh"
