using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DiskCore;

namespace DiskManager;

public partial class MainWindow : Window
{
    private const string NodeFormat = "DiskManager.Node";
    private const int MaxSidebarRows = 400;

    private readonly AppModel model = new();
    private readonly List<(FileNode Node, Border Row)> sidebarRows = [];
    private ScanProgressView? progressView;
    private Popup? collectedPopup;
    private Point? rowPress;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.UseDarkTitleBar(this);

        model.Changed += OnModelChanged;
        model.Error += message => MessageBox.Show(this, message, "DiskManager", MessageBoxButton.OK, MessageBoxImage.Warning);

        Sunburst.HoverChanged += node => model.Hovered = node;
        Sunburst.CenterClicked += model.GoUp;
        Sunburst.SegmentClicked += Activate;
        Sunburst.EmptyClicked += () => model.Select(null);
        Sunburst.DragStarted += node => StartDrag(Sunburst, node);
        Sunburst.MenuRequested += (node, _) => ShowItemMenu(node, Sunburst);

        Collector.DragEnter += Collector_DragOver;
        Collector.DragOver += Collector_DragOver;
        Collector.DragLeave += (_, _) => HighlightCollector(false);
        Collector.Drop += Collector_Drop;

        DragEnter += Window_DragOver;
        DragOver += Window_DragOver;
        DragLeave += (_, _) => DropHighlight.Visibility = Visibility.Collapsed;
        Drop += Window_Drop;

