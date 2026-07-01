# Native macOS DMG Packaging — Design Spec

**Date:** 2026-07-01
**Status:** Approved

## Overview

Add DMG creation to the native macOS build pipeline so that every code change
produces an installable `.dmg` file, matching the Windows `.exe` requirement
already documented in `agents.md`.

## Requirements

1. Version `1.1.0` in `Info.plist` (hardcoded; pubspec.yaml auto-read deferred).
2. Styled DMG with app icon + Applications symlink + optional background image.
3. Output path: `native-macos/dist/Selene-1.1.0-macos.dmg`.
4. Use `create-dmg` (Homebrew) when available; fall back to plain `hdiutil`.
5. Update `agents.md` to mandate DMG packaging after every macOS change.

## Build Script Changes (`native-macos/script/build_and_run.sh`)

### Version

Update the inline `Info.plist` heredoc:

- `CFBundleShortVersionString` → `1.1.0`
- `CFBundleVersion` → `1.1.0`

### DMG Creation Step

Inserted after code-signing, gated on `PACKAGE_ONLY=true`:

1. Determine architecture (`arm64` or `x86_64`) via `uname -m`.
2. `mkdir -p native-macos/dist`.
3. Remove any existing DMG at the target path to ensure clean rebuilds.
4. If `create-dmg` is on `$PATH`:
   - Invoke with `--app-drop-link`, `--icon`, `--window-size`, and optional
     `--background` (if `native-macos/assets/selene-bg.png` exists).
5. Else fall back to plain `hdiutil create -volname -srcfolder -format UDZO`.
   Print a warning that `create-dmg` produces better results and can be
   installed via `brew install create-dmg`.
6. Report the final DMG path.

### Fallback DMG (hdiutil)

```sh
TMP_DMG=$(mktemp -d)
cp -R "$APP_BUNDLE" "$TMP_DMG/"
ln -s /Applications "$TMP_DMG/Applications"
hdiutil create -volname "Selene" -srcfolder "$TMP_DMG" \
  -ov -format UDZO "$DMG_PATH"
rm -rf "$TMP_DMG"
```

## Output

```
native-macos/dist/Selene-1.1.0-macos.dmg
```

## agents.md Updates

Add a parallel section to the existing "Packaging After Changes" guidance:

- macOS changes must produce a `.dmg` installer before finishing.
- Report the DMG path in the final response.
- Verification: `hdiutil attach` + check app bundle exists inside.

## Future Work (Not In Scope)

- Auto-read version from `pubspec.yaml` (consistent with Windows pipeline).
- Code-signing with a Developer ID certificate for distribution outside
  the local machine.
- Notarization via `notarytool`.
