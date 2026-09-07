#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "Usage: $0 <release-archive.zip> <new-release-directory>" >&2
    exit 2
fi

archive_path=$(realpath -- "$1")
release_directory=$(realpath -m -- "$2")

if [[ ! -f "$archive_path" ]]; then
    echo "Release archive was not found." >&2
    exit 1
fi

if [[ -e "$release_directory" ]]; then
    echo "Release directory already exists; choose a fresh release-id directory." >&2
    exit 1
fi

mkdir -p -- "$release_directory"
chmod 755 -- "$release_directory"

# Reject path traversal before extraction because the archive is an external
# deployment input, even though Publish-Release.ps1 creates it locally.
if unzip -Z1 -- "$archive_path" | grep -Eq '(^/|(^|/)\.\.?(/|$))'; then
    echo "Release archive contains an unsafe path." >&2
    exit 1
fi

# Info-ZIP uses exit code 1 for warning-level completion; Windows-created ZIPs
# can trigger that known path-separator warning even after a successful extract.
if unzip -q -- "$archive_path" -d "$release_directory"; then
    :
else
    unzip_status=$?
    if (( unzip_status > 1 )); then
        echo "Release archive extraction failed (unzip exit $unzip_status)." >&2
        exit "$unzip_status"
    fi
fi

if find "$release_directory" -type d \( -name .git -o -name .vs -o -name bin -o -name obj -o -name artifacts -o -name node_modules -o -name ClientApp \) -print -quit | grep -q .; then
    echo "Extracted release contains a forbidden build/source directory." >&2
    exit 1
fi

if find "$release_directory" -type f \( -name '*.db' -o -name '*.db-wal' -o -name '*.db-shm' -o -name '*.sqlite' -o -name '*.sqlite-wal' -o -name '*.sqlite-shm' -o -name '*.pem' -o -name '*.key' -o -name '*.pfx' -o -name '.env' -o -name '.env.*' \) -print -quit | grep -q .; then
    echo "Extracted release contains a forbidden secret or database file." >&2
    exit 1
fi

# ZIP has no dependable Unix mode portability. Apply least-privilege modes
# explicitly: traversable/readable directories, non-executable files, and only
# the framework-dependent apphost executable marked runnable.
find "$release_directory" -type d -exec chmod 755 {} +
find "$release_directory" -type f -exec chmod 644 {} +

apphost="$release_directory/CalorieTracker"
if [[ ! -f "$apphost" ]]; then
    echo "The framework-dependent CalorieTracker apphost is missing." >&2
    exit 1
fi
chmod 755 -- "$apphost"

echo "Extracted release with deterministic directory/file permissions: $release_directory"
