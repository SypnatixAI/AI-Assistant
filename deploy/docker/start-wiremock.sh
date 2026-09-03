#!/bin/sh

set -eu

: "${DEV_JWT_SIGNING_KEY:?DEV_JWT_SIGNING_KEY is required}"
: "${SPA_ORIGIN:?SPA_ORIGIN is required}"

if [ "${#DEV_JWT_SIGNING_KEY}" -lt 32 ]; then
    echo "DEV_JWT_SIGNING_KEY must contain at least 32 characters." >&2
    exit 1
fi

base64_url_encode() {
    openssl base64 -A | tr '+/' '-_' | tr -d '='
}

issued_at="$(date +%s)"
expires_at="$((issued_at + 28800))"
header="$(printf '%s' '{"alg":"HS256","typ":"JWT"}' | base64_url_encode)"
payload="$(printf '%s' "{\"iss\":\"AssistantCore.Dev\",\"aud\":\"AssistantCore.Api\",\"iat\":${issued_at},\"nbf\":${issued_at},\"exp\":${expires_at},\"tid\":\"00000000-0000-0000-0000-000000000100\",\"oid\":\"00000000-0000-0000-0000-000000000200\",\"name\":\"Administrateur DEV\",\"preferred_username\":\"admin@dev.test\",\"scp\":\"access_as_user\",\"roles\":[\"AssistantCore.Access\",\"TenantAdmin\"]}" | base64_url_encode)"
unsigned_token="${header}.${payload}"
signature="$(printf '%s' "$unsigned_token" \
    | openssl dgst -sha256 -hmac "$DEV_JWT_SIGNING_KEY" -binary \
    | base64_url_encode)"
access_token="${unsigned_token}.${signature}"

printf '%s\n' \
    '{' \
    '  "request": {' \
    '    "method": "GET",' \
    '    "urlPath": "/local-auth/token"' \
    '  },' \
    '  "response": {' \
    '    "status": 200,' \
    '    "headers": {' \
    '      "Content-Type": "application/json",' \
    "      \"Access-Control-Allow-Origin\": \"${SPA_ORIGIN}\"," \
    '      "Cache-Control": "no-store"' \
    '    },' \
    "    \"jsonBody\": {\"access_token\":\"${access_token}\",\"token_type\":\"Bearer\",\"expires_in\":28800}" \
    '  }' \
    '}' \
    > /home/wiremock/mappings/local-auth-token.json

exec /docker-entrypoint.sh "$@"

