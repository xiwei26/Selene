# Native macOS DMG Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add DMG creation to the native macOS build pipeline so every code change produces an installable `.dmg`, and update `agents.md` to mandate it.

**Architecture:** Extend the existing `build_and_run.sh` script with a DMG creation phase after the `.app` bundle step. Use `create-dmg` (Homebrew) when available for styled DMGs; fall back to plain `hdiutil`. Version is hardcoded to `1.1.0`. Update `agents.md` to require DMG output alongside the existing Windows `.exe` requirement.

**Tech Stack:** Bash, `hdiutil`, `create-dmg` (optional Homebrew dependency), Swift Package Manager

## Global Constraints

- Version `CFBundleShortVersionString` and `CFBundleVersion` are `1.1.0` (hardcoded)
- DMG output path: `native-macos/dist/Selene-1.1.0-macos.dmg`
- `create-dmg` is the preferred tool; `hdiutil` is the guaranteed fallback
- Background image is optional: `native-macos/assets/selene-bg.png` — skip if missing
- Architecture is detected via `uname -m` (arm64 or x86_64)
- All work gated on `PACKAGE_ONLY=true` (existing behavior)

---

### Task 1: Update version in Info.plist heredoc

**Files:**
- Modify: `native-macos/script/build_and_run.sh:49-52`

**Interfaces:**
- Consumes: n/a
- Produces: `Info.plist` inside `SeleneNative.app` with version `1.1.0`

- [ ] **Step 1: Update the Info.plist version strings in the heredoc**

In `native-macos/script/build_and_run.sh`, change lines 49-52 from:

```xml
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
```

to:

```xml
    <key>CFBundleVersion</key>
    <string>1.1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.1.0</string>
```

- [ ] **Step 2: Build the .app to verify the version is embedded**

Run: `env PACKAGE_ONLY=true native-macos/script/build_and_run.sh`
Expected: script completes, `plutil -p native-macos/SeleneNative.app/Contents/Info.plist` shows `CFBundleShortVersionString => "1.1.0"` and `CFBundleVersion => "1.1.0"`

- [ ] **Step 3: Commit**

```bash
git add native-macos/script/build_and_run.sh
git commit -m "chore: bump native-macos version to 1.1.0"
```

---

### Task 2: Add DMG creation step to build_and_run.sh

**Files:**
- Modify: `native-macos/script/build_and_run.sh:90-94` (the `PACKAGE_ONLY` exit block)

**Interfaces:**
- Consumes: `$APP_BUNDLE` (the `.app` path), `$NATIVE_DIR` (project root for native-macos)
- Produces: `native-macos/dist/Selene-1.1.0-macos.dmg`

- [ ] **Step 1: Add DMG creation function and invocation after the `PACKAGE_ONLY` exit check**

Replace lines 90-94 of `build_and_run.sh` (the `PACKAGE_ONLY` block):

```bash
if [[ "${PACKAGE_ONLY:-}" == "true" ]]; then
    exit 0
fi
```

with:

```bash
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
```

- [ ] **Step 2: Create the assets directory placeholder (no background image yet)**

```bash
mkdir -p native-macos/assets
```

This directory exists for a future `selene-bg.png`. Leave it empty — the script
skips the `--background` flag when the file is absent.

- [ ] **Step 3: Build and verify the DMG is created**

Run: `env PACKAGE_ONLY=true native-macos/script/build_and_run.sh`
Expected: script completes, file `native-macos/dist/Selene-1.1.0-macos.dmg` exists and is a valid DMG.

Verify:

```bash
ls -lh native-macos/dist/Selene-1.1.0-macos.dmg
# Should show a file ~5-10 MB
```

```bash
hdiutil attach native-macos/dist/Selene-1.1.0-macos.dmg -readonly -nobrowse -mountpoint /tmp/selene-dmg-verify
ls /tmp/selene-dmg-verify/
# Should show: SeleneNative.app  Applications (symlink)
hdiutil detach /tmp/selene-dmg-verify
```

- [ ] **Step 4: Commit**

```bash
git add native-macos/script/build_and_run.sh native-macos/assets/.gitkeep 2>/dev/null; git add native-macos/assets
git commit -m "feat: add DMG packaging to native-macos build script"
```

---

### Task 3: Update agents.md to mandate DMG packaging

**Files:**
- Modify: `agents.md:48-71` (the macOS packaging section)

**Interfaces:**
- Consumes: n/a
- Produces: Updated documentation for future agents

- [ ] **Step 1: Update the macOS packaging section in agents.md**

Replace the existing macOS packaging block (lines 48-71):

```markdown
For native macOS changes, use the existing package-only script from the
repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

This rebuilds the Swift release binary and recreates:

```text
native-macos/SeleneNative.app
```

After packaging, verify the app bundle exists and was freshly generated. A
concise check is:

```sh
ls -la native-macos/SeleneNative.app native-macos/SeleneNative.app/Contents native-macos/SeleneNative.app/Contents/MacOS native-macos/SeleneNative.app/Contents/Resources
plutil -p native-macos/SeleneNative.app/Contents/Info.plist
file native-macos/SeleneNative.app/Contents/MacOS/SeleneNative
```

Do not launch the app unless the user explicitly asks.
```

with:

```markdown
For native macOS changes, use the existing package-only script from the
repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

This rebuilds the Swift release binary, recreates the `.app` bundle, and
produces an installable DMG:

```text
native-macos/dist/Selene-1.1.0-macos.dmg
```

After packaging, verify both the app bundle and DMG exist and were freshly
generated. A concise check is:

```sh
ls -la native-macos/SeleneNative.app native-macos/SeleneNative.app/Contents native-macos/SeleneNative.app/Contents/MacOS native-macos/SeleneNative.app/Contents/Resources
plutil -p native-macos/SeleneNative.app/Contents/Info.plist
file native-macos/SeleneNative.app/Contents/MacOS/SeleneNative
ls -lh native-macos/dist/Selene-1.1.0-macos.dmg
```

Report the DMG path in the final response. Do not stop at the `.app` bundle
when a DMG can be built. Do not launch the app unless the user explicitly
asks.
```

- [ ] **Step 2: Commit**

```bash
git add agents.md
git commit -m "docs: update agents.md to mandate DMG packaging for macOS"
```

---

### Task 4: End-to-end verification build

**Files:**
- No file changes — verification only

**Interfaces:**
- Consumes: All changes from Tasks 1-3
- Produces: Confirmed working DMG at `native-macos/dist/Selene-1.1.0-macos.dmg`

- [ ] **Step 1: Clean previous build artifacts and run a full build**

```bash
rm -rf native-macos/SeleneNative.app native-macos/dist/Selene-1.1.0-macos.dmg
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

Expected: script builds, packages `.app`, creates DMG, exits 0.

- [ ] **Step 2: Verify the DMG**

```bash
ls -lh native-macos/dist/Selene-1.1.0-macos.dmg
plutil -p native-macos/SeleneNative.app/Contents/Info.plist | grep -E 'CFBundleShortVersionString|CFBundleVersion'
hdiutil attach native-macos/dist/Selene-1.1.0-macos.dmg -readonly -nobrowse -mountpoint /tmp/selene-dmg-verify
ls /tmp/selene-dmg-verify/
hdiutil detach /tmp/selene-dmg-verify
```

Expected:
- DMG file exists and is non-zero size
- `Info.plist` shows version `1.1.0`
- DMG mounts and contains `SeleneNative.app` + `Applications` symlink
- DMG detaches cleanly
