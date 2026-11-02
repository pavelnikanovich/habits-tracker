#!/usr/bin/env bash
# Test of ops/availability-month.sh (FR-038, research R16): a stub `gh` first on PATH answers the run listing with the
# conclusions of a fixture, so the script's counting and printing are checked without GitHub.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
trap 'rm -rf "$dir"' EXIT

# The stub records its arguments and prints the fixture: one run conclusion per line, as `--jq` would.
mkdir "$dir/bin"
cat > "$dir/bin/gh" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "$STUB_DIR/gh-args"
cat "$STUB_DIR/runs"
STUB
chmod +x "$dir/bin/gh"
export STUB_DIR="$dir"

run_month() {
  : > "$dir/gh-args"
  PATH="$dir/bin:$PATH" "$repo_root/ops/availability-month.sh" "$@" 2> "$dir/stderr"
}

# 8,640 runs (30 days of a run every 5 minutes), 86 of them failed; cancelled and skipped runs did not run.
{
  yes success | head -n 8554
  yes failure | head -n 86
  echo cancelled
  echo skipped
} > "$dir/runs"
output="$(run_month 2026-11)"
[ "$output" = "passed/ran: 8554/8640 (99.00%)" ] || fail "a month of runs printed '$output'"
grep -q "availability.yml" "$dir/gh-args" || fail "the listing does not name availability.yml: $(cat "$dir/gh-args")"
grep -q "created=2026-11-01..2026-11-30" "$dir/gh-args" || fail "the listing does not ask for November 2026: $(cat "$dir/gh-args")"

# A month with no runs has no value.
: > "$dir/runs"
output="$(run_month 2026-10)"
[ "$output" = "no value" ] || fail "a month without runs printed '$output'"
grep -q "created=2026-10-01..2026-10-31" "$dir/gh-args" || fail "the listing does not ask for October 2026: $(cat "$dir/gh-args")"

# February of a leap year ends on the 29th.
output="$(run_month 2028-02)"
grep -q "created=2028-02-01..2028-02-29" "$dir/gh-args" || fail "the listing does not end February 2028 on the 29th: $(cat "$dir/gh-args")"

# A malformed month is a usage error.
if run_month 2026-13 > /dev/null; then fail "month 2026-13 was accepted"; fi
if run_month > /dev/null; then fail "no month was accepted"; fi

if [ "$failures" -eq 0 ]; then
  echo "availability-month.test.sh: ok"
else
  echo "$failures failure(s)" >&2
  exit 1
fi
