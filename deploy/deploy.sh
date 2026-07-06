#!/usr/bin/env bash
set -euo pipefail

APP_DIR="${APP_DIR:-/docker/sonicRelay}"
COMPOSE_FILE="${COMPOSE_FILE:-docker-compose.prod.yml}"
IMAGE="${IMAGE:?IMAGE is required}"
RUN_MIGRATIONS="${RUN_MIGRATIONS:-true}"
MIGRATION_BUNDLE="${MIGRATION_BUNDLE:-./efbundle}"

cd "$APP_DIR"

if [[ ! -f .env ]]; then
  echo "Missing $APP_DIR/.env"
  echo "Create it before the first deploy."
  exit 1
fi

command -v docker >/dev/null 2>&1 || {
  echo "Docker is not installed on the VPS."
  exit 1
}

docker compose version >/dev/null 2>&1 || {
  echo "Docker Compose plugin is not available on the VPS."
  exit 1
}

read_env_value() {
  local key="$1"
  awk -v key="$key" 'BEGIN { FS="=" } $1 == key { sub(/^[^=]*=/, ""); value=$0 } END { gsub(/\r$/, "", value); gsub(/^\"|\"$/, "", value); print value }' .env
}

run_migrations() {
  if [[ "$RUN_MIGRATIONS" != "true" ]]; then
    echo "Skipping EF Core migrations"
    return 0
  fi

  if [[ ! -x "$MIGRATION_BUNDLE" ]]; then
    echo "Missing executable EF Core migration bundle: $MIGRATION_BUNDLE"
    exit 1
  fi

  local postgres_connection
  postgres_connection="${POSTGRES_CONNECTION:-$(read_env_value "ConnectionStrings__Postgres")}"

  if [[ -z "$postgres_connection" ]]; then
    echo "Missing ConnectionStrings__Postgres in $APP_DIR/.env"
    exit 1
  fi

  echo "Stopping API before applying EF Core migrations"
  docker compose -f "$COMPOSE_FILE" stop api >/dev/null 2>&1 || true

  echo "Applying EF Core migrations"
  "$MIGRATION_BUNDLE" --connection "$postgres_connection" --no-color
}

export IMAGE

echo "Pulling image: $IMAGE"
docker compose -f "$COMPOSE_FILE" pull api

run_migrations

echo "Starting SonicRelay stack"
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans

echo "Pruning dangling images"
docker image prune -f >/dev/null

echo "Deployment finished"
docker compose -f "$COMPOSE_FILE" ps
