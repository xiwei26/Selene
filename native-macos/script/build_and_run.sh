#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
NATIVE_DIR="$REPO_ROOT/native-macos"

if [[ ! -d "$NATIVE_DIR" ]]; then
    echo "Error: native-macos/ directory not found at $NATIVE_DIR"
    exit 1
fi

cd "$NATIVE_DIR"

echo "Building SeleneNative..."
swift build -c release

BUILD_DIR=".build/release"
APP_NAME="SeleneNative"
APP_BUNDLE="$NATIVE_DIR/$APP_NAME.app"

rm -rf "$APP_BUNDLE"
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy binary
cp "$BUILD_DIR/$APP_NAME" "$APP_BUNDLE/Contents/MacOS/$APP_NAME"

# Copy Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>SeleneNative</string>
    <key>CFBundleIdentifier</key>
    <string>com.selene.native</string>
    <key>CFBundleName</key>
    <string>Selene</string>
    <key>CFBundleDisplayName</key>
    <string>Selene</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>14.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
PLIST

# Build icon set (use repo logo as source)
ICON_SRC="$REPO_ROOT/logo.png"
if [[ -f "$ICON_SRC" ]]; then
    echo "Note: App icon setup requires iconutil or manual icns creation"
fi

echo "App bundle created at: $APP_BUNDLE"

# Handle verification flag
if [[ "${VERIFY:-}" == "true" ]]; then
    echo "Verifying app process..."
    "$APP_BUNDLE/Contents/MacOS/$APP_NAME" &
    APP_PID=$!
    sleep 2
    if kill -0 "$APP_PID" 2>/dev/null; then
        echo "Verification PASSED: App process running (PID: $APP_PID)"
        kill "$APP_PID" 2>/dev/null || true
        exit 0
    else
        echo "Verification FAILED: App process not running"
        exit 1
    fi
else
    # Launch the app
    echo "Launching $APP_BUNDLE..."
    open "$APP_BUNDLE"
fi
