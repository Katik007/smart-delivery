#!/usr/bin/env bash
set -euo pipefail

cd /home/kabanovae
exec 9>deploy.lock
flock -x 9

if [[ ! ${SSH_ORIGINAL_COMMAND:-} =~ ^deploy\ ([a-f0-9]{40})$ ]]; then
    echo "Expected deploy followed by a Git commit SHA" >&2
    exit 2
fi
revision="${BASH_REMATCH[1]}"

git -C repo fetch --depth 1 origin refs/heads/main
if [[ "$(git -C repo rev-parse FETCH_HEAD)" != "$revision" ]]; then
    echo "The requested commit is no longer the tip of main." >&2
    exit 3
fi
git -C repo checkout --detach "$revision"

image="smartdelivery-kabanovae:$revision"
docker build --tag "$image" repo

compose=(docker compose -p smartdelivery-kabanovae -f compose.yaml -f compose.production.yaml)
if SMARTDELIVERY_IMAGE="$image" "${compose[@]}" up --detach --wait --wait-timeout 150; then
    printf '%s\n' "$image" > deployed-image
    "${compose[@]}" ps
else
    "${compose[@]}" logs --tail 30 >&2
    if [[ -s deployed-image ]]; then
        SMARTDELIVERY_IMAGE="$(<deployed-image)" "${compose[@]}" up --detach --wait --wait-timeout 150
    fi
    exit 1
fi
