#!/usr/bin/env bash
# Test of frontend/scripts/fetch-fonts.sh (research R24): it downloads the Nordwand Mono release zip and the font
# license, checks each against its SHA-256 sum, and puts only the Regular and Bold fonts into src/assets/fonts/ and
# the license into public/fonts/; files already in place with their sums are not downloaded again; any mismatch exits 1
# naming the file and leaves nothing in place. It runs a copy of the script in a temporary folder laid out like the
# repository, with a stub `curl` first on PATH that serves files from a fixture folder by the URL's file name and
# records each call. FONTS_SHA256_FILE points at the fixture's sums. The last case checks the real sums file.
set -uo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
failures=0

fail() {
  echo "FAIL: $1" >&2
  failures=$((failures + 1))
}

dir="$(mktemp -d)"
trap 'rm -rf "$dir"' EXIT

# The stub curl: copies $FIXTURES/<last path segment of the URL> to the file given by -o or --output.
mkdir -p "$dir/stubs"
cat > "$dir/stubs/curl" << 'STUB'
#!/usr/bin/env bash
echo "curl $*" >> "$CURL_CALLS"
output=""
url=""
while [ $# -gt 0 ]; do
  case "$1" in
    -o | --output) output="$2"; shift 2 ;;
    http*) url="$1"; shift ;;
    *) shift ;;
  esac
done
source="$FIXTURES/${url##*/}"
[ -f "$source" ] || { echo "curl: (22) The requested URL returned error: 404" >&2; exit 22; }
cp "$source" "$output"
STUB
chmod +x "$dir/stubs/curl"

