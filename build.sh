#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

swift_options=(-c release -Xswiftc -file-prefix-map -Xswiftc "$PWD=.")
case "${1:-}" in
  "") ;;
  --universal) ;;
  *) echo "Usage: $0 [--universal]" >&2; exit 2 ;;
esac
if [[ $# -gt 1 ]]; then
  echo "Usage: $0 [--universal]" >&2
  exit 2
fi

build_binary() {
  swift build "${swift_options[@]}" "$@" >&2
  swift build "${swift_options[@]}" "$@" --show-bin-path
}

if [[ "${1:-}" == --universal ]]; then
  arm_dir=$(build_binary --arch arm64)
  intel_dir=$(build_binary --arch x86_64)
else
  bin_dir=$(build_binary)
fi

APP=build/DiskManager.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
executable="$APP/Contents/MacOS/DiskManager"
if [[ "${1:-}" == --universal ]]; then
  lipo -create "$arm_dir/DiskManager" "$intel_dir/DiskManager" -output "$executable"
else
  cp "$bin_dir/DiskManager" "$executable"
fi
strip -S "$executable"
cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleName</key><string>DiskManager</string>
	<key>CFBundleDisplayName</key><string>DiskManager</string>
	<key>CFBundleIdentifier</key><string>local.diskmanager.app</string>
	<key>CFBundleExecutable</key><string>DiskManager</string>
	<key>CFBundleIconFile</key><string>AppIcon</string>
	<key>CFBundlePackageType</key><string>APPL</string>
	<key>CFBundleShortVersionString</key><string>1.0.0</string>
	<key>CFBundleVersion</key><string>1</string>
	<key>LSMinimumSystemVersion</key><string>14.0</string>
	<key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
	<key>NSHighResolutionCapable</key><true/>
	<key>NSDesktopFolderUsageDescription</key><string>DiskManager scans this folder to measure disk usage.</string>
	<key>NSDocumentsFolderUsageDescription</key><string>DiskManager scans this folder to measure disk usage.</string>
	<key>NSDownloadsFolderUsageDescription</key><string>DiskManager scans this folder to measure disk usage.</string>
	<key>NSRemovableVolumesUsageDescription</key><string>DiskManager scans this disk to measure disk usage.</string>
	<key>NSNetworkVolumesUsageDescription</key><string>DiskManager scans this disk to measure disk usage.</string>
</dict>
</plist>
PLIST
codesign --force --sign - "$APP" >/dev/null
echo "Built $APP"
