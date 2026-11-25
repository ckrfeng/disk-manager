using System.Globalization;
using System.Text;

namespace DiskCore;

public static class ByteFormat
{
    private static readonly string[] Units = ["KB", "MB", "GB", "TB", "PB", "EB"];

    /// <summary>Formats a size the way File Explorer does: 1024-based units, about three significant digits.</summary>
    public static string Format(long bytes)
    {
        if (bytes < 0) return "-" + Format(bytes == long.MinValue ? long.MaxValue : -bytes);
        if (bytes < 1024) return bytes == 1 ? "1 byte" : $"{bytes} bytes";
        double value = bytes;
        var unit = -1;
        do
        {
            value /= 1024;
            unit++;
        } while (value >= 999.5 && unit < Units.Length - 1);
        var format = value < 9.995 ? "0.00" : value < 99.95 ? "0.0" : "0";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }
}

public static class CommandLine
{
    /// <summary>Quotes one argument for a Windows command line (CommandLineToArgvW rules).</summary>
    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0) return argument;
        var builder = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }
            if (c == '"')
            {
                builder.Append('\\', backslashes * 2 + 1).Append('"');
            }
            else
            {
                builder.Append('\\', backslashes).Append(c);
            }
            backslashes = 0;
        }
        return builder.Append('\\', backslashes * 2).Append('"').ToString();
    }

    public static string Join(params IEnumerable<string> arguments) => string.Join(' ', arguments.Select(Quote));
}