        RenderDisks();
        RenderResult();
        RenderCollector();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);

    private void OnModelChanged(Change change)
    {
        if (change.HasFlag(Change.Disks) || change.HasFlag(Change.Scan)) RenderDisks();
        else if (change.HasFlag(Change.Progress)) progressView?.Update(model.Scan);
        if (change.HasFlag(Change.Result)) RenderResult();
        if (change.HasFlag(Change.Layout)) RenderLayout();
        else if (change.HasFlag(Change.Hover)) RenderHover();
        if (change.HasFlag(Change.Collected)) RenderCollector();
    }

    // Drives

    private void RenderDisks()
    {
        DiskRows.Children.Clear();
        progressView = null;
        foreach (var disk in model.Disks) DiskRows.Children.Add(DiskRow(disk));
        if (model.Scan is { } scan && !model.Disks.Any(d => SamePath(d.Path, scan.Path)))
        {
            var name = System.IO.Path.GetFileName(scan.Path.TrimEnd('\\', '/'));
            DiskRows.Children.Add(Card(Glyph("\uE8B7", 34), string.IsNullOrEmpty(name) ? scan.Path : name, null,
                ProgressContent(scan), null));
        }
        ScanFolderButton.IsEnabled = model.Scan is null;
    }

    private Border DiskRow(DiskInfo disk)
    {
        var scan = model.Scan is { } s && SamePath(s.Path, disk.Path) ? s : null;
        var glyph = disk.Kind switch
        {
            DiskKind.Removable => "\uE88E",
            DiskKind.Network => "\uE8CE",
            _ => "\uEDA2",
        };
        var free = $"{ByteFormat.Format(disk.Available)} free of {ByteFormat.Format(disk.Total)}";
        UIElement body = scan is not null ? ProgressContent(scan) : UsageBar(disk.UsedFraction);
        return Card(Glyph(glyph, 34), disk.Name, (disk.Format, free), body, scan is null ? ScanButtons(disk) : null);
    }

    private UIElement ProgressContent(ScanState scan)
    {
        progressView = new ScanProgressView(model.CancelScan);
        progressView.Update(scan);
        return progressView.Root;
    }

    private static Border Card(UIElement icon, string name, (string Format, string Free)? details, UIElement body,
        UIElement? trailing)
    {
        var title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Bottom };
        title.Inlines.Add(new Run(name) { FontSize = 15, FontWeight = FontWeights.SemiBold });
        var header = new DockPanel();
        if (details is { } d)
        {
            var free = new TextBlock
            {
                Text = d.Free,
                FontSize = 12,
                Foreground = Brush("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12, 0, 0, 0),
            };
            DockPanel.SetDock(free, Dock.Right);
            header.Children.Add(free);
            title.Inlines.Add(new Run("   " + d.Format) { FontSize = 11.5, Foreground = Brush("TertiaryTextBrush") });
        }
        header.Children.Add(title);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        info.Children.Add(header);
        info.Children.Add(body);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(icon);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            grid.Children.Add(trailing);
        }
        return new Border
        {
            Background = Brush("PanelBrush"),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10),
            Child = grid,
        };
    }

    private static Grid UsageBar(double fraction)
    {
        var hsb = fraction switch
        {
            < 0.75 => new Hsb(0.38, 0.65, 0.78),
            < 0.9 => new Hsb(0.13, 0.8, 0.95),
            _ => new Hsb(0.0, 0.7, 0.92),
        };
        var (r, g, b) = hsb.ToRgb();
        var bar = new Grid { Height = 6, Margin = new Thickness(0, 9, 0, 0) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(fraction, 0.005), GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(1 - fraction, 0.0001), GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(0x1A, 255, 255, 255)) };
        Grid.SetColumnSpan(track, 2);
        bar.Children.Add(track);
        bar.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            MinWidth = 6,
            Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
        });
        return bar;
    }

    private StackPanel ScanButtons(DiskInfo disk)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        var scan = new Button { Content = "Scan", IsEnabled = model.Scan is null };
        scan.Click += (_, _) => model.StartScan(disk.Path);
        var more = new Button
        {
            Content = Glyph("\uE70D", 10),
            Padding = new Thickness(7, 5, 7, 5),
            Margin = new Thickness(2, 0, 0, 0),
            IsEnabled = model.Scan is null,
            ToolTip = "More scan options",
        };
        more.Click += (_, _) =>
        {
            var menu = new ContextMenu { PlacementTarget = more, Placement = PlacementMode.Bottom };
            menu.Items.Add(MenuItem("Scan as Administrator…", () => model.StartScan(disk.Path, asAdministrator: true)));
            menu.IsOpen = true;
        };
        panel.Children.Add(scan);
        panel.Children.Add(more);
        return panel;
    }

    private void ScanFolder_Click(object sender, RoutedEventArgs e) => ChooseFolder();

    private void ChooseFolder()
    {
        if (model.Scan is not null) return;
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Choose a folder to scan" };
        if (dialog.ShowDialog(this) == true) model.StartScan(dialog.FolderName);
    }

    // Result

    private void RenderResult()
    {
        var hasResult = model.Result is not null;
        DiskPanel.Visibility = hasResult ? Visibility.Collapsed : Visibility.Visible;
        ResultPanel.Visibility = hasResult ? Visibility.Visible : Visibility.Collapsed;
        AdminLabel.Visibility = model.Result?.IsAdmin == true ? Visibility.Visible : Visibility.Collapsed;
        if (!hasResult && collectedPopup is not null) collectedPopup.IsOpen = false;
    }

    private void RenderLayout()
    {
        var current = model.Current;
        Sunburst.SetLayout(model.Layout, current?.DisplayName ?? "", current is null ? "" : ByteFormat.Format(current.Size),
            model.CanGoUp);
        RenderCrumbs();
        RenderSidebar();
        RenderHover();
    }

    private void RenderCrumbs()
    {
        Crumbs.Children.Clear();
        var trail = new List<FileNode>();
        for (var node = model.Current; node is not null; node = node.Parent) trail.Insert(0, node);
        for (var i = 0; i < trail.Count; i++)
        {
            var node = trail[i];
            if (i > 0)
            {
                var chevron = Glyph("\uE76C", 9, "TertiaryTextBrush");
                chevron.Margin = new Thickness(2, 0, 2, 0);
                Crumbs.Children.Add(chevron);
            }
            var label = i == 0 ? model.Result?.Disk?.Name ?? node.DisplayName : node.Name;
            var crumb = new Button
            {
                Content = label,
                Style = (Style)FindResource("FlatButton"),
                Foreground = Brush(ReferenceEquals(node, model.Current) ? "TextBrush" : "SecondaryTextBrush"),
            };
            crumb.Click += (_, _) => model.Navigate(node);
            Crumbs.Children.Add(crumb);
        }
        Dispatcher.BeginInvoke(new Action(CrumbScroller.ScrollToRightEnd), DispatcherPriority.Loaded);
    }

    private void RenderSidebar()
    {
        SidebarRows.Children.Clear();
        sidebarRows.Clear();
        IReadOnlyList<FileNode> children = model.Current?.Children ?? Array.Empty<FileNode>();
        foreach (var node in children.Take(MaxSidebarRows))
        {
            var row = SidebarRow(node);
            sidebarRows.Add((node, row));
            SidebarRows.Children.Add(row);
        }
        if (children.Count > MaxSidebarRows)
        {
            SidebarRows.Children.Add(new TextBlock
            {
                Text = $"{children.Count - MaxSidebarRows:N0} smaller items not shown",
                Foreground = Brush("TertiaryTextBrush"),
                FontSize = 11.5,
                Margin = new Thickness(8, 6, 8, 6),
            });
        }
        var isEmpty = model.Current is not null && children.Count == 0;
        SidebarEmpty.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        SidebarEmpty.Text = model.Current?.IsUnreadable == true
            ? "This folder can’t be read without more access."
            : "This folder is empty.";
        SidebarScroller.ScrollToTop();
    }

    private Border SidebarRow(FileNode node)
    {
        var (r, g, b) = model.Layout.ColorForTopLevel(node).ToRgb();
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var swatch = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromRgb(r, g, b)),
        };
        var icon = Glyph(IconFor(node), 11);
        icon.Width = 14;
        icon.Margin = new Thickness(8, 0, 0, 0);
        var name = new TextBlock
        {
            Text = node.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 6, 0),
            Foreground = Brush(node.IsReal ? "TextBrush" : "SecondaryTextBrush"),
            ToolTip = node.Name.Length > 40 ? node.Name : null,
        };
        var add = new Button
        {
            Content = Glyph("\uE710", 11),
            Style = (Style)FindResource("FlatButton"),
            Padding = new Thickness(4, 2, 4, 2),
            ToolTip = "Add to collector",
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        add.Click += (_, _) => model.Collect(node);
        var size = new TextBlock
        {
            Text = ByteFormat.Format(node.Size),
            FontSize = 12,
            Foreground = Brush("SecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
        };
        UIElement[] cells = [swatch, icon, name, add, size];
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        var row = new Border
        {
            Height = 26,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 0, 8, 0),
            Background = Brushes.Transparent,
            Child = grid,
            Margin = new Thickness(0, 0, 0, 1),
        };
        row.MouseEnter += (_, _) =>
        {
            if (node.IsReal) add.Visibility = Visibility.Visible;
            model.Hovered = node;
        };
        row.MouseLeave += (_, _) =>
        {
            add.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(model.Hovered, node)) model.Hovered = null;
        };
        row.MouseLeftButtonDown += (_, e) => rowPress = e.GetPosition(row);
        row.MouseMove += (_, e) =>
        {
            if (rowPress is not { } start || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(row);
            if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            rowPress = null;
            if (node.IsReal) StartDrag(row, node);
        };
        row.MouseLeftButtonUp += (_, _) =>
        {
            if (rowPress is null) return;
            rowPress = null;
            Activate(node);
        };
        row.MouseRightButtonUp += (_, e) =>
        {
            ShowItemMenu(node, row);
            e.Handled = true;
        };
        return row;
    }

    private static string IconFor(FileNode node) => node.Kind switch
    {
        NodeKind.Directory => node.IsUnreadable ? "\uE72E" : "\uE8B7",
        NodeKind.File => "\uE8A5",
        NodeKind.Aggregate => "\uE8C8",
        _ => "\uED1A",
    };

    private void RenderHover()
    {
        Sunburst.SetHighlight(model.Hovered, model.Selected);
        var top = model.Hovered ?? model.Selected;
        while (top is not null && !ReferenceEquals(top.Parent, model.Current)) top = top.Parent;
        var rowBrush = Brush("RowBrush");
        foreach (var (node, row) in sidebarRows)
        {
            row.Background = ReferenceEquals(node, top) ? rowBrush : Brushes.Transparent;
        }
        RenderHeader(model.Hovered ?? model.Selected ?? model.Current);
    }

    private void RenderHeader(FileNode? node)
    {
        if (node is null)
        {
            HeaderTitle.Text = HeaderDetail.Text = HeaderGlyph.Text = "";
            HeaderImage.Source = null;
            return;
        }
        HeaderTitle.Text = node.DisplayName;
        var detail = ByteFormat.Format(node.Size);
        if (node.IsDirectory) detail += $" · {node.FileCount:N0} files";
        if (node.IsUnreadable) detail += " · no access";
        HeaderDetail.Text = detail;
        if (node.IsReal)
        {
            HeaderImage.Source = Native.ShellIcon(node.FullPath, node.IsDirectory);
            HeaderGlyph.Text = HeaderImage.Source is null ? IconFor(node) : "";
        }
        else
        {
            HeaderImage.Source = null;
            HeaderGlyph.Text = IconFor(node);
        }
    }

    private void Activate(FileNode node)
    {
        if (model.Activate(node)) ShowHiddenSpace();
    }

    private void ShowItemMenu(FileNode node, FrameworkElement target)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.MousePoint };
        if (node.IsReal)
        {
            if (node.IsDirectory && node.Children.Count > 0) menu.Items.Add(MenuItem("Open", () => model.Navigate(node)));
            menu.Items.Add(MenuItem("Show in Explorer", () => model.Reveal(node), "Ctrl+R"));
            menu.Items.Add(MenuItem("Copy Path", () => CopyPath(node)));
            menu.Items.Add(new Separator());
            menu.Items.Add(MenuItem("Add to Collector", () => model.Collect(node), "Del"));
        }
        else if (node.Kind == NodeKind.Hidden)
        {
            menu.Items.Add(MenuItem("What Is Hidden Space?", ShowHiddenSpace));
        }
        else
        {
            return;
        }
        menu.IsOpen = true;
    }

    private void CopyPath(FileNode node)
    {
        try
        {
            Clipboard.SetText(node.FullPath);
        }
        catch (ExternalException)
        {
            MessageBox.Show(this, "The clipboard is busy. Try again.", "DiskManager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BackToDisks_Click(object sender, RoutedEventArgs e) => model.CloseResult();

    private void More_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = MoreButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(MenuItem("Rescan", () => model.Rescan(asAdministrator: false)));
        menu.Items.Add(MenuItem("Rescan as Administrator…", () => model.Rescan(asAdministrator: true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Disk Cleanup…", OpenDiskCleanup));
        menu.Items.Add(MenuItem("System Protection…", OpenSystemProtection));
        menu.IsOpen = true;
    }

    private void ShowHiddenSpace()
    {
        var hidden = model.Result?.Root.Children.FirstOrDefault(c => c.Kind == NodeKind.Hidden);
        switch (Dialogs.ShowHiddenSpace(this, hidden?.Size, model.Result?.IsAdmin == true))
        {
            case HiddenSpaceAction.ScanAsAdministrator:
                model.Rescan(asAdministrator: true);
                break;
            case HiddenSpaceAction.SystemProtection:
                OpenSystemProtection();
                break;
            case HiddenSpaceAction.DiskCleanup:
                OpenDiskCleanup();
                break;
        }
    }

    private void OpenDiskCleanup()
    {
        var drive = model.Result?.Disk?.Path is { Length: >= 2 } path && path[1] == ':' ? path[..2] : null;
        Launch("cleanmgr.exe", drive is null ? "" : "/d " + drive);
    }

    private void OpenSystemProtection() => Launch("SystemPropertiesProtection.exe", "");

    private void Launch(string program, string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(program, arguments) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception e)
        {
            MessageBox.Show(this, $"{program} couldn’t be opened. {e.Message}", "DiskManager", MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // Collector

    private void RenderCollector()
    {
        var count = model.Collected.Count;
        var empty = count == 0;
        CollectorHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        CollectorSummary.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ShowCollectedButton.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        CollectorIcon.Foreground = Brush(empty ? "SecondaryTextBrush" : "AccentBrush");
        CollectorOutline.StrokeDashArray = empty ? new DoubleCollection { 5, 4 } : new DoubleCollection();
        CollectorSize.Text = ByteFormat.Format(model.CollectedSize);
        CollectorCount.Text = $"{count} item{(count == 1 ? "" : "s")} collected";
        DeleteButton.Visibility = !empty && !model.IsDeleting ? Visibility.Visible : Visibility.Collapsed;
        DeletingText.Visibility = model.IsDeleting ? Visibility.Visible : Visibility.Collapsed;
        if (collectedPopup is { IsOpen: true })
        {
            if (empty || model.IsDeleting) collectedPopup.IsOpen = false;
            else collectedPopup.Child = CollectedList();
        }
    }

    private void ShowCollected_Click(object sender, RoutedEventArgs e)
    {
        collectedPopup = new Popup
        {
            PlacementTarget = ShowCollectedButton,
            Placement = PlacementMode.Top,
            StaysOpen = false,
            AllowsTransparency = true,
            Child = CollectedList(),
        };
        collectedPopup.IsOpen = true;
    }

    private Border CollectedList()
    {
        var list = new StackPanel { Margin = new Thickness(12) };
        foreach (var node in model.Collected)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var back = new Button
            {
                Content = Glyph("\uE7A7", 13),
                Style = (Style)FindResource("FlatButton"),
                ToolTip = "Put back",
                VerticalAlignment = VerticalAlignment.Center,
            };
            back.Click += (_, _) => model.Uncollect(node);
            DockPanel.SetDock(back, Dock.Right);
            var size = new TextBlock
            {
                Text = ByteFormat.Format(node.Size),
                FontSize = 11.5,
                Foreground = Brush("SecondaryTextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
            };
            DockPanel.SetDock(size, Dock.Right);
            var icon = Glyph(node.IsDirectory ? "\uE8B7" : "\uE8A5", 13);
            icon.Margin = new Thickness(0, 0, 8, 0);
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = node.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            texts.Children.Add(new TextBlock
            {
                Text = node.FullPath,
                FontSize = 10.5,
                Foreground = Brush("SecondaryTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = node.FullPath,
            });
            row.Children.Add(back);
            row.Children.Add(size);
            row.Children.Add(icon);
            row.Children.Add(texts);
            list.Children.Add(row);
        }
        var border = new Border
        {
            Width = 380,
            MaxHeight = 320,
            Background = Brush("PanelBrush"),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };
        // A popup isn't inside the window, so it doesn't inherit the window's text color and font.
        TextElement.SetForeground(border, Brush("TextBrush"));
        TextElement.SetFontFamily(border, FontFamily);
        TextElement.SetFontSize(border, 12.5);
        return border;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (model.Collected.Count == 0 || model.IsDeleting) return;
        switch (Dialogs.ConfirmDelete(this, model.Collected.Count, model.CollectedSize))
        {
            case DeleteChoice.RecycleBin:
                model.DeleteCollected(permanently: false);
                break;
            case DeleteChoice.Permanently:
                model.DeleteCollected(permanently: true);
                break;
        }
    }

    private void StartDrag(DependencyObject source, FileNode node)
    {
        model.Dragged = node;
        try
        {
            // A private format only, so dropping on File Explorer can't move the real file.
            DragDrop.DoDragDrop(source, new DataObject(NodeFormat, node.FullPath), DragDropEffects.Move);
        }
        finally
        {
            model.Dragged = null;
            HighlightCollector(false);
        }
    }

    private void Collector_DragOver(object sender, DragEventArgs e)
    {
        var isNode = e.Data.GetDataPresent(NodeFormat) && model.Dragged is not null;
        e.Effects = isNode ? DragDropEffects.Move : DragDropEffects.None;
        HighlightCollector(isNode);
        e.Handled = true;
    }

    private void Collector_Drop(object sender, DragEventArgs e)
    {
        HighlightCollector(false);
        if (e.Data.GetDataPresent(NodeFormat) && model.Dragged is { } node) model.Collect(node);
        e.Handled = true;
    }

    private void HighlightCollector(bool on)
    {
        CollectorBackground.Background = on
            ? new SolidColorBrush(Color.FromArgb(0x2E, 0x3D, 0x9B, 0xF2))
            : Brush("PanelBrush");
        CollectorOutline.Stroke = on ? Brush("AccentBrush") : new SolidColorBrush(Color.FromArgb(0x22, 255, 255, 255));
    }

    // Dropping a folder on the drive list scans it.

    private static string? DroppedFolder(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths && Directory.Exists(paths[0]) ? paths[0] : null;

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Handled) return;
        var canDrop = model.Result is null && model.Scan is null && DroppedFolder(e) is not null;
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        DropHighlight.Visibility = canDrop ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropHighlight.Visibility = Visibility.Collapsed;
        if (e.Handled || model.Result is not null) return;
        if (DroppedFolder(e) is { } folder) model.StartScan(folder);
        e.Handled = true;
    }

    // Keyboard

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var target = model.Target;
        switch (key)
        {
            case Key.O when modifiers == ModifierKeys.Control:
                ChooseFolder();
                break;
            case Key.R when modifiers == ModifierKeys.Control && target is { IsReal: true }:
                model.Reveal(target);
                break;
            case Key.Delete when model.Result is not null && target is { IsReal: true }:
                model.Collect(target);
                break;
            case Key.Up when modifiers == ModifierKeys.Alt:
            case Key.Back when modifiers == ModifierKeys.None && model.Result is not null:
                model.GoUp();
                break;
            case Key.Left when modifiers == ModifierKeys.Alt:
                model.CloseResult();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Helpers

    private static TextBlock Glyph(string glyph, double size, string brush = "SecondaryTextBrush") => new()
    {
        Text = glyph,
        FontFamily = (FontFamily)Application.Current.FindResource("IconFont"),
        FontSize = size,
        Foreground = Brush(brush),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static MenuItem MenuItem(string header, Action onClick, string? gesture = null)
    {
        var item = new MenuItem { Header = header, InputGestureText = gesture ?? "" };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Progress bar, status line and Cancel button for a running scan.</summary>
internal sealed class ScanProgressView
{
    private readonly ProgressBar bar = new() { Maximum = 1, Margin = new Thickness(0, 9, 0, 0) };
    private readonly TextBlock status = new()
    {
        FontSize = 11.5,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public ScanProgressView(Action cancel)
    {
        status.Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush");
        var button = new Button { Content = "Cancel", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), FontSize = 11.5 };
        button.Click += (_, _) => cancel();
        var line = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(button, Dock.Right);
        line.Children.Add(button);
        line.Children.Add(status);
        Root = new StackPanel();
        Root.Children.Add(bar);
        Root.Children.Add(line);
    }

    public StackPanel Root { get; }

    public void Update(ScanState? scan)
    {
        if (scan is null) return;
        if (scan.Fraction is { } fraction)
        {
            bar.IsIndeterminate = false;
            bar.Value = fraction;
        }
        else
        {
            bar.IsIndeterminate = true;
        }
        status.Text = scan.IsAdmin
            ? "Scanning as administrator…"
            : $"{ByteFormat.Format(scan.Progress.Bytes)} · {scan.Progress.Files:N0} files · {scan.Progress.Path}";
    }
}