# The fixture release: the two fonts the script keeps, one it must leave in the zip, and the license.
make_fixture() { # $1: folder, $2: text that makes this fixture's Regular font differ
  mkdir -p "$1/content"
  printf 'regular %s' "$2" > "$1/content/NordwandMono-Regular.woff2"
  printf 'bold' > "$1/content/NordwandMono-Bold.woff2"
  printf 'italic' > "$1/content/NordwandMono-Italic.woff2"
  printf 'license' > "$1/LICENSE-FONT.txt"
  (cd "$1/content" && zip -q -X "$1/NordwandMono-WOFF2.zip" ./*.woff2)
}
make_fixture "$dir/release" ""
make_fixture "$dir/tampered" "changed"
(cd "$dir/release" && sha256sum NordwandMono-WOFF2.zip LICENSE-FONT.txt) > "$dir/release.sha256"
(cd "$dir/release/content" && sha256sum NordwandMono-Regular.woff2 NordwandMono-Bold.woff2) >> "$dir/release.sha256"

# A fresh copy of the frontend folder's script: $1 is the layout's root.
layout() {
  mkdir -p "$1/frontend/scripts"
  cp "$repo_root/frontend/scripts/fetch-fonts.sh" "$1/frontend/scripts/fetch-fonts.sh" 2> /dev/null
}

# Runs the script of layout $1 against fixture folder $2 and sums file $3; sets status, output and calls.
run_fetch() {
  rm -f "$dir/calls"
  touch "$dir/calls"
  output="$(cd "$1" && PATH="$dir/stubs:$PATH" CURL_CALLS="$dir/calls" FIXTURES="$2" FONTS_SHA256_FILE="$3" \
    bash frontend/scripts/fetch-fonts.sh 2>&1)"
  status=$?
  calls="$(wc -l < "$dir/calls")"
}

files_in() { # the file names under folder $1, sorted, on one line
  [ -d "$1" ] || return 0
  find "$1" -type f -printf '%f\n' | sort | tr '\n' ' '
}

# Matching files are put in place; only the two fonts leave the zip.
good="$dir/good"
layout "$good"
run_fetch "$good" "$dir/release" "$dir/release.sha256"
[ "$status" -eq 0 ] || fail "matching files: exit $status, expected 0 ($output)"
[ "$calls" -eq 2 ] || fail "matching files: $calls curl calls, expected 2 (the zip and the license)"
fonts="$(files_in "$good/frontend/src/assets/fonts")"
[ "$fonts" = "NordwandMono-Bold.woff2 NordwandMono-Regular.woff2 " ] \
  || fail "matching files: src/assets/fonts/ holds '$fonts', expected the Bold and Regular fonts"
cmp -s "$good/frontend/src/assets/fonts/NordwandMono-Regular.woff2" "$dir/release/content/NordwandMono-Regular.woff2" \
  || fail "matching files: the Regular font in place is not the one from the zip"
license="$(files_in "$good/frontend/public/fonts")"
[ "$license" = "LICENSE-FONT.txt " ] || fail "matching files: public/fonts/ holds '$license', expected LICENSE-FONT.txt"

# A second run finds the files in place: no download, no output.
run_fetch "$good" "$dir/release" "$dir/release.sha256"
[ "$status" -eq 0 ] || fail "second run: exit $status, expected 0 ($output)"
[ "$calls" -eq 0 ] || fail "second run: $calls curl calls, expected none"
[ -z "$output" ] || fail "second run printed '$output', expected nothing"

# A zip whose sum differs: exit 1 naming the zip, nothing in place.
bad_zip="$dir/bad-zip"
layout "$bad_zip"
run_fetch "$bad_zip" "$dir/tampered" "$dir/release.sha256"
[ "$status" -eq 1 ] || fail "zip mismatch: exit $status, expected 1"
[[ "$output" == *NordwandMono-WOFF2.zip* ]] || fail "zip mismatch: message '$output' does not name NordwandMono-WOFF2.zip"
[ -z "$(files_in "$bad_zip/frontend/src/assets/fonts")$(files_in "$bad_zip/frontend/public/fonts")" ] \
  || fail "zip mismatch: files left in place: $(files_in "$bad_zip/frontend/src/assets/fonts")$(files_in "$bad_zip/frontend/public/fonts")"

# A font whose sum differs inside a zip that matches: exit 1 naming the font, nothing in place.
bad_font="$dir/bad-font"
layout "$bad_font"
sed 's/^[0-9a-f]* \( *NordwandMono-Bold.woff2\)$/0000000000000000000000000000000000000000000000000000000000000000 \1/' \
  "$dir/release.sha256" > "$dir/bad-font.sha256"
run_fetch "$bad_font" "$dir/release" "$dir/bad-font.sha256"
[ "$status" -eq 1 ] || fail "font mismatch: exit $status, expected 1"
[[ "$output" == *NordwandMono-Bold.woff2* ]] || fail "font mismatch: message '$output' does not name NordwandMono-Bold.woff2"
[ -z "$(files_in "$bad_font/frontend/src/assets/fonts")$(files_in "$bad_font/frontend/public/fonts")" ] \
  || fail "font mismatch: files left in place"

# The real sums file holds the four sums of research R24.
expected_sums="10fac057fcc90e99b50ad9cced0b3aa9ba97e62cc174f320082b254ef2f79141  NordwandMono-WOFF2.zip
e0e493edae714ad5bb243a346f3fd45ecdd94a00584118297f2d05e0f6187d38  LICENSE-FONT.txt
23e1721165a9c770f98afbe202fd92b3ddb022fdaf0393850ea03124fa06ebc3  NordwandMono-Regular.woff2
77a855a23ecf34bb06dafdbd62dbf8080b0b8a5b80b302d99b06a7e7dfad4223  NordwandMono-Bold.woff2"
[ "$(cat "$repo_root/frontend/scripts/fonts.sha256" 2> /dev/null)" = "$expected_sums" ] \
  || fail "frontend/scripts/fonts.sha256 does not hold the four sums of research R24"

if [ "$failures" -eq 0 ]; then
  echo "fetch-fonts: 5 cases passed"
else
  echo "fetch-fonts: $failures checks failed" >&2
  exit 1
fi
