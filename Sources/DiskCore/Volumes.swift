import Foundation

public struct DiskInfo: Identifiable, Equatable, Sendable {
    public var id: String { path }
    public let path: String
    public let name: String
    public let total: Int64
    public let available: Int64
    public let free: Int64
    public let format: String
    public let isInternal: Bool

    public var used: Int64 { max(0, total - free) }
    public var purgeable: Int64 { max(0, available - free) }
    public var usedFraction: Double { total > 0 ? Double(total - available) / Double(total) : 0 }
}

public enum Volumes {
    private static let keys: Set<URLResourceKey> = [
        .volumeNameKey, .volumeTotalCapacityKey, .volumeAvailableCapacityKey,
        .volumeAvailableCapacityForImportantUsageKey, .volumeLocalizedFormatDescriptionKey, .volumeIsInternalKey,
    ]

    public static func mounted() -> [DiskInfo] {
        let urls = FileManager.default.mountedVolumeURLs(includingResourceValuesForKeys: Array(keys),
                                                         options: [.skipHiddenVolumes]) ?? []
        return urls.compactMap { info(for: $0) }
    }

    public static func info(forPath path: String) -> DiskInfo? {
        info(for: URL(fileURLWithPath: path))
    }

    private static func info(for url: URL) -> DiskInfo? {
        var url = url
        url.removeAllCachedResourceValues()
        guard let values = try? url.resourceValues(forKeys: keys), let total = values.volumeTotalCapacity, total > 0
        else { return nil }
        let free = Int64(values.volumeAvailableCapacity ?? 0)
        let important = values.volumeAvailableCapacityForImportantUsage ?? 0
        return DiskInfo(path: url.path, name: values.volumeName ?? url.lastPathComponent, total: Int64(total),
                        available: max(important, free), free: free,
                        format: values.volumeLocalizedFormatDescription ?? "",
                        isInternal: values.volumeIsInternal ?? false)
    }
}
