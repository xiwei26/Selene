# Agent Instructions

## Native macOS Packaging

When the user asks to package, rebuild, or repack the native macOS version, use the existing package-only script from the repository root:

```sh
env PACKAGE_ONLY=true native-macos/script/build_and_run.sh
```

This rebuilds the Swift release binary and recreates:

```text
native-macos/SeleneNative.app
```

After packaging, verify the app bundle exists and was freshly generated. A concise check is:

```sh
ls -la native-macos/SeleneNative.app native-macos/SeleneNative.app/Contents native-macos/SeleneNative.app/Contents/MacOS native-macos/SeleneNative.app/Contents/Resources
plutil -p native-macos/SeleneNative.app/Contents/Info.plist
file native-macos/SeleneNative.app/Contents/MacOS/SeleneNative
```

Do not launch the app unless the user explicitly asks. Do not revert or discard unrelated working-tree changes while packaging.
