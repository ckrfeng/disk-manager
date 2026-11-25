# DiskManager

DiskManager is a native app for macOS and Windows that shows what’s taking up your storage. Scan a drive or folder, then click through an interactive sunburst chart to find the files and folders using the most space.

[**Download for macOS**](https://github.com/ckrfeng/disk-manager/releases/latest/download/DiskManager-macOS.zip) · [**Download for Windows**](https://github.com/ckrfeng/disk-manager/releases/latest/download/DiskManager-Windows-x64.exe) · [Download page](https://ckrfeng.github.io/disk-manager/) · [Report an issue](https://github.com/ckrfeng/disk-manager/issues/new)

## Install

Both downloads are on the [latest GitHub release](https://github.com/ckrfeng/disk-manager/releases/latest), each with a SHA-256 checksum.

### macOS

DiskManager requires **macOS 14 Sonoma or later**. The universal download supports both **Apple silicon** and **Intel** Macs.

1. Download `DiskManager-macOS.zip`.
2. Unzip it and move `DiskManager.app` to Applications.
3. Open DiskManager. The app is ad-hoc signed and distributed without Apple notarization. If macOS blocks the first launch, follow [Apple’s instructions](https://support.apple.com/en-us/102445) after checking that your copy came from this repository.

To check the download, put it next to `DiskManager-macOS.zip.sha256` and run `shasum -a 256 -c DiskManager-macOS.zip.sha256`.

### Windows

DiskManager runs on **64-bit Windows 10 and 11**.

1. Download `DiskManager-Windows-x64.exe`. It is a single file and needs nothing else installed.
2. Open it. The app is not code-signed, so Windows may show “Windows protected your PC” the first time. Choose **More info**, then **Run anyway**, after checking that your copy came from this repository.

To check the download, run `certutil -hashfile DiskManager-Windows-x64.exe SHA256` and compare the result with `DiskManager-Windows-x64.exe.sha256`.

## Use DiskManager

- **Scan a drive or folder.** Pick a drive or click **Scan Folder…**. You can also drop a folder onto the window.
- **Explore the sunburst.** Larger slices use more space. Click a folder to drill down, or show an item in Finder or File Explorer. On macOS, you can also preview a file with Quick Look.
- **Review before removing.** Collect items in the review area, then send them to the Trash (Recycle Bin on Windows) or explicitly choose permanent deletion.
- **Inspect space the scan can’t account for.** A drive scan can show the difference between the disk’s reported usage and the files the app could measure. You can rescan as an administrator to include protected folders. On macOS, DiskManager can also list and remove local APFS snapshots. On Windows, it links to System Protection and Disk Cleanup.

## Privacy

DiskManager processes file names, paths, and sizes on your computer. The app includes no analytics or scan uploads. Administrator scans transfer results through a local temporary file, which the app attempts to remove when the scan finishes. The operating system can still restrict access to some folders.

If you report a bug, redact personal file names, paths, volume names, and account details from screenshots or diagnostic output.

## Build from source

### macOS

With Xcode and its command-line tools installed, run:

```sh
swift test
./build.sh
open build/DiskManager.app
```

The project uses SwiftUI and Swift Package Manager. The scanner and filesystem logic live in [DiskCore](Sources/DiskCore/), and the app interface lives in [DiskManager](Sources/DiskManager/). `Scripts/package-release.sh` builds the universal release zip.

### Windows

The Windows version is a separate C# codebase (WPF, .NET 10) in [Windows](Windows/). With the [.NET 10 SDK](https://dotnet.microsoft.com/download) installed, run:

```sh
cd Windows
dotnet test DiskCore.Tests
dotnet run --project DiskManager
```

The app only runs on Windows, but it compiles and its tests run on macOS too. `Scripts/package-windows-release.sh` builds the single-file release `.exe`. See [Windows/README.md](Windows/README.md) for how it differs from the Mac app.

## License

[MIT](LICENSE).
