// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "DiskManager",
    platforms: [.macOS(.v14)],
    targets: [
        .target(name: "DiskCore"),
        .executableTarget(name: "DiskManager", dependencies: ["DiskCore"]),
        .testTarget(name: "DiskCoreTests", dependencies: ["DiskCore"]),
    ]
)
