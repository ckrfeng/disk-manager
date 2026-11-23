import AppKit
import DiskCore
import SwiftUI

struct ScanResultView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        @Bindable var model = model
        VStack(spacing: 0) {
            PathBar()
            HStack(spacing: 0) {
                VStack(spacing: 0) {
                    SunburstView().padding(16)
                    CollectorBar()
                }
                Sidebar()
                    .frame(width: 320)
                    .background(Theme.panel)
            }
        }
        .sheet(isPresented: $model.showHiddenInfo) { HiddenSpaceView() }
    }
}

struct PathBar: View {
    @Environment(AppModel.self) private var model

    private var trail: [FileNode] {
        var nodes: [FileNode] = []
        var node = model.current
        while let n = node {
            nodes.append(n)
            node = n.parent
        }
        return nodes.reversed()
    }

    var body: some View {
        HStack(spacing: 6) {
            Button { model.closeResult() } label: { Label("Disks", systemImage: "chevron.left") }
                .buttonStyle(.bordered)
                .help("Back to the list of disks")
            ScrollViewReader { scroll in
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 2) {
                        ForEach(Array(trail.enumerated()), id: \.offset) { index, node in
                            if index > 0 {
                                Image(systemName: "chevron.right").font(.system(size: 9)).foregroundStyle(.tertiary)
                            }
                            Button(index == 0 ? (model.result?.disk?.name ?? node.displayName) : node.name) {
                                model.navigate(to: node)
                            }
                            .buttonStyle(.plain)
                            .padding(.horizontal, 6)
                            .padding(.vertical, 3)
                            .foregroundStyle(node === model.current ? .primary : .secondary)
                            .id(index)
                        }
                    }
                }
                .onChange(of: model.layoutID) { scroll.scrollTo(trail.count - 1, anchor: .trailing) }
            }
            Spacer(minLength: 8)
            if model.result?.isAdmin == true {
                Label("Administrator", systemImage: "lock.open.fill")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Menu {
                Button("Rescan") { model.rescan(asAdministrator: false) }
                Button("Rescan as Administrator…") { model.rescan(asAdministrator: true) }
                Divider()
                Button("Manage Snapshots…") { model.showSnapshots = true }
            } label: {
                Image(systemName: "ellipsis.circle")
            }
            .menuStyle(.borderlessButton)
            .fixedSize()
        }
        .padding(.leading, 84)
        .padding(.trailing, 14)
        .frame(height: 44)
    }
}

struct Sidebar: View {
    @Environment(AppModel.self) private var model

    private var highlightedTop: FileNode? {
        var node = model.hovered ?? model.selected
        while let n = node, n.parent !== model.current { node = n.parent }
        return node
    }

    var body: some View {
        let top = highlightedTop
        VStack(spacing: 0) {
            if let node = model.hovered ?? model.selected ?? model.current {
                ItemHeader(node: node)
            }
            Divider()
            ScrollView {
                LazyVStack(spacing: 1) {
                    ForEach(model.current?.children ?? [], id: \.id) { node in
                        SidebarRow(node: node, isHighlighted: node === top)
                    }
                }
                .padding(8)
            }
            .id(model.layoutID)
            .overlay {
                if model.current?.children.isEmpty == true {
                    Text(model.current?.isUnreadable == true ? "This folder can’t be read without more access."
                         : "This folder is empty.")
                        .foregroundStyle(.secondary)
                        .padding()
                }
            }
        }
    }
}

extension FileNode: Identifiable {
    public var id: ObjectIdentifier { ObjectIdentifier(self) }
}

struct ItemHeader: View {
    let node: FileNode

    private var symbol: String {
        switch node.kind {
        case .hidden: "eye.slash"
        case .aggregate: "doc.on.doc"
        default: ""
        }
    }

