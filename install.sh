#!/usr/bin/env bash

set -euo pipefail

# Restrict execution strictly to Linux environments to guarantee compatibility and prevent unintended execution on other systems
if [ "$(uname -s)" != "Linux" ]; then
    echo "Error: This deployment script is designed exclusively for Linux systems." >&2
    exit 1
fi

# Ensure standard Linux binaries and container runtimes are in PATH for cron environments
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$HOME/.docker/bin:$PATH"

SCRIPT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CRON_TAG="# knightfight-auto-update"

update_repo() {
    cd "$REPO_DIR" || exit 1

    if ! command -v git >/dev/null 2>&1; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Error: git is not found in PATH." >&2
        exit 1
    fi

    if docker compose version >/dev/null 2>&1; then
        DOCKER_COMPOSE="docker compose"
    elif command -v docker-compose >/dev/null 2>&1; then
        DOCKER_COMPOSE="docker-compose"
    else
        DOCKER_COMPOSE="docker compose"
    fi

    OLD_COMMIT="$(git rev-parse HEAD 2>/dev/null || true)"

    echo "[$(date '+%Y-%m-%d %H:%M:%S')] Checking for updates via git pull..."
    if ! git pull; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Error: git pull failed." >&2
        exit 1
    fi

    NEW_COMMIT="$(git rev-parse HEAD 2>/dev/null || true)"

    if [ -n "$OLD_COMMIT" ] && [ -n "$NEW_COMMIT" ] && [ "$OLD_COMMIT" != "$NEW_COMMIT" ]; then
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] New commits detected ($OLD_COMMIT -> $NEW_COMMIT). Running build and up..."
        $DOCKER_COMPOSE build
        $DOCKER_COMPOSE up -d --remove-orphans
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] Build and deployment completed successfully."
    else
        echo "[$(date '+%Y-%m-%d %H:%M:%S')] No new commits. Repository is up to date ($NEW_COMMIT)."
    fi
}

install_cron() {
    chmod +x "$SCRIPT_PATH"
    
    CRON_JOB="*/5 * * * * /bin/bash \"$SCRIPT_PATH\" --update $CRON_TAG"

    CURRENT_CRONTAB="$(crontab -l 2>/dev/null || true)"

    # Remove existing cron job for this script if present, then add new entry
    NEW_CRONTAB="$(echo "$CURRENT_CRONTAB" | grep -v "$CRON_TAG" | grep -v "$SCRIPT_PATH" || true)"
    
    if [ -n "$NEW_CRONTAB" ]; then
        NEW_CRONTAB="${NEW_CRONTAB}
${CRON_JOB}"
    else
        NEW_CRONTAB="${CRON_JOB}"
    fi

    echo "$NEW_CRONTAB" | crontab -
    echo "Cron task successfully installed:"
    echo "  $CRON_JOB"
}

uninstall_cron() {
    CURRENT_CRONTAB="$(crontab -l 2>/dev/null || true)"
    NEW_CRONTAB="$(echo "$CURRENT_CRONTAB" | grep -v "$CRON_TAG" | grep -v "$SCRIPT_PATH" || true)"

    if [ -n "$NEW_CRONTAB" ]; then
        echo "$NEW_CRONTAB" | crontab -
    else
        crontab -r 2>/dev/null || true
    fi
    echo "Cron task removed."
}

case "${1:-install}" in
    --update|-u|update|--run|run)
        update_repo
        ;;
    --uninstall|uninstall|--remove|remove)
        uninstall_cron
        ;;
    --install|install)
        install_cron
        ;;
    *)
        echo "Usage: $0 [--install | --update | --uninstall]"
        exit 1
        ;;
esac
