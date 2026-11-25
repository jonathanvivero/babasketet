#!/usr/bin/env bash
set -euo pipefail

socat TCP-LISTEN:8081,fork,reuseaddr TCP:web:80 &
socat TCP-LISTEN:8080,fork,reuseaddr TCP:api:8080 &
socat TCP-LISTEN:3000,fork,reuseaddr TCP:william-ui:3000 &
socat TCP-LISTEN:8085,fork,reuseaddr TCP:zitadel:8080 &
socat TCP-LISTEN:8025,fork,reuseaddr TCP:mailpit:8025 &

exec "$@"
