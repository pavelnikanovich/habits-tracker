#!/usr/bin/env bash
# Fetches the interface font, Nordwand Mono 1.5.0 (research R24), which is not kept in the repository: the release zip
# and the font license from GitHub, each checked against its SHA-256 sum in scripts/fonts.sha256 (or in the file named
# by FONTS_SHA256_FILE, which only its test sets). Only the Regular and Bold fonts leave the zip, checked against their
# own sums, into src/assets/fonts/; the license goes to public/fonts/, so the application serves it with the font.
# `npm run build`, `npm start` and `npm test` run it first; with the three files in place and matching, it downloads
# nothing and prints nothing. Any mismatch exits 1 naming the file and leaves none of the three files in place.
set -euo pipefail

frontend="$(cd "$(dirname "$0")/.." && pwd)"
sums="${FONTS_SHA256_FILE:-$frontend/scripts/fonts.sha256}"
release_url="https://github.com/tywr/Nordwand-Mono/releases/download/1.5.0/NordwandMono-WOFF2.zip"
license_url="https://raw.githubusercontent.com/tywr/Nordwand-Mono/1.5.0/LICENSE-FONT.txt"
fonts_dir="$frontend/src/assets/fonts"
license_dir="$frontend/public/fonts"
fonts=(NordwandMono-Regular.woff2 NordwandMono-Bold.woff2)

if [ ! -r "$sums" ]; then
  echo "fetch-fonts: the sums file $sums is missing" >&2
  exit 1
fi

# Whether file $1 has the sum the sums file gives for name $2.
matches() {
  local expected
  expected="$(awk -v name="$2" '$2 == name { print $1 }' "$sums")"
  [ -n "$expected" ] && [ -f "$1" ] && [ "$(sha256sum "$1" | cut -d ' ' -f 1)" = "$expected" ]
}

# Stops with the file's name unless file $1 has the sum of name $2.
check() {
  if ! matches "$1" "$2"; then
    echo "fetch-fonts: $2 does not match its SHA-256 sum in $sums; no font was put in place" >&2
    exit 1
  fi
}

if matches "$fonts_dir/${fonts[0]}" "${fonts[0]}" && matches "$fonts_dir/${fonts[1]}" "${fonts[1]}" \
  && matches "$license_dir/LICENSE-FONT.txt" LICENSE-FONT.txt; then
  exit 0
fi

# Whatever is in place does not match: nothing stays there unless the new files all match.
rm -f "$fonts_dir/${fonts[0]}" "$fonts_dir/${fonts[1]}" "$license_dir/LICENSE-FONT.txt"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "fetch-fonts: downloading Nordwand Mono 1.5.0 from github.com"
curl --fail --location --silent --show-error --output "$work/NordwandMono-WOFF2.zip" "$release_url"
curl --fail --location --silent --show-error --output "$work/LICENSE-FONT.txt" "$license_url"
check "$work/NordwandMono-WOFF2.zip" NordwandMono-WOFF2.zip
check "$work/LICENSE-FONT.txt" LICENSE-FONT.txt

unzip -q -d "$work/fonts" "$work/NordwandMono-WOFF2.zip" "${fonts[@]}"
for font in "${fonts[@]}"; do
  check "$work/fonts/$font" "$font"
done

mkdir -p "$fonts_dir" "$license_dir"
for font in "${fonts[@]}"; do
  mv "$work/fonts/$font" "$fonts_dir/$font"
done
mv "$work/LICENSE-FONT.txt" "$license_dir/LICENSE-FONT.txt"
