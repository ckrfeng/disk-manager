# DiskManager

DiskManager is a native macOS and Windows app that shows what’s taking up your storage. Scan a drive or folder, then click through an interactive sunburst chart to find the files and folders using the most space.

[**Download for macOS**](https://github.com/ckrfeng/disk-manager/releases/latest) · [**Download for Windows**](https://github.com/ckrfeng/disk-manager/releases/latest) · [Download page](https://ckrfeng.github.io/disk-manager/) · [Report an issue](https://github.com/ckrfeng/disk-manager/issues/new)

## Install

DiskManager requires **macOS 14 Sonoma or later**. The universal download supports both **Apple silicon** and **Intel** Macs.

1. Download `DiskManager-macOS.zip` from the latest GitHub release.
2. Unzip it and move `DiskManager.app` to Applications.
3. Open DiskManager. The app is ad-hoc signed and distributed without Apple notarization. If macOS blocks the first launch, follow [Apple’s instructions](https://support.apple.com/en-us/102445) after checking that your copy came from this repository.

The release includes a SHA-256 checksum. With both files in the same folder, run `shasum -a 256 -c DiskManager-macOS.zip.sha256` to check that the download matches.

## Use DiskManager

- **Scan a drive or folder.** Pick a mounted drive or click **Scan Folder…**. You can also drop a folder onto the window.
- **Explore the sunburst.** Larger slices use more space. Click a folder to drill down, show an item in Finder, or preview a file with Quick Look.
- **Review before removing.** Collect items in the review area, then send them to the Trash or explicitly choose permanent deletion.
- **Inspect space the scan can’t account for.** A volume scan can show the difference between the disk’s reported usage and the files the app could measure. DiskManager can list local APFS snapshots and request administrator authorization for a scan or snapshot removal.

## Privacy

DiskManager processes file names, paths, and sizes on your Mac. The app includes no analytics or scan uploads. Administrator scans transfer results through a local temporary file, which the app attempts to remove when the scan finishes. macOS can still restrict access to some folders.

If you report a bug, redact personal file names, paths, volume names, and account details from screenshots or diagnostic output.

## Build from source

With Xcode and its command-line tools installed, run the following on macOS:

```sh
swift test
./build.sh
open build/DiskManager.app
```

The project uses SwiftUI and Swift Package Manager. The scanner and filesystem logic live in [DiskCore](Sources/DiskCore/), and the app interface lives in [DiskManager](Sources/DiskManager/).

## Windows

DiskManager also runs on **64-bit Windows 10 and 11**. It has the same drive list, sunburst chart, collector, and administrator scans, and moves items to the Recycle Bin instead of the Trash.

1. Download `DiskManager-Windows-x64.exe` from the [latest GitHub release](https://github.com/ckrfeng/disk-manager/releases/latest). It is a single file and needs nothing else installed.
2. Open it. The app is not code-signed, so Windows may show “Windows protected your PC” the first time. Choose **More info**, then **Run anyway**.

To check the download, run `certutil -hashfile DiskManager-Windows-x64.exe SHA256` and compare it with `DiskManager-Windows-x64.exe.sha256`.

The Windows version is a separate C# (WPF, .NET 10) codebase in [Windows](Windows/). To build it from source with the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```sh
cd Windows
dotnet test DiskCore.Tests
dotnet run --project DiskManager
```

See [Windows/README.md](Windows/README.md) for the differences from the Mac app.

## License

[MIT](LICENSE).
