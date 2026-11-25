using System.Text;

namespace DiskCore;

/// <summary>Compact binary form of a scanned tree, same layout as the Swift TreeCoding.</summary>
public static class TreeCoding
{
    private const uint Magic = 0x444D_5431;

    public static byte[] Encode(FileNode root)
    {
        using var stream = new MemoryStream();
        Encode(root, stream);
        return stream.ToArray();
    }

    public static void Encode(FileNode root, Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        Write(root, writer);
    }

    public static FileNode Decode(byte[] data)
    {
        using var stream = new MemoryStream(data, writable: false);
        return Decode(stream);
    }

    public static FileNode Decode(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        try
        {
            if (reader.ReadUInt32() != Magic) throw new InvalidDataException("The scan result is not valid.");
            return Read(reader);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("The scan result is truncated.");
        }
    }

    private static void Write(FileNode node, BinaryWriter writer)
    {
        var name = Encoding.UTF8.GetBytes(node.Name);
        var length = Math.Min(name.Length, ushort.MaxValue);
        writer.Write((byte)((byte)node.Kind | (node.IsUnreadable ? 0x80 : 0)));
        writer.Write(node.Size);
        writer.Write(node.FileCount);
        writer.Write((ushort)length);
        writer.Write(name, 0, length);
        writer.Write((uint)node.Children.Count);
        foreach (var child in node.Children) Write(child, writer);
    }

    private static FileNode Read(BinaryReader reader)
    {
        var flags = reader.ReadByte();
        var kind = flags & 0x7F;
        if (kind > (int)NodeKind.Hidden) throw new InvalidDataException("The scan result is not valid.");
        var size = reader.ReadInt64();
        var fileCount = reader.ReadInt64();
        var nameLength = reader.ReadUInt16();
        var name = reader.ReadBytes(nameLength);
        if (name.Length != nameLength) throw new EndOfStreamException();
        var childCount = reader.ReadUInt32();

        var node = new FileNode(Encoding.UTF8.GetString(name), (NodeKind)kind, size, fileCount)
        {
            IsUnreadable = (flags & 0x80) != 0,
        };
        var children = new List<FileNode>((int)Math.Min(childCount, 1u << 16));
        for (uint i = 0; i < childCount; i++) children.Add(Read(reader));
        node.SetChildren(children);
        return node;
    }
}
