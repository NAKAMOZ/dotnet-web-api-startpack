#!/usr/bin/env bash
set -euo pipefail

pattern='-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----|AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{36,}|xox[baprs]-[A-Za-z0-9-]{20,}|sk-(live|proj)-[A-Za-z0-9_-]{20,}|AIza[0-9A-Za-z_-]{35}'

if matches="$(git grep --untracked --exclude-standard -nEI -e "$pattern" -- . ':!scripts/check-secrets.sh')"; then
  echo "Potential committed secret material detected:"
  echo "$matches"
  exit 1
fi

echo "No known secret patterns found in tracked or untracked non-ignored files."
