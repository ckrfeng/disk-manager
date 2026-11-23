import DiskCore
import SwiftUI

struct HSB {
    var h: Double, s: Double, b: Double

    func color(boost: Double = 0) -> Color {
        Color(hue: h, saturation: max(0, s - boost * 0.25), brightness: min(1, b + boost * 0.12))
    }
}

struct Segment {
    let node: FileNode?
    let depth: Int
    let start: Double
    let end: Double
    let hsb: HSB
}

struct SunburstLayout {
    static let maxDepth = 5
    private static let minFraction = 0.003
    private static let palette: [HSB] = [
        HSB(h: 0.58, s: 0.72, b: 0.95), HSB(h: 0.47, s: 0.70, b: 0.80), HSB(h: 0.31, s: 0.62, b: 0.80),
        HSB(h: 0.14, s: 0.78, b: 0.96), HSB(h: 0.07, s: 0.78, b: 0.97), HSB(h: 0.99, s: 0.68, b: 0.94),
        HSB(h: 0.89, s: 0.58, b: 0.90), HSB(h: 0.75, s: 0.52, b: 0.92),
    ]
    private static let fileGray = HSB(h: 0.6, s: 0.04, b: 0.62)
    private static let otherGray = HSB(h: 0.6, s: 0.05, b: 0.36)
    private static let hiddenGray = HSB(h: 0.6, s: 0.08, b: 0.26)

    private(set) var segments: [Segment] = []
    private(set) var topColors: [ObjectIdentifier: HSB] = [:]

    init() {}

    init(root: FileNode) {
        guard root.size > 0 else { return }
        add(root.children, start: 0, depth: 0, total: Double(root.size), base: nil)
    }

    func color(forTopLevel node: FileNode) -> Color {
        (topColors[ObjectIdentifier(node)] ?? Self.otherGray).color()
    }

    func segment(depth: Int, fraction: Double) -> Segment? {
        segments.first { $0.depth == depth && $0.start <= fraction && fraction < $0.end }
    }

    private mutating func add(_ children: [FileNode], start: Double, depth: Int, total: Double, base: HSB?) {
        var cursor = start
        var paletteIndex = 0
        for (index, child) in children.enumerated() {
            let fraction = Double(child.size) / total
            if fraction < Self.minFraction {
                let rest = children[index...].reduce(0.0) { $0 + Double($1.size) } / total
                if rest >= Self.minFraction / 2 {
                    segments.append(Segment(node: nil, depth: depth, start: cursor, end: cursor + rest,
                                            hsb: Self.otherGray))
                }
                break
            }
            let hsb: HSB
            switch child.kind {
            case .hidden: hsb = Self.hiddenGray
            case .file, .aggregate:
                hsb = base.map { HSB(h: $0.h, s: 0.10, b: 0.60) } ?? Self.fileGray
            case .directory:
                if let base {
                    let shade = index.isMultiple(of: 2) ? 0.0 : 0.07
                    hsb = HSB(h: base.h, s: max(0.25, base.s - 0.07 * Double(depth)),
                              b: max(0.35, base.b - 0.05 * Double(depth) - shade))
                } else {
                    hsb = Self.palette[paletteIndex % Self.palette.count]
                    paletteIndex += 1
                }
            }
            if depth == 0 { topColors[ObjectIdentifier(child)] = hsb }
            segments.append(Segment(node: child, depth: depth, start: cursor, end: cursor + fraction, hsb: hsb))
            if child.isDirectory, depth + 1 < Self.maxDepth {
                add(child.children, start: cursor, depth: depth + 1, total: total, base: base ?? hsb)
            }
            cursor += fraction
        }
    }
}
