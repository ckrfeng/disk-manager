using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using DiskCore;

namespace DiskManager;

[Flags]
public enum Change
{
    Disks = 1,
    Scan = 2,
    Progress = 4,
    Result = 8,
    Layout = 16,
    Hover = 32,
    Collected = 64,
}

public sealed class ScanState(string path, bool isAdmin, long? expectedBytes)
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Path { get; } = path;
    public bool IsAdmin { get; } = isAdmin;
    public long? ExpectedBytes { get; } = expectedBytes;
    public DiskScanner.Progress Progress { get; set; }

    public double? Fraction =>
        ExpectedBytes is > 0 && !IsAdmin ? Math.Min(1, (double)Progress.Bytes / ExpectedBytes.Value) : null;
}

public sealed record ScanResult(FileNode Root, DiskInfo? Disk, bool IsAdmin);

/// <summary>App state and actions, on the UI thread. Port of the Swift AppModel.</summary>
public sealed class AppModel
{
    private const long HiddenSpaceThreshold = 100_000_000;

    private readonly DispatcherTimer timer;
    private DiskScanner? scanner;
    private FileNode? hovered;
    private bool isRefreshingDisks;
    private int ticks;

    public AppModel()
    {
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Tick(),
            Dispatcher.CurrentDispatcher);
        timer.Start();
        RefreshDisks();
    }

    public event Action<Change>? Changed;
    public event Action<string>? Error;

    public IReadOnlyList<DiskInfo> Disks { get; private set; } = [];
    public ScanState? Scan { get; private set; }
    public ScanResult? Result { get; private set; }
    public FileNode? Current { get; private set; }
    public FileNode? Selected { get; private set; }
    public SunburstLayout Layout { get; private set; } = new();
    public List<FileNode> Collected { get; } = [];
    public bool IsDeleting { get; private set; }
    public FileNode? Dragged { get; set; }

    public FileNode? Hovered
    {
        get => hovered;
        set
        {
            if (ReferenceEquals(hovered, value)) return;
            hovered = value;
            Changed?.Invoke(Change.Hover);
        }
    }

    public FileNode? Target => Hovered ?? Selected;
    public bool CanGoUp => Current is not null && !ReferenceEquals(Current, Result?.Root);
    public long CollectedSize => Collected.Sum(n => n.Size);

    // Disks

    public async void RefreshDisks()
    {
        if (isRefreshingDisks) return;
        isRefreshingDisks = true;
        try
        {
            // DriveInfo can stall on a sleeping network drive, so it runs off the UI thread.
            var updated = await Task.Run(Volumes.Mounted);
            if (!updated.SequenceEqual(Disks))
            {
                Disks = updated;
                Changed?.Invoke(Change.Disks);
            }
        }
        finally
        {
            isRefreshingDisks = false;
        }
    }

    private void Tick()
    {
        ticks++;
        if (scanner is not null && Scan is not null)
        {
            Scan.Progress = scanner.CurrentProgress;
            Changed?.Invoke(Change.Progress);
        }
        if (ticks % 14 == 0 && Result is null) RefreshDisks();
    }

    // Scanning

    public void StartScan(string path, bool asAdministrator = false)
    {
        if (Scan is not null) return;
        CloseResult();
        var disk = Disks.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
        var state = new ScanState(path, asAdministrator, disk?.Used);
        Scan = state;
        Changed?.Invoke(Change.Scan);

        if (asAdministrator)
        {
            _ = RunScan(state, disk, () => PrivilegedScan(path));
        }
        else
        {
            var scanner = new DiskScanner(path);
            this.scanner = scanner;
            _ = RunScan(state, disk, scanner.Run);
        }
    }

    private async Task RunScan(ScanState state, DiskInfo? disk, Func<FileNode?> scan)
    {
        FileNode? root = null;
        DiskInfo? fresh = null;
        Exception? error = null;
        try
        {
            (root, fresh) = await Task.Run(() =>
            {
                var tree = scan();
                return (tree, tree is not null && disk is not null ? Volumes.Info(disk.Path) : null);
            });
        }
        catch (Exception e)
        {
            error = e;
        }
        FinishScan(state, root, fresh, error);
    }

    private static FileNode? PrivilegedScan(string path)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The app's location is unknown.");
        var output = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DiskManager-scan-{Guid.NewGuid():N}.tree");
        File.WriteAllBytes(output, []);
        try
        {
            var info = new ProcessStartInfo(executable, CommandLine.Join(HelperScan.Flag, path, output))
            {
                UseShellExecute = true,
                Verb = "runas",
            };
            using var process = Process.Start(info) ?? throw new InvalidOperationException("The scan didn't start.");
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("The administrator scan didn't finish.");
            return TreeCoding.Decode(File.ReadAllBytes(output));
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223)
        {
            return null; // The user said no to the administrator prompt.
        }
        finally
        {
            try
            {
                File.Delete(output);
            }
            catch (IOException)
            {
            }
        }
    }

    private void FinishScan(ScanState state, FileNode? root, DiskInfo? fresh, Exception? error)
    {
        if (Scan?.Id != state.Id) return;
        Scan = null;
        scanner = null;
        Changed?.Invoke(Change.Scan);
        if (error is not null)
        {
            Error?.Invoke($"The scan failed. {error.Message}");
            return;
        }
        if (root is null)
        {
            if (!state.IsAdmin && !Directory.Exists(state.Path)) Error?.Invoke($"“{state.Path}” could not be read.");
            return;
        }
        if (fresh is not null && fresh.Used - root.Size > HiddenSpaceThreshold)
        {
            root.Append(new FileNode("hidden space", NodeKind.Hidden, fresh.Used - root.Size));
        }
        Result = new ScanResult(root, fresh, state.IsAdmin);
        Changed?.Invoke(Change.Result);
        Navigate(root);
    }

    public void CancelScan()
    {
        scanner?.Cancel();
        scanner = null;
        Scan = null;
        Changed?.Invoke(Change.Scan);
    }

    public void CloseResult()
    {
        if (Result is null) return;
        foreach (var node in Collected) node.Reattach();
        Collected.Clear();
        Result = null;
        Current = null;
        hovered = null;
        Selected = null;
        Layout = new SunburstLayout();
        Changed?.Invoke(Change.Result | Change.Layout | Change.Collected | Change.Hover);
        RefreshDisks();
    }

    public void Rescan(bool asAdministrator)
    {
        if (Result?.Root.FullPath is not { } path) return;
        StartScan(path, asAdministrator);
    }

    // Navigation

    public void Navigate(FileNode node)
    {
        if (!node.IsDirectory) return;
        Current = node;
        hovered = null;
        Selected = null;
        RebuildLayout();
    }

    public void GoUp()
    {
        if (CanGoUp && Current?.Parent is { } parent) Navigate(parent);
    }

    /// <summary>Opens a folder, explains hidden space, or selects anything else.</summary>
    public bool Activate(FileNode node)
    {
        switch (node.Kind)
        {
            case NodeKind.Directory when node.Children.Count > 0:
                Navigate(node);
                return false;
            case NodeKind.Hidden:
                return true;
            default:
                Select(node);
                return false;
        }
    }

    public void Select(FileNode? node)
    {
        if (ReferenceEquals(Selected, node)) return;
        Selected = node;
        Changed?.Invoke(Change.Hover);
    }

    private void RebuildLayout()
    {
        Layout = Current is null ? new SunburstLayout() : new SunburstLayout(Current);
        Changed?.Invoke(Change.Layout | Change.Hover);
    }

    // Items

    public void Reveal(FileNode node)
    {
        if (!node.IsReal) return;
        try
        {
            Process.Start("explorer.exe", "/select," + CommandLine.Quote(node.FullPath))?.Dispose();
        }
        catch (Win32Exception e)
        {
            Error?.Invoke($"File Explorer couldn't be opened. {e.Message}");
        }
    }

    public void Collect(FileNode node)
    {
        if (!node.IsReal || node.Parent is null || Current is null || ReferenceEquals(node, Current)
            || !node.IsDescendantOf(Current) || IsDeleting)
        {
            return;
        }
        if (Protection.Reason(node.FullPath) is { } reason)
        {
            Error?.Invoke($"“{node.Name}” can’t be collected. {reason}");
            return;
        }
        node.Detach();
        Collected.Add(node);
        if (hovered?.IsDescendantOf(node) == true) hovered = null;
        if (Selected?.IsDescendantOf(node) == true) Selected = null;
        Changed?.Invoke(Change.Collected);
        RebuildLayout();
    }

    public void Uncollect(FileNode node)
    {
        if (IsDeleting || !Collected.Remove(node)) return;
        node.Reattach();
        Changed?.Invoke(Change.Collected);
        RebuildLayout();
    }

    public async void DeleteCollected(bool permanently)
    {
        if (Collected.Count == 0 || IsDeleting) return;
        var items = Collected.ToList();
        var paths = items.Select(n => n.FullPath).ToList();
        IsDeleting = true;
        Changed?.Invoke(Change.Collected);

        var failures = await Native.RunOnStaThread(() =>
        {
            var failed = new SortedDictionary<int, string>();
            for (var i = 0; i < paths.Count; i++)
            {
                try
                {
                    if (permanently) Native.DeletePermanently(paths[i]);
                    else Native.MoveToRecycleBin(paths[i]);
                }
                catch (Exception e)
                {
                    // Anything that goes wrong puts the item back; nothing is half-reported.
                    failed[i] = e.Message;
                }
            }
            return failed;
        });

        IsDeleting = false;
        Collected.Clear();
        foreach (var index in failures.Keys) items[index].Reattach();
        Changed?.Invoke(Change.Collected);
        RebuildLayout();
        if (failures.Count > 0)
        {
            var details = failures.Take(5).Select(f => $"• {items[f.Key].Name}: {f.Value}");
            Error?.Invoke($"{failures.Count} of {items.Count} items could not be deleted and were put back.\n"
                + string.Join("\n", details));
        }
    }
}
