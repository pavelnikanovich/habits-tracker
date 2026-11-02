#!/usr/bin/env bash
# The test integrity check (research R15): scripts/test-integrity.sh <base-ref>.
# Exit 1 when an added line of `git diff <base-ref>...HEAD` holds a skip marker (printed as file:line: marker), or when
# the number of tests at HEAD is below the number at the base; exit 0 otherwise; exit 2 on a usage or counting error.
# The `test-exception` label is checked by the workflow, not here.
#
# The count is the sum of `dotnet test --list-tests`, the Vitest list and `playwright test --list`. The base is counted
# in a temporary checkout of the merge base of <base-ref> and HEAD (the base itself when they share no history, as in
# epic 1 where main is empty). A part the base does not have (no solution, no frontend) counts 0, as does a part whose
# counting command finds nothing. TEST_COUNT_COMMAND replaces the whole counting command, for the script's own test: it
# runs with the checkout as its working directory and prints one number.
#
# Known limit: the listing commands print a theory row or a generated case (it.each, a loop) once, so removing one of its
# rows does not lower the count; markers and removed methods, tests or files are caught.
# A listing command that fails (a broken build, a restore without network) is an error (exit 2), never a count of 0;
# only the absence of a project counts 0. A shallow clone has no merge base to find (exit 2: fetch full history).
#
# The markers are spelled in two pieces, so that this file does not hold them itself.
set -euo pipefail

