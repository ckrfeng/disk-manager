using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DiskCore;

namespace DiskManager;

/// <summary>Draws the sunburst chart and reports hover, clicks and drags. Port of the SwiftUI SunburstView.</summary>
public sealed class SunburstControl : FrameworkElement
{
    private static readonly Color Background = Color.FromRgb(0x1B, 0x1C, 0x21);
    private static readonly Pen SegmentPen = Frozen(new Pen(new SolidColorBrush(Background), 1));
    private static readonly Pen SelectedPen = Frozen(new Pen(Brushes.White, 1.5));
    private static readonly Brush TextBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF5)));
    private static readonly Brush SecondaryBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0x9C, 0xA5)));
    private static readonly Typeface TitleFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface DetailFace = new("Segoe UI");
    private static readonly Typeface IconFace = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const double AnimationSeconds = 0.45;

    private SunburstLayout layout = new();
    private FileNode? highlighted;
    private FileNode? selected;
    private string title = "";
    private string detail = "";
    private bool canGoUp;
    private double progress = 1;
    private Stopwatch? animation;
    private FileNode? hoveredNode;
    private Point? pressPoint;

    public SunburstControl()
    {
        Focusable = false;
        ClipToBounds = true;
    }

    public event Action<FileNode?>? HoverChanged;
    public event Action? CenterClicked;
    public event Action<FileNode>? SegmentClicked;
    public event Action? EmptyClicked;
    public event Action<FileNode>? DragStarted;
    public event Action<FileNode, Point>? MenuRequested;

    public void SetLayout(SunburstLayout newLayout, string newTitle, string newDetail, bool newCanGoUp)
    {
        layout = newLayout;
        title = newTitle;
        detail = newDetail;
        canGoUp = newCanGoUp;
        hoveredNode = null;
        if (animation is null) CompositionTarget.Rendering += Animate;
        animation = Stopwatch.StartNew();
        progress = 0;
        InvalidateVisual();
    }

    public void SetHighlight(FileNode? newHighlighted, FileNode? newSelected)
    {
        if (ReferenceEquals(highlighted, newHighlighted) && ReferenceEquals(selected, newSelected)) return;
        highlighted = newHighlighted;
        selected = newSelected;
        InvalidateVisual();
    }

    private void Animate(object? sender, EventArgs e)
    {
        if (animation is null) return;
        var t = Math.Min(1, animation.Elapsed.TotalSeconds / AnimationSeconds);
        progress = 1 - Math.Pow(1 - t, 3);
        if (t >= 1)
        {
            CompositionTarget.Rendering -= Animate;
            animation = null;
            progress = 1;
        }
        InvalidateVisual();
    }

    // Geometry

    private readonly record struct RingGeometry(Point Center, double Inner, double Ring);

    private RingGeometry Measure()
    {
        var size = RenderSize;
        var radius = Math.Max(40, Math.Min(size.Width, size.Height) / 2 - 8);
        var inner = radius * 0.26;
        return new RingGeometry(new Point(size.Width / 2, size.Height / 2), inner, (radius - inner) / SunburstLayout.MaxDepth);
    }

    private enum HitKind { Center, Ring, Outside }

    private (HitKind Kind, int Depth, double Fraction) Hit(Point point)
    {
        var g = Measure();
        double dx = point.X - g.Center.X, dy = point.Y - g.Center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < g.Inner) return (HitKind.Center, 0, 0);
        var depth = (int)((distance - g.Inner) / g.Ring);
        if (depth >= SunburstLayout.MaxDepth) return (HitKind.Outside, 0, 0);
        var fraction = (Math.Atan2(dy, dx) + Math.PI / 2) / (2 * Math.PI);
        if (fraction < 0) fraction += 1;
        return (HitKind.Ring, depth, fraction);
    }

    private FileNode? NodeAt(Point point)
    {
        var hit = Hit(point);
        return hit.Kind == HitKind.Ring ? layout.SegmentAt(hit.Depth, hit.Fraction)?.Node : null;
    }

    // Drawing

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        var g = Measure();
        var scale = 0.86 + 0.14 * progress;
        foreach (var segment in layout.Segments)
        {
            var isHighlighted = highlighted is not null && segment.Node?.IsDescendantOf(highlighted) == true;
            var isSelected = selected is not null && ReferenceEquals(segment.Node, selected);
            var dimmed = highlighted is not null && !isHighlighted;
            var r0 = (g.Inner + segment.Depth * g.Ring) * scale;
            var r1 = r0 + (g.Ring - 1.5) * scale;
            var a0 = segment.Start * progress * 2 * Math.PI - Math.PI / 2;
            var a1 = segment.End * progress * 2 * Math.PI - Math.PI / 2;
            if (a1 - a0 < 0.0004) continue;

            var (r, gr, b) = segment.Color.ToRgb(isHighlighted || isSelected ? 1 : 0);
            var alpha = (byte)Math.Round(255 * (dimmed ? 0.5 : 1) * progress);
            var fill = new SolidColorBrush(Color.FromArgb(alpha, r, gr, b));
            fill.Freeze();
            dc.DrawGeometry(fill, isSelected ? SelectedPen : SegmentPen, Ring(g.Center, r0, r1, a0, a1));
        }
        DrawCenter(dc, g);
    }

    private static StreamGeometry Ring(Point c, double r0, double r1, double a0, double a1)
    {
        // Each arc is drawn in two halves, so a segment covering the whole circle still renders.
        var mid = (a0 + a1) / 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(At(c, r1, a0), isFilled: true, isClosed: true);
            ctx.ArcTo(At(c, r1, mid), new Size(r1, r1), 0, false, SweepDirection.Clockwise, true, false);
            ctx.ArcTo(At(c, r1, a1), new Size(r1, r1), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(At(c, r0, a1), true, false);
            ctx.ArcTo(At(c, r0, mid), new Size(r0, r0), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.ArcTo(At(c, r0, a0), new Size(r0, r0), 0, false, SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point At(Point c, double r, double angle) => new(c.X + r * Math.Cos(angle), c.Y + r * Math.Sin(angle));

    private void DrawCenter(DrawingContext dc, RingGeometry g)
    {
        if (title.Length == 0) return;
        var width = g.Inner * 1.6;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var lines = new List<FormattedText>();
        if (canGoUp) lines.Add(Text("\uE70E", IconFace, 11, SecondaryBrush, width, dpi));
        var name = Text(title, TitleFace, 13, TextBrush, width, dpi);
        name.MaxLineCount = 2;
        name.Trimming = TextTrimming.CharacterEllipsis;
        lines.Add(name);
        lines.Add(Text(detail, DetailFace, 12, SecondaryBrush, width, dpi));

        var y = g.Center.Y - lines.Sum(l => l.Height + 3) / 2;
        foreach (var line in lines)
        {
            dc.DrawText(line, new Point(g.Center.X - width / 2, y));
            y += line.Height + 3;
        }
    }

    private static FormattedText Text(string text, Typeface face, double size, Brush brush, double width, double dpi) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, size, brush, dpi)
        {
            MaxTextWidth = Math.Max(1, width),
            TextAlignment = TextAlignment.Center,
        };

    // Input

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (pressPoint is { } start && e.LeftButton == MouseButtonState.Pressed
            && (Math.Abs(point.X - start.X) > SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(point.Y - start.Y) > SystemParameters.MinimumVerticalDragDistance))
        {
            pressPoint = null;
            if (NodeAt(start) is { IsReal: true } node) DragStarted?.Invoke(node);
            return;
        }
        Cursor = canGoUp && Hit(point).Kind == HitKind.Center ? Cursors.Hand : null;
        SetHovered(NodeAt(point));
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        pressPoint = null;
        SetHovered(null);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        pressPoint = e.GetPosition(this);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (pressPoint is null) return;
        pressPoint = null;
        var point = e.GetPosition(this);
        var hit = Hit(point);
        switch (hit.Kind)
        {
            case HitKind.Center:
                CenterClicked?.Invoke();
                break;
            case HitKind.Ring:
                if (NodeAt(point) is { } node) SegmentClicked?.Invoke(node);
                break;
            default:
                EmptyClicked?.Invoke();
                break;
        }
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonUp(e);
        if (NodeAt(e.GetPosition(this)) is { } node)
        {
            MenuRequested?.Invoke(node, e.GetPosition(this));
            e.Handled = true;
        }
    }

    private void SetHovered(FileNode? node)
    {
        if (ReferenceEquals(hoveredNode, node)) return;
        hoveredNode = node;
        HoverChanged?.Invoke(node);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
