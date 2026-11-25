using System.IO.Enumeration;
using System.Security;

namespace DiskCore;

/// <summary>
/// Scans a folder tree on several threads. Port of the Swift DiskScanner, using .NET's
/// FileSystemEnumerable (NtQueryDirectoryFile on Windows) instead of getattrlistbulk.
/// </summary>
public sealed class DiskScanner
{
    public readonly record struct Progress(long Bytes, long Files, string Path);

    private readonly record struct Entry(string Name, bool IsDirectory, long Length, FileAttributes Attributes);

    private const int MaxFilesPerDirectory = 64;
    private const int KeptFilesPerDirectory = 48;

    // FILE_ATTRIBUTE_RECALL_ON_OPEN and FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS mark cloud placeholders
    // (OneDrive "online-only" files). Their length is the cloud size, but they take no space on the disk.
    private const FileAttributes CloudOnly =
        (FileAttributes)0x0004_0000 | (FileAttributes)0x0040_0000 | FileAttributes.Offline;

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = 0,
    };

    private readonly object gate = new();
    private readonly Stack<(FileNode Node, string Path)> queue = new();
    private int active;
    private bool cancelled;
    private Progress progress = new(0, 0, "");

    public DiskScanner(string path)
    {
        RootPath = Normalize(path);
    }

    public string RootPath { get; }

    public Progress CurrentProgress
    {
        get
        {
            lock (gate) return progress;
        }
    }

    public static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));

    public void Cancel()
    {
        lock (gate)
        {
            cancelled = true;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>Returns the scanned tree, or null when the path is not a folder or the scan was cancelled.</summary>
    public FileNode? Run()
    {
        if (!Directory.Exists(RootPath)) return null;
        var root = new FileNode(RootPath, NodeKind.Directory);
        lock (gate) queue.Push((root, RootPath));

        var threads = new Thread[Math.Clamp(Environment.ProcessorCount, 2, 8)];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new Thread(Work) { IsBackground = true, Name = "DiskScanner" };
            threads[i].Start();
        }
        foreach (var thread in threads) thread.Join();

        lock (gate)
        {
            if (cancelled) return null;
        }
        root.FinalizeSizes();
        return root;
    }

    private void Work()
    {
        lock (gate)
        {
            while (true)
            {
                while (queue.Count == 0 && active > 0 && !cancelled) Monitor.Wait(gate);
                if (cancelled || !queue.TryPop(out var item)) break;
                active++;
                Monitor.Exit(gate);

                long bytes = 0, files = 0;
                List<(FileNode, string)> subdirectories = [];
                try
                {
                    subdirectories = Scan(item.Node, item.Path, ref bytes, ref files);
                }
                catch (Exception)
                {
                    item.Node.IsUnreadable = true;
                }
                finally
                {
                    Monitor.Enter(gate);
                }

                foreach (var subdirectory in subdirectories) queue.Push(subdirectory);
                active--;
                progress = new Progress(progress.Bytes + bytes, progress.Files + files, item.Path);
                Monitor.PulseAll(gate);
            }
            Monitor.PulseAll(gate);
        }
    }

    private static List<(FileNode, string)> Scan(FileNode node, string path, ref long bytes, ref long files)
    {
        var directories = new List<(FileNode, string)>();
        var fileNodes = new List<FileNode>();
        try
        {
            var entries = new FileSystemEnumerable<Entry>(path,
                static (ref FileSystemEntry e) => new Entry(e.FileName.ToString(), e.IsDirectory, e.Length, e.Attributes),
                Options);
            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    // Junctions, symbolic links and mounted volumes point somewhere else. Following them
                    // would count the same data twice or loop forever.
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    directories.Add((new FileNode(entry.Name, NodeKind.Directory), System.IO.Path.Join(path, entry.Name)));
                    continue;
                }
                var size = (entry.Attributes & CloudOnly) != 0 ? 0 : entry.Length;
                bytes += size;
                files++;
                fileNodes.Add(new FileNode(entry.Name, NodeKind.File, size, 1));
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or SecurityException)
        {
            node.IsUnreadable = true;
        }

        if (fileNodes.Count > MaxFilesPerDirectory)
        {
            fileNodes.Sort((a, b) => b.Size.CompareTo(a.Size));
            var rest = fileNodes.GetRange(KeptFilesPerDirectory, fileNodes.Count - KeptFilesPerDirectory);
            fileNodes.RemoveRange(KeptFilesPerDirectory, rest.Count);
            fileNodes.Add(new FileNode($"{rest.Count:N0} smaller files", NodeKind.Aggregate,
                rest.Sum(f => f.Size), rest.Count));
        }

        var children = new List<FileNode>(directories.Count + fileNodes.Count);
        foreach (var (directory, _) in directories) children.Add(directory);
        children.AddRange(fileNodes);
        node.SetChildren(children);
        return directories;
    }
}

/// <summary>The work done by the elevated copy of the app: scan a path and write the tree to a file.</summary>
public static class HelperScan
{
    public const string Flag = "--helper-scan";

    public static int Run(string path, string output)
    {
        var root = new DiskScanner(path).Run();
        if (root is null) return 1;
        try
        {
            // The unelevated app created this file, so it keeps the user's ownership and stays readable.
            using var stream = new FileStream(output, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.SetLength(0);
            TreeCoding.Encode(root, stream);
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 1;
        }
    }
}