if [ $# -ne 1 ] || [ -z "$1" ]; then
  echo "usage: scripts/test-integrity.sh <base-ref>" >&2
  exit 2
fi
base_ref="$1"

root="$(git rev-parse --show-toplevel)"
cd "$root"
git rev-parse --verify --quiet "$base_ref^{commit}" > /dev/null \
  || { echo "test-integrity.sh: $base_ref is not a commit" >&2; exit 2; }

markers=("Ski""p =" ".ski""p(" ".onl""y(" "xi""t(" "xdescri""be(" "test.fi""xme("
  "Ski""p=" "Assert.Ski""p(" "Ski""pUnless" "Ski""pWhen" "Explici""t = true" ".ski""pIf(")

# --- markers: only added lines count, in test files and everywhere else. A letter-initial marker must start a word.
merge_base_status=0
merge_base="$(git merge-base "$base_ref" HEAD 2> /dev/null)" || merge_base_status=$?
if [ "$merge_base_status" -eq 0 ]; then
  diff_range=("$base_ref...HEAD")
elif [ "$(git rev-parse --is-shallow-repository)" = true ] || [ "$merge_base_status" -ne 1 ]; then
  # Exit 1 of merge-base means "no common ancestor"; anything else, or a shallow clone, means missing history.
  echo "test-integrity.sh: the merge base of $base_ref and HEAD cannot be found, the history is missing: fetch full history (git fetch --unshallow)" >&2
  exit 2
else
  merge_base="$(git rev-parse "$base_ref^{commit}")"
  diff_range=("$base_ref" HEAD) # no common history: there is no merge base to diff from
fi

found=0
while IFS= read -r hit; do
  [ -n "$hit" ] || continue
  echo "$hit"
  found=1
done < <(
  git diff --no-color --no-ext-diff -U0 "${diff_range[@]}" | awk -v markers="$(printf '%s\n' "${markers[@]}")" '
    BEGIN { n = split(markers, marker, "\n") }
    /^diff --git / { header = 1; next }
    header && /^\+\+\+ / { file = ($0 == "+++ /dev/null") ? "" : substr($0, 7); next }
    /^@@ / { header = 0; split($3, plus, ","); line = substr(plus[1], 2) + 0; next }
    header { next }
    /^\+/ {
      text = substr($0, 2)
      for (i = 1; i <= n; i++) {
        # A marker that starts with a letter must start a word, so that a call of exit is not taken for the Jasmine marker.
        rest = text
        offset = 0
        while ((at = index(rest, marker[i])) > 0) {
          before = (offset + at > 1) ? substr(text, offset + at - 1, 1) : ""
          if (marker[i] !~ /^[A-Za-z]/ || before !~ /[A-Za-z0-9_$]/) { print file ":" line ": " marker[i]; break }
          offset += at
          rest = substr(text, offset + 1)
        }
      }
      line++
    }'
)

# --- the count.
errors="$(mktemp)"

# run_listing <name> <command...>: prints the command's stdout; if it fails, says so with its stderr and returns 2.
run_listing() {
  local name="$1" out status=0
  shift
  out="$("$@" 2> "$errors")" || status=$?
  if [ "$status" -ne 0 ]; then
    echo "test-integrity.sh: $name failed with exit $status in $PWD:" >&2
    cat "$errors" >&2
    printf '%s\n' "$out" | tail -n 20 >&2
    return 2
  fi
  printf '%s\n' "$out"
}

count_here() { # the number of tests of the tree in the current directory
  local total=0 n out tmp
  if [ -n "${TEST_COUNT_COMMAND:-}" ]; then
    out="$(run_listing TEST_COUNT_COMMAND bash -c "$TEST_COUNT_COMMAND")" || return 2
    out="$(tail -n 1 <<< "$out")"
    out="${out//[[:space:]]/}"
    case "$out" in
      "") echo 0 ;;
      *[!0-9]*) echo "test-integrity.sh: TEST_COUNT_COMMAND printed '$out', not a number" >&2; return 2 ;;
      *) echo "$out" ;;
    esac
    return 0
  fi

  source "$root/scripts/lib/dotnet-path.sh"
  if [ -f backend/HabitsTracker.slnx ]; then
    ensure_dotnet test-integrity.sh
    out="$(run_listing "dotnet test backend/HabitsTracker.slnx --list-tests" dotnet test backend/HabitsTracker.slnx --list-tests)" || return 2
    n="$(sed -n 's/^Discovered \([0-9][0-9]*\) tests in assembly.*/\1/p' <<< "$out" | awk '{ sum += $1 } END { print sum + 0 }')"
    total=$((total + n))
  fi
  if [ -f frontend/package.json ]; then
    if [ ! -d frontend/node_modules ]; then
      echo "test-integrity.sh: frontend/node_modules is missing in $PWD: run npm ci first" >&2
      return 2
    fi
    tmp="$(mktemp)"
    # Vitest also lists the Playwright specs under e2e/, which the next command counts.
    out="$(cd frontend && run_listing "npx vitest list" npx --no-install vitest list --json "$tmp")" || { rm -f "$tmp"; return 2; }
    n="$(node -e '
      const tests = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
      console.log(tests.filter((t) => !/[\\/]e2e[\\/]/.test(t.file)).length);' "$tmp")" \
      || { echo "test-integrity.sh: the Vitest list is not readable JSON" >&2; rm -f "$tmp"; return 2; }
    rm -f "$tmp"
    total=$((total + n))
    if [ -f frontend/playwright.config.ts ]; then
      out="$(cd frontend && run_listing "npx playwright test --list" npx --no-install playwright test --list)" || return 2
      n="$(sed -n 's/^Total: \([0-9][0-9]*\) tests\{0,1\} in .*/\1/p' <<< "$out" | tail -n 1)"
      total=$((total + ${n:-0}))
    fi
  fi
  echo "$total"
}

base_dir="$(mktemp -d)"
cleanup() {
  git -C "$root" worktree remove --force "$base_dir" > /dev/null 2>&1 || true
  rm -rf "$base_dir" "$errors"
}
trap cleanup EXIT
git worktree add --quiet --detach "$base_dir" "$merge_base" > /dev/null
# The base is counted with the installed packages of HEAD.
[ -d "$root/frontend/node_modules" ] && [ -d "$base_dir/frontend" ] \
  && ln -s "$root/frontend/node_modules" "$base_dir/frontend/node_modules"

head_count="$(count_here)" || exit 2
base_count="$(cd "$base_dir" && count_here)" || exit 2

echo "test-integrity.sh: tests at HEAD $head_count, at the base ($merge_base) $base_count"
if [ "$head_count" -lt "$base_count" ]; then
  echo "test-integrity.sh: the test count fell from $base_count to $head_count" >&2
  found=1
fi

exit "$found"
