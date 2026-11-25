namespace DiskCore;

/// <summary>The folders that decide what is protected. Tests pass their own; the app uses <see cref="Current"/>.</summary>
public sealed record ProtectionContext(string WindowsDirectory, IReadOnlyList<string> ProgramDirectories, string Home,
    string AppDirectory)
{
    public static ProtectionContext Current { get; } = new(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        ],
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        AppContext.BaseDirectory);
}

/// <summary>Decides which items must not be collected for deletion. Windows counterpart of the Swift Protection.</summary>
public static class Protection
{
    public const string WindowsReason = "This item is part of Windows and is protected.";
    public const string DriveReason = "This is the top of a drive. Open it and pick the items inside instead.";
    public const string HomeReason =
        "This is a standard folder of your user folder. Open it and pick the items inside instead.";
    public const string AppReason = "This item contains the running app.";

    private static readonly HashSet<string> SystemTopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "$Recycle.Bin", "System Volume Information", "Recovery", "Boot", "$WinREAgent", "$SysReset",
        "$Windows.~BT", "$Windows.~WS", "Config.Msi", "Documents and Settings",
        "pagefile.sys", "hiberfil.sys", "swapfile.sys", "DumpStack.log", "DumpStack.log.tmp", "bootmgr", "BOOTNXT",
    };

    private static readonly string[] HomeFolders =
    [
        "", @"\Desktop", @"\Documents", @"\Downloads", @"\Pictures", @"\Music", @"\Videos", @"\Favorites",
        @"\Contacts", @"\Links", @"\Saved Games", @"\Searches", @"\3D Objects", @"\OneDrive", @"\AppData",
        @"\AppData\Local", @"\AppData\LocalLow", @"\AppData\Roaming",
    ];

    public static string? Reason(string path, ProtectionContext? context = null)
    {
        var ctx = context ?? ProtectionContext.Current;
        var p = Canonical(path);
        if (IsDriveRoot(p)) return DriveReason;

        var windows = Canonical(ctx.WindowsDirectory);
        if (windows.Length > 0 && (Same(p, windows) || Inside(p, windows))) return WindowsReason;
        foreach (var directory in ctx.ProgramDirectories)
        {
            var d = Canonical(directory);
            if (d.Length > 0 && Same(p, d)) return WindowsReason;
        }
        if (p.Length > 3 && p[1] == ':' && p[2] == '\\')
        {
            var rest = p[3..];
            var separator = rest.IndexOf('\\');
            var top = separator < 0 ? rest : rest[..separator];
            if (SystemTopLevel.Contains(top)) return WindowsReason;
        }

        var home = Canonical(ctx.Home);
        if (home.Length > 0)
        {
            var users = home[..Math.Max(0, home.LastIndexOf('\\'))];
            if (users.Length > 0 && (Same(p, users) || Same(p, users + @"\Public") || Same(p, users + @"\Default")))
            {
                return WindowsReason;
            }
            if (HomeFolders.Any(folder => Same(p, home + folder))) return HomeReason;
        }

        var app = Canonical(ctx.AppDirectory);
        if (app.Length > 0 && (Same(p, app) || Inside(p, app) || Inside(app, p))) return AppReason;
        return null;
    }

    private static string Canonical(string path) => path.Replace('/', '\\').TrimEnd('\\');

    private static bool IsDriveRoot(string p) => p.Length == 0 || (p.Length == 2 && p[1] == ':');

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool Inside(string path, string directory) =>
        path.StartsWith(directory + "\\", StringComparison.OrdinalIgnoreCase);
}
