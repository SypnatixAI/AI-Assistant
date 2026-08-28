#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIGURATION_FILE="$ROOT_DIR/AssistantCore.Service/appsettings.Local.json"
OUTPUT_DIRECTORY="$ROOT_DIR/.local"
TOKEN_FILE="$OUTPUT_DIRECTORY/local-jwt.txt"

for command_name in jq openssl; do
    if ! command -v "$command_name" >/dev/null 2>&1; then
        echo "Commande requise introuvable: $command_name" >&2
        exit 1
    fi
done

base64_url_encode() {
    openssl base64 -A | tr '+/' '-_' | tr -d '='
}

issuer="$(jq -er '.Authentication.LocalJwt.Issuer' "$CONFIGURATION_FILE")"
audience="$(jq -er '.Authentication.LocalJwt.Audience' "$CONFIGURATION_FILE")"
signing_key="$(jq -er '.Authentication.LocalJwt.SigningKey' "$CONFIGURATION_FILE")"
issued_at="$(date +%s)"
expires_at="$((issued_at + 28800))"

header="$(jq -cn '{alg:"HS256",typ:"JWT"}' | base64_url_encode)"
payload="$(jq -cn \
    --arg issuer "$issuer" \
    --arg audience "$audience" \
    --argjson issued_at "$issued_at" \
    --argjson expires_at "$expires_at" \
    '{
        iss: $issuer,
        aud: $audience,
        iat: $issued_at,
        nbf: $issued_at,
        exp: $expires_at,
        tid: "00000000-0000-0000-0000-000000000100",
        oid: "00000000-0000-0000-0000-000000000200",
        name: "Administrateur local",
        preferred_username: "admin@local.test",
        scp: "access_as_user"
    }' | base64_url_encode)"
unsigned_token="$header.$payload"
signature="$(printf '%s' "$unsigned_token" \
    | openssl dgst -sha256 -hmac "$signing_key" -binary \
    | base64_url_encode)"

mkdir -p "$OUTPUT_DIRECTORY"
printf '%s\n' "$unsigned_token.$signature" > "$TOKEN_FILE"
chmod 600 "$TOKEN_FILE"

echo "$TOKEN_FILE"
