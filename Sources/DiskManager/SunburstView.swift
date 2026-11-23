import DiskCore
import SwiftUI

struct SunburstGeometry {
    let center: CGPoint
    let inner: CGFloat
    let ring: CGFloat

    init(size: CGSize) {
        let radius = max(40, min(size.width, size.height) / 2 - 8)
        center = CGPoint(x: size.width / 2, y: size.height / 2)
        inner = radius * 0.26
        ring = (radius - inner) / CGFloat(SunburstLayout.maxDepth)
    }

    enum Hit { case center, ring(depth: Int, fraction: Double), outside }

    func hit(_ point: CGPoint) -> Hit {
        let dx = point.x - center.x, dy = point.y - center.y
        let distance = hypot(dx, dy)
        if distance < inner { return .center }
        let depth = Int((distance - inner) / ring)
        guard depth < SunburstLayout.maxDepth else { return .outside }
        var fraction = (atan2(dy, dx) + .pi / 2) / (2 * .pi)
        if fraction < 0 { fraction += 1 }
        return .ring(depth: depth, fraction: fraction)
    }
}

struct SunburstCanvas: View, Animatable {
    var progress: Double
    let layout: SunburstLayout
    let highlighted: FileNode?
    let selected: FileNode?

    var animatableData: Double {
        get { progress }
        set { progress = newValue }
    }

    var body: some View {
        Canvas { context, size in
            let geometry = SunburstGeometry(size: size)
            let scale = 0.86 + 0.14 * progress
            for segment in layout.segments {
                let isHighlighted = highlighted.map { h in segment.node?.isDescendant(of: h) ?? false } ?? false
                let isSelected = selected != nil && segment.node === selected
                let dimmed = highlighted != nil && !isHighlighted
                let r0 = (geometry.inner + CGFloat(segment.depth) * geometry.ring) * scale
                let r1 = r0 + (geometry.ring - 1.5) * scale
                let a0 = Angle.radians(segment.start * progress * 2 * .pi - .pi / 2)
                let a1 = Angle.radians(segment.end * progress * 2 * .pi - .pi / 2)
                var path = Path()
                path.addArc(center: geometry.center, radius: r1, startAngle: a0, endAngle: a1, clockwise: false)
                path.addArc(center: geometry.center, radius: r0, startAngle: a1, endAngle: a0, clockwise: true)
                path.closeSubpath()
                context.fill(path, with: .color(segment.hsb.color(boost: isHighlighted || isSelected ? 1 : 0)
                    .opacity((dimmed ? 0.5 : 1) * progress)))
                context.stroke(path, with: .color(isSelected ? .white : Theme.background),
                               lineWidth: isSelected ? 1.5 : 1)
            }
        }
    }
}

struct SunburstView: View {
    @Environment(AppModel.self) private var model
    @State private var progress = 1.0

    var body: some View {
        GeometryReader { proxy in
            let geometry = SunburstGeometry(size: proxy.size)
            ZStack {
                SunburstCanvas(progress: progress, layout: model.layout, highlighted: model.hovered,
                               selected: model.selected)
                center(geometry)
            }
            .contentShape(Rectangle())
            .onContinuousHover { phase in
                switch phase {
                case .active(let point): hover(node(at: point, geometry))
                case .ended: hover(nil)
                }
            }
            .onTapGesture { point in
                switch geometry.hit(point) {
                case .center: model.goUp()
                case .ring: node(at: point, geometry).map(model.activate)
                case .outside: model.selected = nil
                }
            }
            .onDrag {
                guard let node = model.hovered, node.isReal else { return NSItemProvider() }
                model.dragged = node
                return NSItemProvider(object: node.path as NSString)
            }
            .contextMenu {
                if let node = model.hovered { ItemMenu(node: node) }
            }
        }
        .onChange(of: model.layoutID) {
            var transaction = Transaction()
            transaction.disablesAnimations = true
            withTransaction(transaction) { progress = 0 }
            DispatchQueue.main.async {
                withAnimation(.easeOut(duration: 0.45)) { progress = 1 }
            }
        }
    }

    private func node(at point: CGPoint, _ geometry: SunburstGeometry) -> FileNode? {
        guard case .ring(let depth, let fraction) = geometry.hit(point) else { return nil }
        return model.layout.segment(depth: depth, fraction: fraction)?.node
    }

    private func hover(_ node: FileNode?) {
        if model.hovered !== node { model.hovered = node }
    }

    @ViewBuilder
    private func center(_ geometry: SunburstGeometry) -> some View {
        if let current = model.current {
            VStack(spacing: 3) {
                if model.canGoUp {
                    Image(systemName: "chevron.up")
                        .font(.system(size: 11, weight: .bold))
                        .foregroundStyle(.secondary)
                }
                Text(current.displayName)
                    .font(.system(size: 13, weight: .semibold))
                    .lineLimit(2)
                    .multilineTextAlignment(.center)
                    .truncationMode(.middle)
                Text(formatBytes(current.size))
                    .font(.system(size: 12).monospacedDigit())
                    .foregroundStyle(.secondary)
            }
            .frame(width: geometry.inner * 1.6)
            .position(geometry.center)
            .allowsHitTesting(false)
            .help(model.canGoUp ? "Click to go to the enclosing folder" : "")
        }
    }
}

struct ItemMenu: View {
    @Environment(AppModel.self) private var model
    let node: FileNode

    var body: some View {
        if node.isReal {
            if node.isDirectory, !node.children.isEmpty {
                Button("Open") { model.navigate(to: node) }
            }
            Button("Quick Look") { model.preview(node) }
            Button("Show in Finder") { model.reveal(node) }
            Button("Copy Path") { model.copyPath(node) }
            Divider()
            Button("Add to Collector") { model.collect(node) }
        } else if node.kind == .hidden {
            Button("What Is Hidden Space?") { model.showHiddenInfo = true }
        }
    }
}
