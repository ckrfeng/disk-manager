import Foundation

public enum TreeCoding {
    public enum DecodeError: Error { case truncated, invalid }

    private static let magic: UInt32 = 0x444D_5431

    public static func encode(_ root: FileNode) -> Data {
        var data = Data()
        append(magic, to: &data)
        encode(root, into: &data)
        return data
    }

    public static func decode(_ data: Data) throws -> FileNode {
        var offset = data.startIndex
        guard try read(UInt32.self, from: data, at: &offset) == magic else { throw DecodeError.invalid }
        return try decodeNode(from: data, at: &offset)
    }

    private static func encode(_ node: FileNode, into data: inout Data) {
        let name = Array(node.name.utf8.prefix(Int(UInt16.max)))
        data.append(node.kind.rawValue | (node.isUnreadable ? 0x80 : 0))
        append(node.size, to: &data)
        append(Int64(node.fileCount), to: &data)
        append(UInt16(name.count), to: &data)
        data.append(contentsOf: name)
        append(UInt32(node.children.count), to: &data)
        for child in node.children { encode(child, into: &data) }
    }

    private static func decodeNode(from data: Data, at offset: inout Int) throws -> FileNode {
        let flags = try read(UInt8.self, from: data, at: &offset)
        guard let kind = FileNode.Kind(rawValue: flags & 0x7F) else { throw DecodeError.invalid }
        let size = try read(Int64.self, from: data, at: &offset)
        let fileCount = try read(Int64.self, from: data, at: &offset)
        let nameLength = Int(try read(UInt16.self, from: data, at: &offset))
        guard offset + nameLength <= data.endIndex else { throw DecodeError.truncated }
        let name = String(decoding: data[offset..<offset + nameLength], as: UTF8.self)
        offset += nameLength
        let childCount = Int(try read(UInt32.self, from: data, at: &offset))
        let node = FileNode(name: name, kind: kind, size: size, fileCount: Int(fileCount))
        node.isUnreadable = flags & 0x80 != 0
        var children: [FileNode] = []
        children.reserveCapacity(min(childCount, 1 << 16))
        for _ in 0..<childCount { children.append(try decodeNode(from: data, at: &offset)) }
        node.setChildren(children)
        return node
    }

    private static func append<T: FixedWidthInteger>(_ value: T, to data: inout Data) {
        withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
    }

    private static func read<T: FixedWidthInteger>(_ type: T.Type, from data: Data, at offset: inout Int) throws -> T {
        let size = MemoryLayout<T>.size
        guard offset + size <= data.endIndex else { throw DecodeError.truncated }
        let value = data[offset..<offset + size].withUnsafeBytes { $0.loadUnaligned(as: T.self) }
        offset += size
        return T(littleEndian: value)
    }
}
