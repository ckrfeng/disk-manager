using DiskCore;

namespace DiskCore.Tests;

public sealed class ScannerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "DiskCoreTests-" + Guid.NewGuid());

    public ScannerTests()
    {
        Directory.CreateDirectory(Path.Combine(root, "a", "b"));
        Directory.CreateDirectory(Path.Combine(root, "many"));
        File.WriteAllBytes(Path.Combine(root, "a", "big.bin"), new byte[300_000]);
        File.WriteAllBytes(Path.Combine(root, "a", "b", "mid.bin"), new byte[50_000]);
        File.WriteAllBytes(Path.Combine(root, "small.txt"), new byte[10]);
        for (var i = 0; i < 100; i++) File.WriteAllBytes(Path.Combine(root, "many", $"f{i}"), new byte[1000 + i]);
    }

    public void Dispose()
    {
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, (UnixFileMode)0b111_000_000);
        }
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ScanCountsFilesAndSizes()
    {
        var tree = new DiskScanner(root).Run();
        Assert.NotNull(tree);
        Assert.Equal(103, tree.FileCount);
        var expected = 300_000 + 50_000 + 10 + Enumerable.Range(0, 100).Sum(i => 1000 + i);
        Assert.Equal(expected, tree.Size);
        Assert.Equal(tree.Children.Select(c => c.Size).OrderByDescending(s => s), tree.Children.Select(c => c.Size));

        var a = tree.Children.Single(c => c.Name == "a");
        Assert.Equal(350_000, a.Size);
        var mid = a.Children.Single(c => c.Name == "b").Children.Single();
        Assert.Equal(Path.Combine(DiskScanner.Normalize(root), "a", "b", "mid.bin"), mid.FullPath);
    }

    [Fact]
    public void ManySmallFilesAreAggregated()
    {
        var tree = new DiskScanner(root).Run()!;
        var many = tree.Children.Single(c => c.Name == "many");
        Assert.Equal(49, many.Children.Count);
        var aggregate = many.Children.Single(c => c.Kind == NodeKind.Aggregate);
        Assert.Equal(52, aggregate.FileCount);
        Assert.Equal(100, many.FileCount);
    }

    [Fact]
    public void LinkedFoldersAreNotFollowed()
    {
        var link = Path.Combine(root, "loop");
        try
        {
            Directory.CreateSymbolicLink(link, root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // Creating links needs Developer Mode or administrator rights on Windows.
        }
        try
        {
            var tree = new DiskScanner(root).Run()!;
            Assert.DoesNotContain(tree.Children, c => c.Name == "loop" && c.IsDirectory);
            Assert.Equal(103, tree.FileCount);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void UnreadableFolderIsMarked()
    {
        if (OperatingSystem.IsWindows()) return; // Denying access needs ACL edits; covered by hand on Windows.
        var locked = Path.Combine(root, "locked");
        Directory.CreateDirectory(locked);
        File.WriteAllBytes(Path.Combine(locked, "secret"), new byte[5000]);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        try
        {
            if (Directory.EnumerateFileSystemEntries(locked).Any()) return; // Running as root can read anyway.
        }
        catch (UnauthorizedAccessException)
        {
            // Expected: the folder really is locked.
        }

        var tree = new DiskScanner(root).Run()!;
        var node = tree.Children.Single(c => c.Name == "locked");
        Assert.True(node.IsUnreadable);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void MissingPathReturnsNull()
    {
        Assert.Null(new DiskScanner(Path.Combine(root, "nope")).Run());
    }

    [Fact]
    public void DetachAndReattachKeepTotals()
    {
        var tree = new DiskScanner(root).Run()!;
        long total = tree.Size, count = tree.FileCount;
        var a = tree.Children.Single(c => c.Name == "a");
        var b = a.Children.Single(c => c.Name == "b");
        b.Detach();
        Assert.Equal(total - b.Size, tree.Size);
        Assert.Equal(count - 1, tree.FileCount);
        Assert.DoesNotContain(b, a.Children);
        b.Reattach();
        Assert.Equal(total, tree.Size);
        Assert.Equal(count, tree.FileCount);
        Assert.Contains(b, a.Children);
        Assert.True(b.IsDescendantOf(tree));
    }

    [Fact]
    public void TreeCodingRoundTrips()
    {
        var tree = new DiskScanner(root).Run()!;
        tree.Children[0].IsUnreadable = true;
        var decoded = TreeCoding.Decode(TreeCoding.Encode(tree));
        Assert.Equal(Flatten(tree), Flatten(decoded));
        Assert.True(decoded.Children[0].IsUnreadable);
        Assert.Throws<InvalidDataException>(() => TreeCoding.Decode(TreeCoding.Encode(tree)[..20]));
        Assert.Throws<InvalidDataException>(() => TreeCoding.Decode(new byte[] { 1, 2, 3, 4, 5 }));
    }

    [Fact]
    public void HelperScanWritesTree()
    {
        var output = Path.Combine(Path.GetTempPath(), "DiskCoreTests-" + Guid.NewGuid() + ".tree");
        File.WriteAllBytes(output, new byte[10]);
        try
        {
            Assert.Equal(0, HelperScan.Run(root, output));
            var decoded = TreeCoding.Decode(File.ReadAllBytes(output));
            Assert.Equal(103, decoded.FileCount);
            Assert.Equal(1, HelperScan.Run(Path.Combine(root, "nope"), output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    private static List<string> Flatten(FileNode node)
    {
        var lines = new List<string> { $"{node.FullPath}|{node.Kind}|{node.Size}|{node.FileCount}|{node.IsUnreadable}" };
        foreach (var child in node.Children) lines.AddRange(Flatten(child));
        return lines;
    }
}

public sealed class ProtectionTests
{
    private static readonly ProtectionContext Context = new(@"C:\Windows",
        [@"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData"], @"C:\Users\carter",
        @"C:\Tools\DiskManager\");

    [Theory]
    [InlineData(@"C:\", Protection.DriveReason)]
    [InlineData(@"D:\", Protection.DriveReason)]
    [InlineData(@"C:\Windows", Protection.WindowsReason)]
    [InlineData(@"c:\windows\System32\drivers", Protection.WindowsReason)]
    [InlineData(@"C:\Program Files", Protection.WindowsReason)]
    [InlineData(@"C:\ProgramData", Protection.WindowsReason)]
    [InlineData(@"C:\pagefile.sys", Protection.WindowsReason)]
    [InlineData(@"C:\$Recycle.Bin\S-1-5-21", Protection.WindowsReason)]
    [InlineData(@"D:\System Volume Information", Protection.WindowsReason)]
    [InlineData(@"C:\Users", Protection.WindowsReason)]
    [InlineData(@"C:\Users\Public", Protection.WindowsReason)]
    [InlineData(@"C:\Users\carter", Protection.HomeReason)]
    [InlineData(@"C:\Users\carter\Downloads\", Protection.HomeReason)]
    [InlineData(@"C:\Users\carter\AppData\Local", Protection.HomeReason)]
    [InlineData(@"C:\Tools", Protection.AppReason)]
    [InlineData(@"C:\Tools\DiskManager", Protection.AppReason)]
    [InlineData(@"C:\Tools\DiskManager\DiskManager.exe", Protection.AppReason)]
    public void ProtectedPaths(string path, string reason)
    {
        Assert.Equal(reason, Protection.Reason(path, Context));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Some App")]
    [InlineData(@"C:\Users\carter\Downloads\movie.mkv")]
    [InlineData(@"C:\Users\carter\AppData\Local\Temp")]
    [InlineData(@"C:\Windowsish")]
    [InlineData(@"D:\Games")]
    [InlineData(@"C:\Users\other\Documents")]
    public void UnprotectedPaths(string path)
    {
        Assert.Null(Protection.Reason(path, Context));
    }
}

public sealed class FormattingTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1_500_000, "1.43 MB")]
    [InlineData(15_000_000_000, "14.0 GB")]
    [InlineData(1_023_000_000, "976 MB")]
    [InlineData(1_073_000_000, "1.00 GB")]
    [InlineData(2_097_152_000_000, "1.91 TB")]
    public void BytesMatchExplorer(long bytes, string expected)
    {
        using var _ = new InvariantCulture();
        Assert.Equal(expected, ByteFormat.Format(bytes));
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData(@"C:\Program Files\x", "\"C:\\Program Files\\x\"")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData(@"D:\My Stuff\", "\"D:\\My Stuff\\\\\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("", "\"\"")]
    public void ArgumentsAreQuoted(string argument, string expected)
    {
        Assert.Equal(expected, CommandLine.Quote(argument));
    }

    [Fact]
    public void DriveNamesMatchExplorer()
    {
        Assert.Equal("Local Disk (C:)", Volumes.DisplayName(@"C:\", "", DiskKind.Fixed));
        Assert.Equal("Backup (E:)", Volumes.DisplayName(@"e:\", "Backup", DiskKind.Removable));
        Assert.Equal("USB Drive (F:)", Volumes.DisplayName(@"F:\", null, DiskKind.Removable));
    }

    private sealed class InvariantCulture : IDisposable
    {
        private readonly System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;

        public InvariantCulture() => System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = previous;
    }
}

public sealed class LayoutTests
{
    [Fact]
    public void SegmentsCoverTheRingInOrder()
    {
        var root = new FileNode("root", NodeKind.Directory);
        var big = new FileNode("big", NodeKind.Directory);
        big.Append(new FileNode("inner", NodeKind.File, 600, 1));
        root.Append(big);
        root.Append(new FileNode("file", NodeKind.File, 398, 1));
        root.Append(new FileNode("tiny", NodeKind.File, 2, 1));

        var layout = new SunburstLayout(root);
        var top = layout.Segments.Where(s => s.Depth == 0).ToList();
        Assert.Equal(new string?[] { "big", "file", null }, top.Select(s => s.Node?.Name));
        Assert.Equal(0.6, top[0].End, 6);
        Assert.Equal(1.0, top[^1].End, 6);
        Assert.Same(big, layout.SegmentAt(0, 0.3)?.Node);
        Assert.Equal("inner", layout.SegmentAt(1, 0.3)?.Node?.Name);
        Assert.Null(layout.SegmentAt(1, 0.9));
        Assert.NotEqual(layout.ColorForTopLevel(big), layout.ColorForTopLevel(root.Children[1]));
    }

    [Fact]
    public void HsbConvertsToRgb()
    {
        Assert.Equal(((byte)255, (byte)0, (byte)0), new Hsb(0, 1, 1).ToRgb());
        Assert.Equal(((byte)0, (byte)0, (byte)255), new Hsb(2.0 / 3, 1, 1).ToRgb());
        Assert.Equal(((byte)128, (byte)128, (byte)128), new Hsb(0.3, 0, 0.5).ToRgb());
    }
}
