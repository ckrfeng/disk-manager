import Darwin
import Foundation

public final class DiskScanner {
    public struct Progress: Sendable {
        public var bytes: Int64 = 0
        public var files = 0
        public var path = ""
        public init() {}
    }

    private struct LinkKey: Hashable {
        let dev: Int32
        let ino: UInt64
    }

    private enum Attr {
        static let returnedAttrs: UInt32 = 0x8000_0000
        static let name: UInt32 = 0x1
        static let devID: UInt32 = 0x2
        static let objType: UInt32 = 0x8
        static let fileID: UInt32 = 0x0200_0000
        static let error: UInt32 = 0x2000_0000
        static let fileLinkCount: UInt32 = 0x1
        static let fileAllocSize: UInt32 = 0x4
    }

    private static let dataVolume = "/System/Volumes/Data"
    private static let maxFilesPerDirectory = 64
    private static let keptFilesPerDirectory = 48
    private static let bufferSize = 256 * 1024

    public let rootPath: String
    private let cond = NSCondition()
    private var queue: [(FileNode, String)] = []
    private var active = 0
    private var cancelled = false
    private var progress = Progress()
    private let linkLock = NSLock()
    private var seenLinks = Set<LinkKey>()
    private var allowedDevs = Set<dev_t>()

    public init(path: String) {
        var resolved = [CChar](repeating: 0, count: Int(PATH_MAX))
        rootPath = realpath(path, &resolved) != nil ? String(cString: resolved) : path
    }

    public func cancel() {
        cond.lock()
        cancelled = true
        cond.broadcast()
        cond.unlock()
    }

    public var currentProgress: Progress {
        cond.lock()
        defer { cond.unlock() }
        return progress
    }

    public func run() -> FileNode? {
        var st = stat()
        guard stat(rootPath, &st) == 0, (st.st_mode & S_IFMT) == S_IFDIR else { return nil }
        allowedDevs = [st.st_dev]
        if rootPath == "/", let volumes = try? FileManager.default.contentsOfDirectory(atPath: "/System/Volumes") {
            for volume in volumes where stat("/System/Volumes/" + volume, &st) == 0 {
                allowedDevs.insert(st.st_dev)
            }
        }

        let root = FileNode(name: rootPath, kind: .directory)
        queue = [(root, rootPath)]
        let threads = max(2, min(8, ProcessInfo.processInfo.activeProcessorCount))
        DispatchQueue.concurrentPerform(iterations: threads) { _ in work() }
        guard !cancelled else { return nil }
        root.finalize()
        return root
    }

    private func work() {
        let buffer = UnsafeMutableRawPointer.allocate(byteCount: Self.bufferSize, alignment: 8)
        defer { buffer.deallocate() }
        cond.lock()
        while true {
            while queue.isEmpty && active > 0 && !cancelled { cond.wait() }
            guard !cancelled, let (node, path) = queue.popLast() else { break }
            active += 1
            cond.unlock()

            var bytes: Int64 = 0
            var files = 0
            let subdirectories = scan(node, path: path, buffer: buffer, bytes: &bytes, files: &files)

            cond.lock()
            queue.append(contentsOf: subdirectories)
            active -= 1
            progress.bytes += bytes
            progress.files += files
            progress.path = path
            cond.broadcast()
        }
        cond.broadcast()
        cond.unlock()
    }

    private func scan(_ node: FileNode, path: String, buffer: UnsafeMutableRawPointer,
                      bytes: inout Int64, files: inout Int) -> [(FileNode, String)] {
        let fd = open(path, O_RDONLY | O_DIRECTORY | O_NOFOLLOW)
        guard fd >= 0 else {
            node.isUnreadable = true
            return []
        }
        defer { close(fd) }
        var st = stat()
        guard fstat(fd, &st) == 0, allowedDevs.contains(st.st_dev) else {
            node.isExcluded = true
            return []
        }

        var attrs = attrlist()
        attrs.bitmapcount = UInt16(ATTR_BIT_MAP_COUNT)
        attrs.commonattr = Attr.returnedAttrs | Attr.name | Attr.devID | Attr.objType | Attr.fileID | Attr.error
        attrs.fileattr = Attr.fileLinkCount | Attr.fileAllocSize

        let filterFirmlinks = rootPath == "/" && path == Self.dataVolume
        let prefix = path.hasSuffix("/") ? path : path + "/"
        var directories: [(FileNode, String)] = []
        var fileNodes: [FileNode] = []

        while true {
            let count = getattrlistbulk(fd, &attrs, buffer, Self.bufferSize, 0)
            guard count > 0 else { break }
            var entry = UnsafeRawPointer(buffer)
            for _ in 0..<count {
                let length = Int(entry.loadUnaligned(as: UInt32.self))
                defer { entry += length }
                var field = entry + 4
                let common = field.loadUnaligned(as: UInt32.self)
                let fileAttrs = (field + 12).loadUnaligned(as: UInt32.self)
                field += 20
                if common & Attr.error != 0 { field += 4 }
                guard common & Attr.name != 0 else { continue }
                let nameOffset = Int(field.loadUnaligned(as: Int32.self))
                let name = String(cString: (field + nameOffset).assumingMemoryBound(to: CChar.self))
                field += 8
                var dev: Int32 = 0
                if common & Attr.devID != 0 {
                    dev = field.loadUnaligned(as: Int32.self)
                    field += 4
                }
                var type: UInt32 = 0
                if common & Attr.objType != 0 {
                    type = field.loadUnaligned(as: UInt32.self)
                    field += 4
                }
                var fileID: UInt64 = 0
                if common & Attr.fileID != 0 {
                    fileID = field.loadUnaligned(as: UInt64.self)
                    field += 8
                }

                if type == VDIR.rawValue {
                    if filterFirmlinks && lstat("/" + name, &st) == 0 { continue }
                    directories.append((FileNode(name: name, kind: .directory), prefix + name))
                    continue
                }

                var linkCount: UInt32 = 1
                if fileAttrs & Attr.fileLinkCount != 0 {
                    linkCount = field.loadUnaligned(as: UInt32.self)
                    field += 4
                }
                var size: Int64 = 0
                if fileAttrs & Attr.fileAllocSize != 0 {
                    size = field.loadUnaligned(as: Int64.self)
                }
                if linkCount > 1 {
                    linkLock.lock()
                    let isNew = seenLinks.insert(LinkKey(dev: dev, ino: fileID)).inserted
                    linkLock.unlock()
                    if !isNew { size = 0 }
                }
                bytes += size
                files += 1
                fileNodes.append(FileNode(name: name, kind: .file, size: size, fileCount: 1))
            }
        }

        if fileNodes.count > Self.maxFilesPerDirectory {
            fileNodes.sort { $0.size > $1.size }
            let rest = fileNodes[Self.keptFilesPerDirectory...]
            let aggregate = FileNode(name: "\(rest.count.formatted()) smaller files", kind: .aggregate,
                                     size: rest.reduce(0) { $0 + $1.size }, fileCount: rest.count)
            fileNodes.removeSubrange(Self.keptFilesPerDirectory...)
            fileNodes.append(aggregate)
        }
        node.setChildren(directories.map(\.0) + fileNodes)
        return directories
    }
}
