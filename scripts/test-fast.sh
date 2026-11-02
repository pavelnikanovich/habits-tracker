#!/usr/bin/env bash
# The fast test command (research R12): every backend test, PostgreSQL in Testcontainers (needs Docker),
# then every frontend unit test, once, without watching.
set -euo pipefail
cd "$(dirname "$0")/.."

source scripts/lib/dotnet-path.sh
ensure_dotnet test-fast.sh

dotnet test backend/HabitsTracker.slnx
npm --prefix frontend test
