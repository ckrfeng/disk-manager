import Foundation

public final class FileNode {
    public enum Kind: UInt8 {
        case file, directory, aggregate, hidden
    }

    public let name: String
    public let kind: Kind
    public internal(set) var size: Int64
    public internal(set) var fileCount: Int
    public internal(set) var children: [FileNode] = []
    public internal(set) var isUnreadable = false
    public private(set) unowned(unsafe) var parent: FileNode?
    var isExcluded = false

    public init(name: String, kind: Kind, size: Int64 = 0, fileCount: Int = 0) {
        self.name = name
        self.kind = kind
        self.size = size
        self.fileCount = fileCount
    }

    public var isDirectory: Bool { kind == .directory }
    public var isReal: Bool { kind == .file || kind == .directory }

    public var path: String {
        guard let parent else { return name }
        let base = parent.path
        return base.hasSuffix("/") ? base + name : base + "/" + name
    }

    public var displayName: String {
        guard parent == nil else { return name }
        return name == "/" ? "/" : (name as NSString).lastPathComponent
    }

    public func isDescendant(of other: FileNode) -> Bool {
        var node: FileNode? = self
        while let n = node {
            if n === other { return true }
            node = n.parent
        }
        return false
    }

    func setChildren(_ nodes: [FileNode]) {
        children = nodes
        for node in nodes { node.parent = self }
    }

    @discardableResult
    func finalize() -> Int64 {
        guard kind == .directory else { return size }
        children.removeAll { $0.isExcluded }
        var total: Int64 = 0
        var count = 0
        for child in children {
            total += child.finalize()
            count += child.fileCount
        }
        size = total
        fileCount = count
        children.sort { $0.size > $1.size }
        return total
    }

    public func append(_ child: FileNode) {
        child.parent = self
        children.append(child)
        children.sort { $0.size > $1.size }
        adjust(size: child.size, fileCount: child.fileCount)
    }

    public func detach() {
        guard let parent, let index = parent.children.firstIndex(where: { $0 === self }) else { return }
        parent.children.remove(at: index)
        parent.adjust(size: -size, fileCount: -fileCount)
    }

    public func reattach() {
        guard let parent, !parent.children.contains(where: { $0 === self }) else { return }
        let index = parent.children.firstIndex { $0.size < size } ?? parent.children.count
        parent.children.insert(self, at: index)
        parent.adjust(size: size, fileCount: fileCount)
    }

    private func adjust(size delta: Int64, fileCount countDelta: Int) {
        var node: FileNode? = self
        while let n = node {
            n.size += delta
            n.fileCount += countDelta
            if let p = n.parent { p.children.sort { $0.size > $1.size } }
            node = n.parent
        }
    }
}
