// swift-tools-version:5.9
import PackageDescription

let package = Package(
    name: "SeleneNative",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(
            name: "SeleneNative",
            targets: ["SeleneNative"]
        ),
    ],
    targets: [
        .executableTarget(
            name: "SeleneNative",
            dependencies: []
        ),
        .testTarget(
            name: "SeleneNativeTests",
            dependencies: ["SeleneNative"]
        ),
    ]
)
