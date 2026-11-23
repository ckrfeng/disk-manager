import DiskCore
import SwiftUI

struct SnapshotsView: View {
    @Environment(AppModel.self) private var model
    @Environment(\.dismiss) private var dismiss
    @State private var snapshots: [Snapshot]?
    @State private var pending: Snapshot?
    @State private var busy: String?
    @State private var failure: String?

    private var volume: String { model.result?.disk?.path ?? "/" }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Label("Snapshots", systemImage: "clock.arrow.circlepath").font(.title2.weight(.semibold))
            Text("""
            Local APFS snapshots keep older versions of the disk and can quietly hold on to space \
            from files you already deleted. macOS removes them on its own; purge one to get the space back now.
            """)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)

            Group {
                if let snapshots {
                    if snapshots.isEmpty {
                        Text("No snapshots on this disk.").foregroundStyle(.secondary)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    } else {
                        ScrollView {
                            VStack(spacing: 6) { ForEach(snapshots, content: row) }
                        }
                    }
                } else {
                    ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
            .frame(height: 240)

            if let failure {
                Text(failure).font(.caption).foregroundStyle(.red).lineLimit(3)
            }
            HStack {
                Button("Refresh") { load() }
                Spacer()
                Button("Done") { dismiss() }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(22)
        .frame(width: 560)
        .task { load() }
        .confirmationDialog("Purge this snapshot?", isPresented: Binding(get: { pending != nil },
                                                                         set: { if !$0 { pending = nil } }),
                            presenting: pending) { snapshot in
            Button("Purge Snapshot", role: .destructive) { purge(snapshot) }
            Button("Cancel", role: .cancel) {}
        } message: { snapshot in
            Text("“\(snapshot.name)” will be deleted. Backups that rely on it can no longer restore from it.")
        }
    }

    private func row(_ snapshot: Snapshot) -> some View {
        HStack(spacing: 10) {
            VStack(alignment: .leading, spacing: 2) {
                Text(snapshot.name).lineLimit(1).truncationMode(.middle)
                Text([snapshot.date?.formatted(date: .abbreviated, time: .shortened), snapshot.volume]
                    .compactMap { $0 }.joined(separator: " · "))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Spacer(minLength: 8)
            if busy == snapshot.uuid {
                ProgressView().controlSize(.small)
            } else if snapshot.isPurgeable {
                Button("Purge…") { pending = snapshot }.disabled(busy != nil)
            } else {
                Text("In use by macOS").font(.caption).foregroundStyle(.tertiary)
            }
        }
        .padding(10)
        .background(Theme.row, in: RoundedRectangle(cornerRadius: 8))
    }

    private func load() {
        let volume = volume
        Task.detached {
            let list = Snapshots.list(volume: volume)
            await MainActor.run { snapshots = list }
        }
    }

    private func purge(_ snapshot: Snapshot) {
        busy = snapshot.uuid
        failure = nil
        Task.detached {
            let result = Result { try Snapshots.delete(snapshot) }
            await MainActor.run {
                busy = nil
                if case .failure(let error) = result, (error as? Shell.Failure)?.wasCancelled != true {
                    failure = error.localizedDescription
                }
                model.refreshDisks()
                load()
            }
        }
    }
}
