#!/bin/bash
# Builds dist/DiskManager-Windows-x64.exe (self-contained, single file) and its SHA-256 checksum.
# Needs the .NET 10 SDK; runs on macOS, Linux, or Windows (Git Bash).
set -euo pipefail
cd "$(dirname "$0")/.."

out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT

dotnet test Windows/DiskCore.Tests
dotnet publish Windows/DiskManager -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "$out"

mkdir -p dist
exe=DiskManager-Windows-x64.exe
cp "$out/DiskManager.exe" "dist/$exe"
(cd dist && shasum -a 256 "$exe" > "$exe.sha256")
echo "Packaged dist/$exe and dist/$exe.sha256"
