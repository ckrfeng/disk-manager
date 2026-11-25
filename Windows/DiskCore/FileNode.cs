namespace DiskCore;

public enum NodeKind : byte
{
    File,
    Directory,
    Aggregate,
    Hidden,
}

/// <summary>One file, folder, or synthetic entry in a scanned tree. Mirrors the Swift FileNode.</summary>
public sealed class FileNode
{
    private static readonly Comparison<FileNode> BySizeDescending = (a, b) => b.Size.CompareTo(a.Size);

    private List<FileNode> children = [];

    public FileNode(string name, NodeKind kind, long size = 0, long fileCount = 0)
    {
        Name = name;
        Kind = kind;
        Size = size;
        FileCount = fileCount;
    }

    public string Name { get; }
    public NodeKind Kind { get; }
    public long Size { get; internal set; }
    public long FileCount { get; internal set; }
    public IReadOnlyList<FileNode> Children => children;
    public bool IsUnreadable { get; internal set; }
    public FileNode? Parent { get; private set; }
    internal bool IsExcluded { get; set; }

    public bool IsDirectory => Kind == NodeKind.Directory;
    public bool IsReal => Kind is NodeKind.File or NodeKind.Directory;

    public string FullPath => Parent is null ? Name : System.IO.Path.Join(Parent.FullPath, Name);

    public string DisplayName
    {
        get
        {
            if (Parent is not null) return Name;
            var trimmed = System.IO.Path.TrimEndingDirectorySeparator(Name);
            var leaf = System.IO.Path.GetFileName(trimmed);
            return string.IsNullOrEmpty(leaf) ? Name : leaf;
        }
    }

    public bool IsDescendantOf(FileNode other)
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (ReferenceEquals(node, other)) return true;
        }
        return false;
    }

    internal void SetChildren(List<FileNode> nodes)
    {
        children = nodes;
        foreach (var node in nodes) node.Parent = this;
    }

    internal long FinalizeSizes()
    {
        if (Kind != NodeKind.Directory) return Size;
        children.RemoveAll(c => c.IsExcluded);
        long total = 0, count = 0;
        foreach (var child in children)
        {
            total += child.FinalizeSizes();
            count += child.FileCount;
        }
        Size = total;
        FileCount = count;
        children.Sort(BySizeDescending);
        return total;
    }

    public void Append(FileNode child)
    {
        child.Parent = this;
        children.Add(child);
        children.Sort(BySizeDescending);
        Adjust(child.Size, child.FileCount);
    }

    /// <summary>Removes this node from its parent but keeps the parent link so it can be put back.</summary>
    public void Detach()
    {
        if (Parent is null) return;
        var index = Parent.children.FindIndex(c => ReferenceEquals(c, this));
        if (index < 0) return;
        Parent.children.RemoveAt(index);
        Parent.Adjust(-Size, -FileCount);
    }

    public void Reattach()
    {
        if (Parent is null || Parent.children.Exists(c => ReferenceEquals(c, this))) return;
        var index = Parent.children.FindIndex(c => c.Size < Size);
        Parent.children.Insert(index < 0 ? Parent.children.Count : index, this);
        Parent.Adjust(Size, FileCount);
    }

    private void Adjust(long sizeDelta, long countDelta)
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            node.Size += sizeDelta;
            node.FileCount += countDelta;
            node.Parent?.children.Sort(BySizeDescending);
        }
    }
}
