import Foundation

public enum Protection {
    private static let exact: Set<String> = [
        "/", "/Users", "/Users/Shared", "/Applications", "/Library", "/System", "/private", "/private/var",
        "/private/tmp", "/private/etc", "/usr", "/usr/local", "/bin", "/sbin", "/opt", "/etc", "/var", "/tmp",
        "/Volumes", "/cores",
    ]
    private static let prefixes = [
        "/System/", "/bin/", "/sbin/", "/Library/Apple/", "/private/var/db/", "/private/etc/",
    ]
    private static let homeFolders = [
        "", "/Library", "/Desktop", "/Documents", "/Downloads", "/Movies", "/Music", "/Pictures", "/Public",
        "/Applications", "/Library/Application Support", "/Library/Preferences", "/Library/Containers",
        "/Library/Mobile Documents", "/Library/Keychains",
    ]

    public static func reason(forPath rawPath: String, home: String = NSHomeDirectory()) -> String? {
        var path = rawPath
        if path.hasPrefix("/System/Volumes/Data/") { path.removeFirst("/System/Volumes/Data".count) }
        if exact.contains(path) || prefixes.contains(where: path.hasPrefix)
            || (path.hasPrefix("/usr/") && !path.hasPrefix("/usr/local/")) {
            return "This item is part of macOS and is protected."
        }
        if homeFolders.contains(where: { path == home + $0 }) {
            return "This is a standard folder of your home directory. Open it and pick the items inside instead."
        }
        let bundle = Bundle.main.bundlePath
        if bundle.hasSuffix(".app"), path == bundle || bundle.hasPrefix(path + "/") || path.hasPrefix(bundle + "/") {
            return "This item contains the running app."
        }
        return nil
    }
}
