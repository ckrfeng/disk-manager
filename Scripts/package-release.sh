#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."

./build.sh --universal
app=build/DiskManager.app
lipo "$app/Contents/MacOS/DiskManager" -verify_arch arm64 x86_64
codesign --verify --strict "$app"

mkdir -p dist
archive=DiskManager-macOS.zip
staging=$(mktemp -d "$PWD/dist/.release.XXXXXX")
trap 'rm -rf "$staging"' EXIT

ditto -c -k --norsrc --keepParent "$app" "$staging/$archive"
(cd "$staging" && shasum -a 256 "$archive" > "$archive.sha256")
mv -f "$staging/$archive" "$staging/$archive.sha256" dist/
echo "Packaged dist/$archive and dist/$archive.sha256"
