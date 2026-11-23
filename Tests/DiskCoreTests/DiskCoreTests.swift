import XCTest
@testable import DiskCore

final class DiskCoreTests: XCTestCase {
    private var root: URL!

    override func setUpWithError() throws {
        root = FileManager.default.temporaryDirectory.appendingPathComponent("DiskCoreTests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: root.appendingPathComponent("a/b"), withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: root.appendingPathComponent("many"), withIntermediateDirectories: true)
        try Data(count: 300_000).write(to: root.appendingPathComponent("a/big.bin"))
        try Data(count: 50_000).write(to: root.appendingPathComponent("a/b/mid.bin"))
        try Data(count: 10).write(to: root.appendingPathComponent("small.txt"))
        for i in 0..<100 {
            try Data(count: 1000 + i).write(to: root.appendingPathComponent("many/f\(i)"))
        }
        try FileManager.default.linkItem(at: root.appendingPathComponent("a/big.bin"),
                                         to: root.appendingPathComponent("hardlink.bin"))
    }

    override func tearDownWithError() throws {
        try FileManager.default.removeItem(at: root)
    }

    private func duKilobytes() throws -> Int64 {
        let out = try Shell.run("/usr/bin/du", ["-sk", root.path])
        return Int64(String(decoding: out, as: UTF8.self).split(separator: "\t")[0])!
    }

    func testScanMatchesDu() throws {
        let tree = try XCTUnwrap(DiskScanner(path: root.path).run())
        XCTAssertEqual(tree.fileCount, 104)
        let du = try duKilobytes() * 1024
        XCTAssertLessThanOrEqual(abs(tree.size - du), 64 * 1024, "scanned \(tree.size), du \(du)")

        let a = try XCTUnwrap(tree.children.first { $0.name == "a" })
        XCTAssertEqual(tree.children.map(\.size), tree.children.map(\.size).sorted(by: >))
        let links = [tree.children.first { $0.name == "hardlink.bin" }!.size,
                     a.children.first { $0.name == "big.bin" }!.size]
        XCTAssertEqual(links.filter { $0 == 0 }.count, 1, "hard link must be counted once")

        let many = try XCTUnwrap(tree.children.first { $0.name == "many" })
        XCTAssertEqual(many.children.count, 49)
        XCTAssertEqual(many.children.filter { $0.kind == .aggregate }.first?.fileCount, 52)
        XCTAssertEqual(many.fileCount, 100)

        let mid = try XCTUnwrap(a.children.first { $0.name == "b" }?.children.first)
        XCTAssertEqual(mid.path, DiskScanner(path: root.path).rootPath + "/a/b/mid.bin")
    }

    func testDetachAndReattach() throws {
        let tree = try XCTUnwrap(DiskScanner(path: root.path).run())
        let total = tree.size, count = tree.fileCount
        let a = try XCTUnwrap(tree.children.first { $0.name == "a" })
        let b = try XCTUnwrap(a.children.first { $0.name == "b" })
        b.detach()
        XCTAssertEqual(tree.size, total - b.size)
        XCTAssertEqual(tree.fileCount, count - 1)
        XCTAssertFalse(a.children.contains { $0 === b })
        b.reattach()
        XCTAssertEqual(tree.size, total)
        XCTAssertEqual(tree.fileCount, count)
        XCTAssertTrue(b.isDescendant(of: tree))
    }

    func testCodingRoundTrip() throws {
        let tree = try XCTUnwrap(DiskScanner(path: root.path).run())
        let copy = try TreeCoding.decode(TreeCoding.encode(tree))
        func flatten(_ node: FileNode) -> [String] {
            ["\(node.path)|\(node.size)|\(node.fileCount)|\(node.kind)"] + node.children.flatMap(flatten)
        }
        XCTAssertEqual(flatten(copy), flatten(tree))
        XCTAssertThrowsError(try TreeCoding.decode(TreeCoding.encode(tree).dropLast(3)))
    }

    func testCancelledScanReturnsNil() {
        let scanner = DiskScanner(path: root.path)
        scanner.cancel()
        XCTAssertNil(scanner.run())
    }

    func testProtection() {
        let home = "/Users/me"
        for path in ["/", "/System/Library/CoreServices", "/usr/bin/swift", "/Users", "/Users/me",
                     "/Users/me/Library", "/System/Volumes/Data/Users/me/Documents", "/private/var/db/dyld"] {
            XCTAssertNotNil(Protection.reason(forPath: path, home: home), path)
        }
        for path in ["/Users/me/Downloads/movie.mp4", "/usr/local/Cellar", "/Users/me/Library/Caches",
                     "/Applications/Foo.app", "/System/Volumes/Data/Users/me/Movies/a.mov"] {
            XCTAssertNil(Protection.reason(forPath: path, home: home), path)
        }
    }

    func testSnapshotParsing() throws {
        let plist: [String: Any] = ["Snapshots": [
            ["SnapshotName": "com.apple.TimeMachine.2026-11-20-101500.local", "SnapshotUUID": "A", "Purgeable": true],
            ["SnapshotName": "com.apple.os.update-ABC", "SnapshotUUID": "B", "Purgeable": false],
        ]]
        let data = try PropertyListSerialization.data(fromPropertyList: plist, format: .xml, options: 0)
        let snapshots = Snapshots.parse(data, volume: "/")
        XCTAssertEqual(snapshots.map(\.uuid), ["A", "B"])
        XCTAssertEqual(snapshots.map(\.isPurgeable), [true, false])
        XCTAssertNotNil(snapshots[0].date)
        XCTAssertNil(snapshots[1].date)
    }

    func testShellQuoting() throws {
        XCTAssertEqual(Shell.quote("it's"), "'it'\\''s'")
        let out = try Shell.run("/bin/sh", ["-c", "printf %s " + Shell.quote("a 'b' $c")])
        XCTAssertEqual(String(decoding: out, as: UTF8.self), "a 'b' $c")
    }
}
