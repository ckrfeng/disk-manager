# DiskManager for Windows

A native Windows version of DiskManager (WPF, .NET 10). It lives next to the macOS app and shares no code with it.
The parts map one to one:

| macOS (Swift) | Windows (C#) |
| --- | --- |
| `Sources/DiskCore/DiskScanner.swift` (`getattrlistbulk`) | `DiskCore/DiskScanner.cs` (`FileSystemEnumerable`) |
| `FileNode`, `TreeCoding` | `FileNode.cs`, `TreeCoding.cs` (same binary format) |
| `Volumes.swift` | `Volumes.cs` (`DriveInfo`) |
| `Protection.swift` (macOS system folders) | `Protection.cs` (Windows, Program Files, user folders…) |
| `SunburstLayout.swift`, `SunburstView.swift` | `DiskCore/SunburstLayout.cs`, `DiskManager/SunburstControl.cs` |
| `AppModel.swift` | `DiskManager/AppModel.cs` |
| Administrator scan via `osascript` | Elevated second copy via UAC (`runas`) |
| Move to Trash | Move to Recycle Bin (`SHFileOperation`) |
| APFS snapshots | Not ported. The hidden-space dialog links to System Protection and Disk Cleanup instead. |

## Build and run (on Windows)

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
cd Windows
dotnet test DiskCore.Tests
dotnet run --project DiskManager
```

Make a folder you can copy to another PC (needs .NET 10 Desktop Runtime there):

```powershell
dotnet publish DiskManager -c Release -r win-x64 --self-contained false -o publish
```

Add `--self-contained true -p:PublishSingleFile=true` for a single `.exe` that runs without installing .NET.

## Build on a Mac

The app only runs on Windows, but everything compiles on macOS, and the core tests run there:

```bash
cd Windows
dotnet test DiskCore.Tests
dotnet build DiskManager
```

## Known differences from the Mac app

- Sizes are file lengths, not allocated size, and hard links are counted every time they appear.
  `C:\Windows\WinSxS` is mostly hard links, so the Windows folder can look larger than it really is.
- Junctions, symbolic links and mounted folders are skipped, like the Mac app skips other volumes.
- OneDrive "online-only" files count as 0 bytes, because they use no space on the drive.
- No Quick Look. Right-click an item to show it in File Explorer instead.
- The app icon is generated from `Resources/AppIcon.png` by `Scripts/make-windows-icon.sh`.
