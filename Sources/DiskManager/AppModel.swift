import AppKit
import DiskCore
import Observation
import SwiftUI

struct ScanState {
    let id = UUID()
    let path: String
    let isAdmin: Bool
    let expectedBytes: Int64?
    var progress = DiskScanner.Progress()

    var fraction: Double? {
        guard let expectedBytes, expectedBytes > 0, !isAdmin else { return nil }
        return min(1, Double(progress.bytes) / Double(expectedBytes))
    }
}

struct ScanResult {
    let root: FileNode
    let disk: DiskInfo?
    let isAdmin: Bool
}

@MainActor
@Observable
final class AppModel {
    private(set) var disks: [DiskInfo] = []
    private(set) var scan: ScanState?
    private(set) var result: ScanResult?
    private(set) var current: FileNode?
    private(set) var layout = SunburstLayout()
    private(set) var layoutID = 0
    private(set) var collected: [FileNode] = []
    private(set) var isDeleting = false
    var hovered: FileNode?
    var selected: FileNode?
    var dragged: FileNode?
    var errorMessage: String?
    var previewURL: URL?
    var showSnapshots = false
    var showHiddenInfo = false

    @ObservationIgnored private var scanner: DiskScanner?
    @ObservationIgnored private var timer: Timer?

    init() {
        refreshDisks()
        let center = NSWorkspace.shared.notificationCenter
        for name in [NSWorkspace.didMountNotification, NSWorkspace.didUnmountNotification,
                     NSWorkspace.didRenameVolumeNotification] {
            center.addObserver(forName: name, object: nil, queue: .main) { [weak self] _ in
                MainActor.assumeIsolated { self?.refreshDisks() }
            }
        }
        timer = Timer.scheduledTimer(withTimeInterval: 0.15, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
    }

    var target: FileNode? { hovered ?? selected }
    var canGoUp: Bool { current.map { $0 !== result?.root } ?? false }
    var collectedSize: Int64 { collected.reduce(0) { $0 + $1.size } }

    // MARK: Disks

    func refreshDisks() {
        let updated = Volumes.mounted()
        if updated != disks { disks = updated }
    }

    private var ticks = 0
    private func tick() {
        ticks += 1
        if let scanner, scan != nil { scan?.progress = scanner.currentProgress }
        if ticks % 14 == 0, result == nil { refreshDisks() }
    }

    func chooseFolder() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.prompt = "Scan"
        if panel.runModal() == .OK, let url = panel.url { startScan(path: url.path) }
    }

    // MARK: Scanning

    func startScan(path: String, asAdministrator: Bool = false) {
        guard scan == nil else { return }
        closeResult()
        let disk = disks.first { $0.path == path }
        let state = ScanState(path: path, isAdmin: asAdministrator, expectedBytes: disk?.used)
        scan = state
        let id = state.id

        if asAdministrator {
            guard let executable = Bundle.main.executablePath else { return }
            Task.detached(priority: .userInitiated) {
                let outcome = Result { try Self.privilegedScan(path: path, executable: executable) }
                await self.finishScan(id: id, outcome: outcome, disk: disk, isAdmin: true)
            }
        } else {
            let scanner = DiskScanner(path: path)
            self.scanner = scanner
            Task.detached(priority: .userInitiated) {
                let root = scanner.run()
                await self.finishScan(id: id, outcome: .success(root), disk: disk, isAdmin: false)
            }
        }
    }

    nonisolated private static func privilegedScan(path: String, executable: String) throws -> FileNode? {
        let output = FileManager.default.temporaryDirectory.appendingPathComponent("scan-\(UUID().uuidString).tree")
        guard FileManager.default.createFile(atPath: output.path, contents: nil,
                                             attributes: [.posixPermissions: 0o600]) else { return nil }
        defer { try? FileManager.default.removeItem(at: output) }
        do {
            try Shell.runAsAdministrator(executable, ["--helper-scan", path, output.path],
                                         prompt: "DiskManager wants to scan “\(path)” as administrator.")
        } catch let failure as Shell.Failure where failure.wasCancelled {
            return nil
        }
        return try TreeCoding.decode(Data(contentsOf: output, options: .mappedIfSafe))
    }

