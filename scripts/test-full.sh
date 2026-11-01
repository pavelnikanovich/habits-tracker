#!/usr/bin/env bash
# The full test command (research R12): the fast command, frontend lint, the end-to-end tests (they start their own
# scripts/dev.sh, so a running one must be stopped first), then every shell test under scripts/tests/ and ops/tests/.
# It stops at the first section that fails.
set -euo pipefail
cd "$(dirname "$0")/.."

scripts/test-fast.sh
npm --prefix frontend run lint
npm --prefix frontend run e2e

shopt -s nullglob # ops/tests/ may not exist yet
for test in scripts/tests/*.test.sh ops/tests/*.test.sh; do
  echo "== $test"
  bash "$test"
done