    var body: some View {
        HStack(spacing: 10) {
            Group {
                if node.isReal {
                    Image(nsImage: NSWorkspace.shared.icon(forFile: node.path)).resizable()
                } else {
                    Image(systemName: symbol).font(.system(size: 22)).foregroundStyle(.secondary)
                }
            }
            .frame(width: 36, height: 36)
            VStack(alignment: .leading, spacing: 2) {
                Text(node.displayName)
                    .font(.system(size: 13, weight: .semibold))
                    .lineLimit(1)
                    .truncationMode(.middle)
                Text(detail)
                    .font(.system(size: 11).monospacedDigit())
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
        }
        .padding(.horizontal, 14)
        .frame(height: 62)
    }

    private var detail: String {
        var text = formatBytes(node.size)
        if node.isDirectory { text += " · \(node.fileCount.formatted()) files" }
        if node.isUnreadable { text += " · no access" }
        return text
    }
}

struct SidebarRow: View {
    @Environment(AppModel.self) private var model
    let node: FileNode
    let isHighlighted: Bool
    @State private var isHovering = false

    var body: some View {
        HStack(spacing: 8) {
            RoundedRectangle(cornerRadius: 3)
                .fill(model.layout.color(forTopLevel: node))
                .frame(width: 10, height: 10)
            Image(systemName: icon)
                .font(.system(size: 11))
                .foregroundStyle(.secondary)
                .frame(width: 14)
            Text(node.name)
                .lineLimit(1)
                .truncationMode(.middle)
                .foregroundStyle(node.isReal ? .primary : .secondary)
            Spacer(minLength: 6)
            if isHovering, node.isReal {
                Button { model.collect(node) } label: { Image(systemName: "plus.circle.fill") }
                    .buttonStyle(.plain)
                    .foregroundStyle(.secondary)
                    .help("Add to collector")
            }
            Text(formatBytes(node.size))
                .font(.system(size: 12).monospacedDigit())
                .foregroundStyle(.secondary)
        }
        .font(.system(size: 12.5))
        .padding(.horizontal, 8)
        .frame(height: 26)
        .background(isHighlighted ? Theme.row : .clear, in: RoundedRectangle(cornerRadius: 6))
        .contentShape(Rectangle())
        .onHover { inside in
            isHovering = inside
            if inside {
                model.hovered = node
            } else if model.hovered === node {
                model.hovered = nil
            }
        }
        .onTapGesture { model.activate(node) }
        .onDrag {
            guard node.isReal else { return NSItemProvider() }
            model.dragged = node
            return NSItemProvider(object: node.path as NSString)
        }
        .contextMenu { ItemMenu(node: node) }
    }

    private var icon: String {
        switch node.kind {
        case .directory: node.isUnreadable ? "lock.fill" : "folder.fill"
        case .file: "doc.fill"
        case .aggregate: "doc.on.doc.fill"
        case .hidden: "eye.slash.fill"
        }
    }
}

struct CollectorBar: View {
    @Environment(AppModel.self) private var model
    @State private var isTargeted = false
    @State private var showItems = false
    @State private var confirmDelete = false

