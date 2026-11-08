#!/usr/bin/env bash
set -euo pipefail

readonly DOCKER="${DOCKER_BIN:-docker}"
readonly IMAGE="${ASPIRE_DASHBOARD_IMAGE:-mcr.microsoft.com/aspire/dashboard:13.6.4}"
readonly CONTAINER_NAME="${ASPIRE_DASHBOARD_CONTAINER_NAME:-todo-api-aspire-dashboard}"

fail() {
    printf 'error: %s\n' "$*" >&2
    exit 1
}

cleanup() {
    "$DOCKER" stop --time 2 "$CONTAINER_NAME" >/dev/null 2>&1 || true
    "$DOCKER" rm --force "$CONTAINER_NAME" >/dev/null 2>&1 || true
}

if ! command -v "$DOCKER" >/dev/null 2>&1; then
    fail "Docker is required to run the Aspire Dashboard. Install Docker Desktop or another Docker-compatible engine, then make sure 'docker' is on your PATH."
fi

if ! "$DOCKER" info >/dev/null 2>&1; then
    fail "Docker is installed, but the Docker daemon is not reachable. Start Docker Desktop or your Docker engine, then try again."
fi

trap cleanup EXIT INT TERM

# Remove a container left behind by a previous interrupted run.
cleanup

printf 'Starting Aspire Dashboard (%s)\n' "$IMAGE"
printf '  UI:        http://localhost:18888\n'
printf '  OTLP/gRPC: http://localhost:4317\n'
printf '  OTLP/HTTP: http://localhost:4318\n'
printf 'Press Ctrl+C to stop the dashboard.\n\n'

# Run in the background so the trap can react to Ctrl+C immediately, then wait
# in the foreground. Shells without job control keep the container in the same
# process group, so the terminal also signals it.
"$DOCKER" run \
    --rm \
    --name "$CONTAINER_NAME" \
    --publish 18888:18888 \
    --publish 4317:18889 \
    --publish 4318:18890 \
    --env DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true \
    "$IMAGE" &

DOCKER_PID=$!
wait "$DOCKER_PID"
