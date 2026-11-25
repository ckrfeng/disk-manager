namespace DiskCore;

public readonly record struct Hsb(double H, double S, double B)
{
    /// <summary>Converts to RGB. A boost of 1 lightens the color for hover and selection.</summary>
    public (byte R, byte G, byte B) ToRgb(double boost = 0)
    {
        var s = Math.Max(0, S - boost * 0.25);
        var v = Math.Min(1, B + boost * 0.12);
        var h = (H % 1 + 1) % 1 * 6;
        var c = v * s;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var m = v - c;
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return (ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
}

public readonly record struct Segment(FileNode? Node, int Depth, double Start, double End, Hsb Color);

/// <summary>Rings of the sunburst chart, as fractions of a full turn. Port of the Swift SunburstLayout.</summary>
public sealed class SunburstLayout
{
    public const int MaxDepth = 5;
    private const double MinFraction = 0.003;

    private static readonly Hsb[] Palette =
    [
        new(0.58, 0.72, 0.95), new(0.47, 0.70, 0.80), new(0.31, 0.62, 0.80), new(0.14, 0.78, 0.96),
        new(0.07, 0.78, 0.97), new(0.99, 0.68, 0.94), new(0.89, 0.58, 0.90), new(0.75, 0.52, 0.92),
    ];
    private static readonly Hsb FileGray = new(0.6, 0.04, 0.62);
    private static readonly Hsb OtherGray = new(0.6, 0.05, 0.36);
    private static readonly Hsb HiddenGray = new(0.6, 0.08, 0.26);

    private readonly List<Segment> segments = [];
    private readonly Dictionary<FileNode, Hsb> topColors = [];

    public SunburstLayout()
    {
    }

    public SunburstLayout(FileNode root)
    {
        if (root.Size > 0) Add(root.Children, 0, 0, root.Size, null);
    }

    public IReadOnlyList<Segment> Segments => segments;

    public Hsb ColorForTopLevel(FileNode node) => topColors.TryGetValue(node, out var color) ? color : OtherGray;

    public Segment? SegmentAt(int depth, double fraction)
    {
        foreach (var segment in segments)
        {
            if (segment.Depth == depth && segment.Start <= fraction && fraction < segment.End) return segment;
        }
        return null;
    }

    private void Add(IReadOnlyList<FileNode> children, double start, int depth, double total, Hsb? baseColor)
    {
        var cursor = start;
        var paletteIndex = 0;
        for (var index = 0; index < children.Count; index++)
        {
            var child = children[index];
            var fraction = child.Size / total;
            if (fraction < MinFraction)
            {
                double rest = 0;
                for (var j = index; j < children.Count; j++) rest += children[j].Size;
                rest /= total;
                if (rest >= MinFraction / 2) segments.Add(new Segment(null, depth, cursor, cursor + rest, OtherGray));
                break;
            }

            Hsb color;
            switch (child.Kind)
            {
                case NodeKind.Hidden:
                    color = HiddenGray;
                    break;
                case NodeKind.File or NodeKind.Aggregate:
                    color = baseColor is { } fileBase ? new Hsb(fileBase.H, 0.10, 0.60) : FileGray;
                    break;
                default:
                    if (baseColor is { } b)
                    {
                        var shade = index % 2 == 0 ? 0.0 : 0.07;
                        color = new Hsb(b.H, Math.Max(0.25, b.S - 0.07 * depth), Math.Max(0.35, b.B - 0.05 * depth - shade));
                    }
                    else
                    {
                        color = Palette[paletteIndex % Palette.Length];
                        paletteIndex++;
                    }
                    break;
            }

            if (depth == 0) topColors[child] = color;
            segments.Add(new Segment(child, depth, cursor, cursor + fraction, color));
            if (child.IsDirectory && depth + 1 < MaxDepth) Add(child.Children, cursor, depth + 1, total, baseColor ?? color);
            cursor += fraction;
        }
    }
}
