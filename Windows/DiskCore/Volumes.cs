using System.Security;

namespace DiskCore;

public enum DiskKind
{
    Fixed,
    Removable,
    Network,
    Optical,
    Other,
}

public sealed record DiskInfo(string Path, string Name, long Total, long Available, long Free, string Format, DiskKind Kind)
{
    public long Used => Math.Max(0, Total - Free);
    public double UsedFraction => Total > 0 ? Math.Clamp((double)Used / Total, 0, 1) : 0;
}

public static class Volumes
{
    /// <summary>Lists the drives that are ready. Can block on a sleeping network drive, so call it off the UI thread.</summary>
    public static IReadOnlyList<DiskInfo> Mounted()
    {
        var disks = new List<DiskInfo>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (Info(drive) is { } info) disks.Add(info);
        }
        return disks;
    }

    public static DiskInfo? Info(string path)
    {
        var root = System.IO.Path.GetPathRoot(path);
        return string.IsNullOrEmpty(root) ? null : Info(new DriveInfo(root));
    }

    private static DiskInfo? Info(DriveInfo drive)
    {
        try
        {
            if (drive.DriveType is DriveType.NoRootDirectory or DriveType.Unknown || !drive.IsReady) return null;
            var total = drive.TotalSize;
            if (total <= 0) return null;
            var kind = drive.DriveType switch
            {
                DriveType.Fixed => DiskKind.Fixed,
                DriveType.Removable => DiskKind.Removable,
                DriveType.Network => DiskKind.Network,
                DriveType.CDRom => DiskKind.Optical,
                _ => DiskKind.Other,
            };
            return new DiskInfo(drive.RootDirectory.FullName, DisplayName(drive.Name, drive.VolumeLabel, kind), total,
                drive.AvailableFreeSpace, drive.TotalFreeSpace, drive.DriveFormat, kind);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    /// <summary>Names a drive the way File Explorer does, e.g. "Local Disk (C:)".</summary>
    public static string DisplayName(string root, string? label, DiskKind kind)
    {
        var letter = root.TrimEnd('\\', '/');
        var name = !string.IsNullOrWhiteSpace(label) ? label : kind switch
        {
            DiskKind.Fixed => "Local Disk",
            DiskKind.Removable => "USB Drive",
            DiskKind.Network => "Network Drive",
            DiskKind.Optical => "CD Drive",
            _ => "Drive",
        };
        return letter.Length == 2 && letter[1] == ':' ? $"{name} ({letter.ToUpperInvariant()})" : name;
    }
}
