#!/bin/bash
# deploy-local.sh — Build (self-contained) and deploy to local Radarr install for testing
#
# Usage:
#   ./scripts/deploy-local.sh           # build + deploy backend + frontend
#   ./scripts/deploy-local.sh ui        # build + deploy frontend only
#   ./scripts/deploy-local.sh backend   # build + deploy backend only
#   ./scripts/deploy-local.sh --no-build [ui|backend|all]  # deploy without building

set -euo pipefail

DEV_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_OUTPUT="$DEV_ROOT/_output"
BACKEND_BUILD="$BUILD_OUTPUT/net8.0/win-x64"
LIVE_DIR="/d/Apps/Radarr"
LIVE_BIN="$LIVE_DIR/bin"
LIVE_UI="$LIVE_BIN/UI"
SKIP_BUILD=false

# Parse --no-build flag
if [ "${1:-}" = "--no-build" ]; then
    SKIP_BUILD=true
    shift
fi

DEPLOY_MODE="${1:-all}"

# --- Validation ---
if [ ! -d "$LIVE_DIR" ]; then
    echo "ERROR: Live Radarr not found at $LIVE_DIR"
    exit 1
fi

# --- Build ---
build_backend() {
    echo "Building backend (win-x64, self-contained, Release)..."
    cd "$DEV_ROOT"
    dotnet msbuild -restore src/Radarr.sln \
        -p:Configuration=Release \
        -p:Platform=Posix \
        -p:RuntimeIdentifiers=win-x64 \
        -p:SelfContained=true \
        -t:PublishAllRids

    echo "Building tray app..."
    dotnet publish src/NzbDrone/Radarr.csproj \
        -c Release -r win-x64 -f net8.0-windows \
        --self-contained true \
        -o "$BACKEND_BUILD/" \
        -p:Platform=Posix \
        -p:TreatWarningsAsErrors=false

    echo "Backend build complete."
}

build_ui() {
    echo "Building frontend..."
    cd "$DEV_ROOT"
    yarn build
    echo "Frontend build complete."
}

# --- Stop Radarr if running ---
stop_radarr() {
    if tasklist 2>/dev/null | grep -qi "Radarr"; then
        echo "Stopping Radarr..."
        taskkill //IM "Radarr.exe" //F 2>/dev/null || true
        taskkill //IM "Radarr.Console.exe" //F 2>/dev/null || true
        sleep 2
        echo "Radarr stopped."
    else
        echo "Radarr is not running."
    fi
}

# --- Deploy frontend ---
deploy_ui() {
    if [ ! -d "$BUILD_OUTPUT/UI" ]; then
        echo "ERROR: No UI build found at $BUILD_OUTPUT/UI — run 'yarn build' first"
        return 1
    fi
    echo "Deploying frontend..."
    rm -rf "$LIVE_UI"
    cp -r "$BUILD_OUTPUT/UI" "$LIVE_UI"
    echo "Frontend deployed."
}

# --- Deploy backend ---
deploy_backend() {
    if [ ! -d "$BACKEND_BUILD" ]; then
        echo "ERROR: No backend build found at $BACKEND_BUILD"
        return 1
    fi
    echo "Deploying backend (replacing bin/ entirely)..."
    # Preserve data files that live outside bin/
    # Wipe old bin/ and replace with self-contained build
    rm -rf "$LIVE_BIN"
    mkdir -p "$LIVE_BIN"
    cp -r "$BACKEND_BUILD"/* "$LIVE_BIN/"
    echo "Backend deployed."
}

# --- Start Radarr ---
start_radarr() {
    echo "Starting Radarr..."
    local win_data
    win_data="$(cygpath -w "$LIVE_DIR")"
    "$LIVE_BIN/Radarr.Console.exe" --data="$win_data" &
    disown
    echo "Radarr started — http://localhost:9871"
}

# --- Main ---
echo "=== Radarr Local Deploy ==="
echo "Mode: $DEPLOY_MODE"
echo ""

# --- Build phase ---
if [ "$SKIP_BUILD" = false ]; then
    case "$DEPLOY_MODE" in
        ui|frontend)  build_ui ;;
        backend)      build_backend ;;
        all|"")       build_backend; build_ui ;;
    esac
    echo ""
fi

stop_radarr

case "$DEPLOY_MODE" in
    ui|frontend)
        deploy_ui
        ;;
    backend)
        deploy_backend
        ;;
    all|"")
        deploy_backend
        deploy_ui
        ;;
    *)
        echo "Unknown mode: $DEPLOY_MODE"
        echo "Usage: $0 [--no-build] [ui|backend|all]"
        exit 1
        ;;
esac

echo ""
read -rp "Start Radarr now? [Y/n] " answer
if [[ "${answer:-Y}" =~ ^[Yy]?$ ]]; then
    start_radarr
fi

echo "Done."
