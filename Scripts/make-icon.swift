import AppKit

let size = 1024.0
let output = CommandLine.arguments[1]
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size), pixelsHigh: Int(size), bitsPerSample: 8,
                           samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                           bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
let context = NSGraphicsContext.current!.cgContext

let tile = CGRect(x: 100, y: 100, width: 824, height: 824)
let tilePath = CGPath(roundedRect: tile, cornerWidth: 186, cornerHeight: 186, transform: nil)
context.saveGState()
context.setShadow(offset: CGSize(width: 0, height: -10), blur: 24, color: NSColor.black.withAlphaComponent(0.35).cgColor)
context.addPath(tilePath)
context.setFillColor(NSColor.black.cgColor)
context.fillPath()
context.restoreGState()

context.saveGState()
context.addPath(tilePath)
context.clip()
let background = CGGradient(colorsSpace: CGColorSpaceCreateDeviceRGB(), colors: [
    NSColor(red: 0.20, green: 0.21, blue: 0.26, alpha: 1).cgColor,
    NSColor(red: 0.08, green: 0.085, blue: 0.105, alpha: 1).cgColor,
] as CFArray, locations: [0, 1])!
context.drawLinearGradient(background, start: CGPoint(x: 512, y: tile.maxY), end: CGPoint(x: 512, y: tile.minY),
                           options: [])

let center = CGPoint(x: 512, y: 512)
let inner = 100.0, ring = 66.0, gap = 6.0
let top: [(fraction: Double, hue: Double, sat: Double, bri: Double)] = [
    (0.29, 0.58, 0.72, 0.97), (0.19, 0.47, 0.70, 0.84), (0.14, 0.31, 0.62, 0.84), (0.11, 0.14, 0.78, 0.98),
    (0.09, 0.07, 0.78, 0.98), (0.07, 0.99, 0.68, 0.95), (0.06, 0.89, 0.58, 0.92), (0.05, 0.75, 0.52, 0.94),
]
let coverage: [[Double]] = [[1], [0.62, 0.3], [0.34, 0.2, 0.16], [0.2, 0.12]]

func sector(_ depth: Int, _ start: Double, _ end: Double, _ color: NSColor) {
    let r0 = inner + Double(depth) * ring, r1 = r0 + ring - gap
    let pad = gap / 2 / ((r0 + r1) / 2)
    let a0 = .pi / 2 - start * 2 * .pi - pad, a1 = .pi / 2 - end * 2 * .pi + pad
    guard a0 > a1 else { return }
    let path = CGMutablePath()
    path.addArc(center: center, radius: r1, startAngle: a0, endAngle: a1, clockwise: true)
    path.addArc(center: center, radius: r0, startAngle: a1, endAngle: a0, clockwise: false)
    path.closeSubpath()
    context.addPath(path)
    context.setFillColor(color.cgColor)
    context.fillPath()
}

var cursor = 0.0
for (index, item) in top.enumerated() {
    for (depth, parts) in coverage.enumerated() {
        var start = cursor
        for (partIndex, part) in parts.enumerated() {
            let span = item.fraction * part * (index.isMultiple(of: 2) || depth < 2 ? 1 : 0.8)
            let color = NSColor(hue: item.hue, saturation: max(0.3, item.sat - 0.09 * Double(depth)),
                                brightness: max(0.4, item.bri - 0.07 * Double(depth) - 0.06 * Double(partIndex)),
                                alpha: 1)
            if span > 0.012 { sector(depth, start, start + span, color) }
            start += span
        }
    }
    cursor += item.fraction
}
context.restoreGState()

try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: output))
