import QuickLook
import SwiftUI

struct DiskManagerApp: App {
    @State private var model = AppModel()

    var body: some Scene {
        Window("DiskManager", id: "main") {
            RootView()
                .environment(model)
                .preferredColorScheme(.dark)
                .frame(minWidth: 940, minHeight: 640)
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1100, height: 720)
        .commands {
            CommandGroup(replacing: .newItem) {
                Button("Scan Folder…") { model.chooseFolder() }
                    .keyboardShortcut("o")
                    .disabled(model.scan != nil)
            }
            CommandMenu("Item") {
                Button("Quick Look") { model.target.map(model.preview) }
                    .keyboardShortcut(.space, modifiers: [])
                    .disabled(model.target?.isReal != true)
                Button("Show in Finder") { model.target.map(model.reveal) }
                    .keyboardShortcut("r")
                    .disabled(model.target?.isReal != true)
                Button("Add to Collector") { model.target.map(model.collect) }
                    .keyboardShortcut(.delete)
                    .disabled(model.target?.isReal != true)
                Divider()
                Button("Enclosing Folder") { model.goUp() }
                    .keyboardShortcut(.upArrow)
                    .disabled(!model.canGoUp)
                Button("Back to Disks") { model.closeResult() }
                    .keyboardShortcut("[")
                    .disabled(model.result == nil)
            }
        }
    }
}

struct RootView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        @Bindable var model = model
        ZStack {
            Theme.background.ignoresSafeArea()
            if model.result != nil {
                ScanResultView().transition(.opacity)
            } else {
                DiskListView().transition(.opacity)
            }
        }
        .animation(.easeOut(duration: 0.2), value: model.result != nil)
        .alert("DiskManager", isPresented: Binding(get: { model.errorMessage != nil },
                                                    set: { if !$0 { model.errorMessage = nil } })) {
            Button("OK", role: .cancel) {}
        } message: {
            Text(model.errorMessage ?? "")
        }
        .sheet(isPresented: $model.showSnapshots) { SnapshotsView() }
        .quickLookPreview($model.previewURL)
    }
}

enum Theme {
    static let background = Color(red: 0.105, green: 0.11, blue: 0.13)
    static let panel = Color(red: 0.14, green: 0.15, blue: 0.175)
    static let row = Color.white.opacity(0.07)
    static let accent = Color(hue: 0.58, saturation: 0.75, brightness: 0.95)
}

func formatBytes(_ bytes: Int64) -> String {
    ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
}
