using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DiskCore;

namespace DiskManager;

internal enum DeleteChoice
{
    Cancel,
    RecycleBin,
    Permanently,
}

internal enum HiddenSpaceAction
{
    None,
    ScanAsAdministrator,
    SystemProtection,
    DiskCleanup,
}

/// <summary>Small dark dialogs built in code: delete confirmation and the hidden-space explainer.</summary>
internal static class Dialogs
{
    public static DeleteChoice ConfirmDelete(Window owner, int count, long size)
    {
        var choice = DeleteChoice.Cancel;
        var window = Create(owner, "Delete collected items");
        var panel = new StackPanel { Margin = new Thickness(22), Width = 440 };
        panel.Children.Add(Heading($"Delete {count} collected item{(count == 1 ? "" : "s")} ({ByteFormat.Format(size)})?"));
        panel.Children.Add(Body("Items moved to the Recycle Bin free up space once you empty it. "
            + "Permanent deletion can’t be undone.", secondary: true));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };
        buttons.Children.Add(MakeButton("Cancel", null, window.Close, isCancel: true));
        buttons.Children.Add(MakeButton("Delete Permanently", "DangerButton", () =>
        {
            choice = DeleteChoice.Permanently;
            window.Close();
        }));
        buttons.Children.Add(MakeButton("Move to Recycle Bin", "AccentButton", () =>
        {
            choice = DeleteChoice.RecycleBin;
            window.Close();
        }, isDefault: true));
        panel.Children.Add(buttons);

        window.Content = panel;
        window.ShowDialog();
        return choice;
    }

    public static HiddenSpaceAction ShowHiddenSpace(Window owner, long? hiddenSize, bool scannedAsAdministrator)
    {
        var action = HiddenSpaceAction.None;
        var window = Create(owner, "Hidden space");
        var panel = new StackPanel { Margin = new Thickness(22), Width = 460 };
        panel.Children.Add(Heading("Hidden space"));
        if (hiddenSize is { } size)
        {
            panel.Children.Add(Body($"{ByteFormat.Format(size)} on this drive is in use but was not reachable by the scan."));
        }
        panel.Children.Add(Body("It’s usually made of folders the app has no permission to read, System Restore points "
            + "and shadow copies, and file system overhead such as the NTFS master file table.", secondary: true));

        var actions = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        void Add(string glyph, string title, string detail, HiddenSpaceAction result)
        {
            actions.Children.Add(ActionButton(glyph, title, detail, () =>
            {
                action = result;
                window.Close();
            }));
        }
        if (!scannedAsAdministrator)
        {
            Add("\uEA18", "Scan as Administrator…", "Reads folders that belong to other users and to Windows.",
                HiddenSpaceAction.ScanAsAdministrator);
        }
        Add("\uE81C", "System Protection…", "Restore points can hold a lot of space. Lower their limit or delete them there.",
            HiddenSpaceAction.SystemProtection);
        Add("\uE74D", "Disk Cleanup…", "Removes temporary files, update leftovers and previous Windows installations.",
            HiddenSpaceAction.DiskCleanup);
        panel.Children.Add(actions);

        var done = MakeButton("Done", null, window.Close, isDefault: true, isCancel: true);
        done.HorizontalAlignment = HorizontalAlignment.Right;
        done.Margin = new Thickness(0, 14, 0, 0);
        panel.Children.Add(done);

        window.Content = panel;
        window.ShowDialog();
        return action;
    }

    private static Window Create(Window owner, string title)
    {
        var window = new Window
        {
            Owner = owner,
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = Resource<Brush>("BackgroundBrush"),
            Foreground = Resource<Brush>("TextBrush"),
            FontFamily = owner.FontFamily,
            FontSize = 13,
            UseLayoutRounding = true,
        };
        window.SourceInitialized += (_, _) => Native.UseDarkTitleBar(window);
        return window;
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text,
        FontSize = 17,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
    };

    private static TextBlock Body(string text, bool secondary = false) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 10),
        Foreground = Resource<Brush>(secondary ? "SecondaryTextBrush" : "TextBrush"),
    };

    private static Button MakeButton(string text, string? style, Action onClick, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 80,
            IsDefault = isDefault,
            IsCancel = isCancel,
        };
        if (style is not null) button.Style = Resource<Style>(style);
        button.Click += (_, _) => onClick();
        return button;
    }

    private static Button ActionButton(string glyph, string title, string detail, Action onClick)
    {
        var icon = new TextBlock
        {
            Text = glyph,
            FontFamily = Resource<FontFamily>("IconFont"),
            FontSize = 17,
            Width = 24,
            Foreground = Resource<Brush>("AccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        var texts = new StackPanel();
        texts.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Medium });
        texts.Children.Add(new TextBlock
        {
            Text = detail,
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Resource<Brush>("SecondaryTextBrush"),
            Margin = new Thickness(0, 2, 0, 0),
        });
        var content = new DockPanel();
        content.Children.Add(icon);
        content.Children.Add(texts);

        var button = new Button
        {
            Content = content,
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Resource<Brush>("RowBrush"),
            BorderBrush = Brushes.Transparent,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static T Resource<T>(string key) => (T)Application.Current.FindResource(key);
}
