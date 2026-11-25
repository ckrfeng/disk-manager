using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DiskManager;

/// <summary>Windows shell calls: Recycle Bin, file icons, dark title bars.</summary>
internal static class Native
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;
    private const ushort FofWantNukeWarning = 0x4000;

    private const uint ShgfiIcon = 0x0100;
    private const uint ShgfiUseFileAttributes = 0x0010;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;

    private const int DwmUseImmersiveDarkMode = 20;

    private static readonly Dictionary<string, ImageSource?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Moves a file or folder to the Recycle Bin. If it is too big for the Recycle Bin,
    /// Windows asks before deleting it for good. Call on an STA thread.
    /// </summary>
    public static void MoveToRecycleBin(string path)
    {
        var operation = new ShFileOpStruct
        {
            wFunc = FoDelete,
            pFrom = path + "\0", // The list must end with two NULs; the marshaller adds the second.
            fFlags = (ushort)(FofAllowUndo | FofNoConfirmation | FofNoErrorUi | FofSilent | FofWantNukeWarning),
        };
        var result = SHFileOperation(ref operation);
        if (operation.fAnyOperationsAborted) throw new OperationCanceledException("The deletion was cancelled.");
        if (result != 0) throw new IOException($"Windows couldn’t move it to the Recycle Bin (error 0x{result:X}).");
    }

    public static void DeletePermanently(string path)
    {
        try
        {
            Delete(path);
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only files block deletion; clear the flag and try once more.
            ClearReadOnly(path);
            Delete(path);
        }

        static void Delete(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else File.Delete(path);
        }
    }

    private static void ClearReadOnly(string path)
    {
        if (!Directory.Exists(path))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            return;
        }
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = true };
        foreach (var entry in new DirectoryInfo(path).EnumerateFileSystemInfos("*", options))
        {
            if (entry.Attributes.HasFlag(FileAttributes.ReadOnly)) entry.Attributes &= ~FileAttributes.ReadOnly;
        }
    }

    /// <summary>Runs work on a new STA thread, which shell file operations need.</summary>
    public static Task<T> RunOnStaThread<T>(Func<T> work)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(work());
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }) { IsBackground = true, Name = "DiskManager delete" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>The generic shell icon for a file type or folder. It doesn't touch the disk.</summary>
    public static ImageSource? ShellIcon(string path, bool isDirectory)
    {
        var key = isDirectory ? "<folder>" : System.IO.Path.GetExtension(path);
        if (key.Equals(".exe", StringComparison.OrdinalIgnoreCase) || key.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || key.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            key += "|" + path; // These carry their own icons.
        }
        if (IconCache.TryGetValue(key, out var cached)) return cached;

        var info = new ShFileInfo();
        var attributes = isDirectory ? FileAttributeDirectory : FileAttributeNormal;
        ImageSource? image = null;
        if (SHGetFileInfo(path, attributes, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiUseFileAttributes)
            != IntPtr.Zero && info.hIcon != IntPtr.Zero)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                image = source;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }
        IconCache[key] = image;
        return image;
    }

    public static void UseDarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct operation);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
