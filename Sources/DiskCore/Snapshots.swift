import Foundation

public struct Snapshot: Identifiable, Equatable, Sendable {
    public var id: String { uuid }
    public let name: String
    public let uuid: String
    public let volume: String
    public let isPurgeable: Bool

    public var date: Date? {
        guard let match = name.firstMatch(of: #/\d{4}-\d{2}-\d{2}-\d{6}/#) else { return nil }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd-HHmmss"
        return formatter.date(from: String(match.output))
    }
}

public enum Snapshots {
    public static func list(volume: String) -> [Snapshot] {
        let volumes = volume == "/" ? ["/System/Volumes/Data", "/"] : [volume]
        return volumes.flatMap { mount -> [Snapshot] in
            guard let data = try? Shell.run("/usr/sbin/diskutil", ["apfs", "listSnapshots", "-plist", mount]) else {
                return []
            }
            return parse(data, volume: mount)
        }
    }

    static func parse(_ data: Data, volume: String) -> [Snapshot] {
        guard let plist = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any],
              let items = plist["Snapshots"] as? [[String: Any]] else { return [] }
        return items.compactMap { item in
            guard let name = item["SnapshotName"] as? String, let uuid = item["SnapshotUUID"] as? String else {
                return nil
            }
            return Snapshot(name: name, uuid: uuid, volume: volume, isPurgeable: item["Purgeable"] as? Bool ?? true)
        }
    }

    public static func delete(_ snapshot: Snapshot) throws {
        let arguments = ["apfs", "deleteSnapshot", snapshot.volume, "-uuid", snapshot.uuid]
        do {
            try Shell.run("/usr/sbin/diskutil", arguments)
        } catch {
            try Shell.runAsAdministrator("/usr/sbin/diskutil", arguments,
                                         prompt: "DiskManager needs administrator access to purge a snapshot.")
        }
    }
}
