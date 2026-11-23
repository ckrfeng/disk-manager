import AppKit
import DiskCore
import SwiftUI
import UniformTypeIdentifiers

struct DiskListView: View {
    @Environment(AppModel.self) private var model
    @State private var isTargeted = false

    var body: some View {
        VStack(spacing: 0) {
            VStack(spacing: 4) {
                Image(nsImage: NSApp.applicationIconImage)
                    .resizable()
                    .frame(width: 84, height: 84)
                Text("DiskManager").font(.system(size: 26, weight: .bold))
                Text("Find out what’s taking up your disk space.").foregroundStyle(.secondary)
            }
            .padding(.top, 28)
            .padding(.bottom, 24)

            ScrollView {
                VStack(spacing: 10) {
                    ForEach(model.disks) { disk in
                        DiskRow(disk: disk)
                    }
                    if let scan = model.scan, !model.disks.contains(where: { $0.path == scan.path }) {
                        FolderScanRow(scan: scan)
                    }
                }
                .frame(maxWidth: 680)
                .padding(.horizontal, 24)
                .frame(maxWidth: .infinity)
            }

            HStack(spacing: 12) {
                Button { model.chooseFolder() } label: { Label("Scan Folder…", systemImage: "folder") }
                    .disabled(model.scan != nil)
                Button { model.showSnapshots = true } label: {
                    Label("Snapshots…", systemImage: "clock.arrow.circlepath")
                }
                Text("or drop a folder anywhere in this window").font(.caption).foregroundStyle(.tertiary)
            }
            .padding(.vertical, 18)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .overlay {
            if isTargeted {
                RoundedRectangle(cornerRadius: 14).strokeBorder(Theme.accent, lineWidth: 3).padding(8)
            }
        }
        .onDrop(of: [.fileURL], isTargeted: $isTargeted) { providers in
            guard let provider = providers.first else { return false }
            _ = provider.loadObject(ofClass: URL.self) { url, _ in
                guard let url, (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true else {
                    return
                }
                DispatchQueue.main.async { model.startScan(path: url.path) }
            }
            return true
        }
    }
}

struct ScanProgress: View {
    @Environment(AppModel.self) private var model
    let scan: ScanState

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            if let fraction = scan.fraction {
                ProgressView(value: fraction).tint(Theme.accent)
            } else {
                ProgressView().progressViewStyle(.linear).tint(Theme.accent)
            }
            HStack {
                Text(status)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
                    .truncationMode(.middle)
                Spacer()
                Button("Cancel") { model.cancelScan() }.controlSize(.small)
            }
        }
    }

    private var status: String {
        if scan.isAdmin { return "Scanning as administrator…" }
        return "\(formatBytes(scan.progress.bytes)) · \(scan.progress.files.formatted()) files · \(scan.progress.path)"
    }
}

struct DiskRow: View {
    @Environment(AppModel.self) private var model
    let disk: DiskInfo

    private var barColor: Color {
        switch disk.usedFraction {
        case ..<0.75: Color(hue: 0.38, saturation: 0.65, brightness: 0.78)
        case ..<0.9: Color(hue: 0.13, saturation: 0.8, brightness: 0.95)
        default: Color(hue: 0.0, saturation: 0.7, brightness: 0.92)
        }
    }

    var body: some View {
        let scan = model.scan.flatMap { $0.path == disk.path ? $0 : nil }
        HStack(spacing: 14) {
            Image(nsImage: NSWorkspace.shared.icon(forFile: disk.path))
                .resizable()
                .frame(width: 52, height: 52)
            VStack(alignment: .leading, spacing: 6) {
                HStack(alignment: .firstTextBaseline) {
                    Text(disk.name).font(.system(size: 15, weight: .semibold)).lineLimit(1)
                    Text(disk.format).font(.caption).foregroundStyle(.tertiary).lineLimit(1)
                    Spacer()
                    Text("\(formatBytes(disk.available)) free of \(formatBytes(disk.total))")
                        .font(.system(size: 12).monospacedDigit())
                        .foregroundStyle(.secondary)
                }
                if let scan {
                    ScanProgress(scan: scan)
                } else {
                    GeometryReader { proxy in
                        ZStack(alignment: .leading) {
                            Capsule().fill(.white.opacity(0.1))
                            Capsule().fill(barColor)
                                .frame(width: max(6, proxy.size.width * disk.usedFraction))
                        }
                    }
                    .frame(height: 6)
                }
            }
            if scan == nil {
                Menu {
                    Button("Scan as Administrator…") { model.startScan(path: disk.path, asAdministrator: true) }
                } label: {
                    Text("Scan")
                } primaryAction: {
                    model.startScan(path: disk.path)
                }
                .fixedSize()
                .disabled(model.scan != nil)
            }
        }
        .padding(14)
        .background(Theme.panel, in: RoundedRectangle(cornerRadius: 12))
        .animation(.easeOut(duration: 0.3), value: disk.usedFraction)
    }
}

struct FolderScanRow: View {
    let scan: ScanState

    var body: some View {
        HStack(spacing: 14) {
            Image(nsImage: NSWorkspace.shared.icon(forFile: scan.path))
                .resizable()
                .frame(width: 52, height: 52)
            VStack(alignment: .leading, spacing: 6) {
                Text((scan.path as NSString).lastPathComponent).font(.system(size: 15, weight: .semibold))
                ScanProgress(scan: scan)
            }
        }
        .padding(14)
        .background(Theme.panel, in: RoundedRectangle(cornerRadius: 12))
    }
}
