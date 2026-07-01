#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
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
    <key>CFBundleIconFile</key>
    <string>AppIcon.icns</string>
    <key>CFBundleIconName</key>
    <string>AppIcon</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleVersion</key>
    <string>1.1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.1.0</string>
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
    ICONSET="$APP_BUNDLE/Contents/Resources/AppIcon.iconset"
    mkdir -p "$ICONSET"
    sips -z 16 16 "$ICON_SRC" --out "$ICONSET/icon_16x16.png" >/dev/null
    sips -z 32 32 "$ICON_SRC" --out "$ICONSET/icon_16x16@2x.png" >/dev/null
    sips -z 32 32 "$ICON_SRC" --out "$ICONSET/icon_32x32.png" >/dev/null
    sips -z 64 64 "$ICON_SRC" --out "$ICONSET/icon_32x32@2x.png" >/dev/null
    sips -z 128 128 "$ICON_SRC" --out "$ICONSET/icon_128x128.png" >/dev/null
    sips -z 256 256 "$ICON_SRC" --out "$ICONSET/icon_128x128@2x.png" >/dev/null
    sips -z 256 256 "$ICON_SRC" --out "$ICONSET/icon_256x256.png" >/dev/null
    sips -z 512 512 "$ICON_SRC" --out "$ICONSET/icon_256x256@2x.png" >/dev/null
    sips -z 512 512 "$ICON_SRC" --out "$ICONSET/icon_512x512.png" >/dev/null
    sips -z 1024 1024 "$ICON_SRC" --out "$ICONSET/icon_512x512@2x.png" >/dev/null
    iconutil -c icns "$ICONSET" -o "$APP_BUNDLE/Contents/Resources/AppIcon.icns"
    rm -rf "$ICONSET"
    echo "App icon created from: $ICON_SRC"
fi

if [[ -f "$APP_BUNDLE/Contents/Resources/AppIcon.icns" ]]; then
    touch "$APP_BUNDLE"
fi

if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$APP_BUNDLE" >/dev/null
    echo "App bundle ad-hoc signed"
fi

echo "App bundle created at: $APP_BUNDLE"

if [[ "${PACKAGE_ONLY:-}" == "true" ]]; then
    # --- DMG packaging ---
    APP_VERSION="1.1.0"
    ARCH="$(uname -m)"
    DIST_DIR="$NATIVE_DIR/dist"
    DMG_NAME="Selene-${APP_VERSION}-macos.dmg"
    DMG_PATH="$DIST_DIR/$DMG_NAME"

    mkdir -p "$DIST_DIR"
    rm -f "$DMG_PATH"

    if command -v create-dmg >/dev/null 2>&1; then
        echo "Creating styled DMG with create-dmg..."

        CREATE_DMG_ARGS=(
            --volname "Selene"
            --app-drop-link 600 180
            --icon "SeleneNative.app" 180 180
            --window-size 800 450
            --hide-extension "SeleneNative.app"
        )

        BG_FILE="$NATIVE_DIR/assets/selene-bg.png"
        if [[ -f "$BG_FILE" ]]; then
            CREATE_DMG_ARGS+=(--background "$BG_FILE")
            echo "Using DMG background: $BG_FILE"
        fi

        create-dmg "${CREATE_DMG_ARGS[@]}" "$DMG_PATH" "$APP_BUNDLE"
    else
        echo "WARNING: create-dmg not found. Falling back to plain hdiutil DMG."
        echo "Install create-dmg for a styled DMG: brew install create-dmg"

        TMP_DMG=$(mktemp -d)
        cp -R "$APP_BUNDLE" "$TMP_DMG/SeleneNative.app"
        ln -s /Applications "$TMP_DMG/Applications"
        hdiutil create -volname "Selene" -srcfolder "$TMP_DMG" \
            -ov -format UDZO "$DMG_PATH"
        rm -rf "$TMP_DMG"
    fi

    if [[ -f "$DMG_PATH" ]]; then
        echo "DMG created at: $DMG_PATH"
        ls -lh "$DMG_PATH"
    else
        echo "ERROR: Failed to create DMG at $DMG_PATH"
        exit 1
    fi

    exit 0
fi

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
