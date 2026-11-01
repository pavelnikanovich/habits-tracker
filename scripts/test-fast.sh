#!/usr/bin/env bash
# The fast test command (research R12): every backend test, PostgreSQL in Testcontainers (needs Docker).
set -euo pipefail
cd "$(dirname "$0")/.."

# Hooks run from Claude Code's environment, where dotnet may be installed but not on PATH.
if ! command -v dotnet > /dev/null 2>&1; then
  dotnet_root="${DOTNET_ROOT:-$HOME/.dotnet}"
  if [ -x "$dotnet_root/dotnet" ]; then
    export PATH="$dotnet_root:$PATH"
  else
    echo "test-fast.sh: the .NET SDK is missing: no dotnet on PATH or in $dotnet_root (install SDK 10.0.403)." >&2
    exit 1
  fi
fi

dotnet test backend/HabitsTracker.slnx