    var body: some View {
        HStack(spacing: 12) {
            Image(systemName: model.collected.isEmpty ? "tray" : "tray.full.fill")
                .font(.system(size: 20))
                .foregroundStyle(model.collected.isEmpty ? Color.secondary : Theme.accent)
            if model.collected.isEmpty {
                Text("Drag files and folders here to collect them for deletion.")
                    .foregroundStyle(.secondary)
                Spacer()
            } else {
                VStack(alignment: .leading, spacing: 1) {
                    Text(formatBytes(model.collectedSize)).font(.system(size: 14, weight: .semibold).monospacedDigit())
                    Text("\(model.collected.count) item\(model.collected.count == 1 ? "" : "s") collected")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Button("Show") { showItems = true }
                    .popover(isPresented: $showItems, arrowEdge: .top) { collectedList }
                Spacer()
                if model.isDeleting {
                    ProgressView().controlSize(.small)
                    Text("Deleting…").foregroundStyle(.secondary)
                } else {
                    Button("Delete…") { confirmDelete = true }
                        .buttonStyle(.borderedProminent)
                        .tint(.red)
                }
            }
        }
        .padding(.horizontal, 16)
        .frame(height: 56)
        .background(isTargeted ? Theme.accent.opacity(0.18) : Theme.panel, in: RoundedRectangle(cornerRadius: 12))
        .overlay {
            RoundedRectangle(cornerRadius: 12)
                .strokeBorder(isTargeted ? Theme.accent : .white.opacity(0.08),
                              style: StrokeStyle(lineWidth: 1, dash: model.collected.isEmpty ? [5, 4] : []))
        }
        .padding([.horizontal, .bottom], 16)
        .onDrop(of: [.plainText], isTargeted: $isTargeted) { _ in
            guard let node = model.dragged else { return false }
            model.dragged = nil
            model.collect(node)
            return true
        }
        .confirmationDialog("Delete \(model.collected.count) collected item\(model.collected.count == 1 ? "" : "s") "
            + "(\(formatBytes(model.collectedSize)))?", isPresented: $confirmDelete) {
            Button("Move to Trash") { model.deleteCollected(permanently: false) }
            Button("Delete Permanently", role: .destructive) { model.deleteCollected(permanently: true) }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Items moved to the Trash free up space once you empty it. Permanent deletion can’t be undone.")
        }
    }

    private var collectedList: some View {
        ScrollView {
            VStack(spacing: 2) {
                ForEach(model.collected, id: \.id) { node in
                    HStack(spacing: 8) {
                        Image(systemName: node.isDirectory ? "folder.fill" : "doc.fill").foregroundStyle(.secondary)
                        VStack(alignment: .leading, spacing: 0) {
                            Text(node.name).lineLimit(1).truncationMode(.middle)
                            Text(node.path).font(.caption2).foregroundStyle(.secondary).lineLimit(1)
                                .truncationMode(.head)
                        }
                        Spacer(minLength: 8)
                        Text(formatBytes(node.size)).font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                        Button { model.uncollect(node) } label: { Image(systemName: "arrow.uturn.backward.circle") }
                            .buttonStyle(.plain)
                            .help("Put back")
                    }
                    .padding(.vertical, 4)
                }
            }
            .padding(12)
        }
        .frame(width: 380)
        .frame(maxHeight: 320)
    }
}

struct HiddenSpaceView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        let hidden = model.result?.root.children.first { $0.kind == .hidden }
        VStack(alignment: .leading, spacing: 14) {
            Label("Hidden space", systemImage: "eye.slash.fill").font(.title2.weight(.semibold))
            if let hidden {
                Text("\(formatBytes(hidden.size)) on this disk is in use but was not reachable by the scan.")
            }
            Text("""
            It is usually made of files in folders the app has no permission to read, local snapshots, \
            purgeable space macOS reclaims on its own, and file system overhead.
            """)
            .foregroundStyle(.secondary)
            if let purgeable = model.result?.disk?.purgeable, purgeable > 0 {
                Text("About \(formatBytes(purgeable)) of it is purgeable.").foregroundStyle(.secondary)
            }
            VStack(alignment: .leading, spacing: 8) {
                action("Scan as Administrator…", "lock.open", "Reads folders owned by other users and the system.") {
                    dismiss()
                    model.rescan(asAdministrator: true)
                }
                action("Grant Full Disk Access…", "hand.raised", "Lets scans read Mail, Messages, Safari and "
                    + "other privacy-protected data. Add DiskManager in System Settings, then rescan.") {
                    NSWorkspace.shared.open(URL(string:
                        "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles")!)
                }
                action("Manage Snapshots…", "clock.arrow.circlepath", "Lists local APFS snapshots you can purge.") {
                    dismiss()
                    model.showSnapshots = true
                }
            }
            HStack {
                Spacer()
                Button("Done") { dismiss() }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(22)
        .frame(width: 460)
    }

    private func action(_ title: String, _ symbol: String, _ detail: String,
                        perform: @escaping () -> Void) -> some View {
        Button(action: perform) {
            HStack(spacing: 12) {
                Image(systemName: symbol).font(.system(size: 17)).frame(width: 24).foregroundStyle(Theme.accent)
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).fontWeight(.medium)
                    Text(detail).font(.caption).foregroundStyle(.secondary).multilineTextAlignment(.leading)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer(minLength: 0)
            }
            .padding(10)
            .background(Theme.row, in: RoundedRectangle(cornerRadius: 8))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}