    private func finishScan(id: UUID, outcome: Result<FileNode?, Error>, disk: DiskInfo?, isAdmin: Bool) {
        guard scan?.id == id else { return }
        let path = scan?.path ?? ""
        scan = nil
        scanner = nil
        switch outcome {
        case .failure(let error):
            errorMessage = "The scan failed. \(error.localizedDescription)"
        case .success(nil):
            if !isAdmin, !FileManager.default.isReadableFile(atPath: path) {
                errorMessage = "“\(path)” could not be read."
            }
        case .success(let root?):
            let fresh = disk.flatMap { Volumes.info(forPath: $0.path) }
            if let fresh {
                let hidden = fresh.used - root.size
                if hidden > 100_000_000 {
                    root.append(FileNode(name: "hidden space", kind: .hidden, size: hidden))
                }
            }
            result = ScanResult(root: root, disk: fresh, isAdmin: isAdmin)
            navigate(to: root)
        }
    }

    func cancelScan() {
        scanner?.cancel()
        scanner = nil
        scan = nil
    }

    func closeResult() {
        collected.forEach { $0.reattach() }
        collected = []
        result = nil
        current = nil
        hovered = nil
        selected = nil
        layout = SunburstLayout()
        refreshDisks()
    }

    func rescan(asAdministrator: Bool) {
        guard let path = result?.root.path else { return }
        showHiddenInfo = false
        startScan(path: path, asAdministrator: asAdministrator)
    }

    // MARK: Navigation

    func navigate(to node: FileNode) {
        guard node.isDirectory else { return }
        current = node
        hovered = nil
        selected = nil
        rebuildLayout()
    }

    func goUp() {
        guard canGoUp, let parent = current?.parent else { return }
        navigate(to: parent)
    }

    func activate(_ node: FileNode) {
        switch node.kind {
        case .directory where !node.children.isEmpty: navigate(to: node)
        case .hidden: showHiddenInfo = true
        default: selected = node
        }
    }

    private func rebuildLayout() {
        layout = current.map { SunburstLayout(root: $0) } ?? SunburstLayout()
        layoutID += 1
    }

    // MARK: Items

    func preview(_ node: FileNode) {
        guard node.isReal else { return }
        previewURL = URL(fileURLWithPath: node.path)
    }

    func reveal(_ node: FileNode) {
        guard node.isReal else { return }
        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: node.path)])
    }

    func copyPath(_ node: FileNode) {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(node.path, forType: .string)
    }

    func collect(_ node: FileNode) {
        guard node.isReal, node.parent != nil, let current, node !== current, node.isDescendant(of: current),
              !isDeleting else { return }
        if let reason = Protection.reason(forPath: node.path) {
            errorMessage = "“\(node.name)” can’t be collected. \(reason)"
            return
        }
        node.detach()
        collected.append(node)
        if hovered?.isDescendant(of: node) == true { hovered = nil }
        if selected?.isDescendant(of: node) == true { selected = nil }
        rebuildLayout()
    }

    func uncollect(_ node: FileNode) {
        guard !isDeleting, let index = collected.firstIndex(where: { $0 === node }) else { return }
        collected.remove(at: index)
        node.reattach()
        rebuildLayout()
    }

    func deleteCollected(permanently: Bool) {
        guard !collected.isEmpty, !isDeleting else { return }
        let items = collected
        let paths = items.map(\.path)
        isDeleting = true
        Task.detached(priority: .userInitiated) {
            var failures: [Int: String] = [:]
            for (index, path) in paths.enumerated() {
                let url = URL(fileURLWithPath: path)
                do {
                    if permanently {
                        try FileManager.default.removeItem(at: url)
                    } else {
                        try FileManager.default.trashItem(at: url, resultingItemURL: nil)
                    }
                } catch {
                    failures[index] = error.localizedDescription
                }
            }
            await self.finishDeleting(items: items, failures: failures)
        }
    }

    private func finishDeleting(items: [FileNode], failures: [Int: String]) {
        isDeleting = false
        collected = []
        for index in failures.keys.sorted() { items[index].reattach() }
        if !failures.isEmpty {
            let details = failures.keys.sorted().prefix(5).map { "• \(items[$0].name): \(failures[$0]!)" }
            errorMessage = "\(failures.count) of \(items.count) items could not be deleted and were put back.\n"
                + details.joined(separator: "\n")
        }
        rebuildLayout()
    }
}
