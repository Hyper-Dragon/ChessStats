#!/bin/sh
# Runs ChessStats on a schedule and serves the reports with nginx.
#
# Accounts are declared by directories under /app/ChessStatsResults - that is the list
# "chess-stats -refresh" works from. CHESS_USERS seeds them on first boot, and any
# directories already on the mounted volume are honoured as well.
set -eu

RESULTS=/app/ChessStatsResults
CACHE=/app/ChessStatsCache
INTERVAL="${REFRESH_INTERVAL:-86400}"

mkdir -p "$RESULTS" "$CACHE"

if [ -n "${CHESS_USERS:-}" ]; then
    for user in $CHESS_USERS; do
        mkdir -p "$RESULTS/$user"
    done
fi

accounts="$(ls -A "$RESULTS" 2>/dev/null | grep -v '^index\.html$' || true)"
if [ -z "$accounts" ]; then
    echo "WARNING: no accounts configured - set CHESS_USERS or seed $RESULTS" >&2
else
    echo "==> accounts: $(echo $accounts)"
fi

echo "==> starting nginx"
nginx

refresh() {
    echo "==> refresh started $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
    # A non-zero exit (an unknown account, an API failure) must not stop the loop.
    /app/chess-stats -refresh || echo "==> refresh finished with exit code $?"
    echo "==> refresh finished $(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}

refresh

while :; do
    sleep "$INTERVAL"
    refresh
done
