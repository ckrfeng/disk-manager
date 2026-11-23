#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

swift build -c release
APP=build/DiskManager.app
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
cp .build/release/DiskManager "$APP/Contents/MacOS/DiskManager"
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
	<key>CFBundleShortVersionString</key><string>1.0</string>
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
