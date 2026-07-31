#!/bin/zsh

set -euo pipefail

script_dir=${0:A:h}
ios_root=${script_dir:h}
source_root="$ios_root/Selene"
build_root="$ios_root/build/Simulator"
app_path="$build_root/Selene.app"
archive_path="$build_root/Selene-native-ios-simulator.zip"
sdk_path=$(xcrun --sdk iphonesimulator --show-sdk-path)
sources=("${(@f)$(find "$source_root" -name '*.swift' -print | sort)}")

mkdir -p "$build_root/arm64" "$build_root/x86_64" "$app_path"

for architecture in arm64 x86_64; do
    swiftc \
        -parse-as-library \
        -O \
        -whole-module-optimization \
        -sdk "$sdk_path" \
        -target "$architecture-apple-ios17.0-simulator" \
        -module-name Selene \
        "${sources[@]}" \
        -o "$build_root/$architecture/Selene"
done

lipo -create \
    "$build_root/arm64/Selene" \
    "$build_root/x86_64/Selene" \
    -output "$app_path/Selene"

cp "$source_root/Resources/Info.plist" "$app_path/Info.plist"
plutil -replace CFBundleExecutable -string Selene "$app_path/Info.plist"
plutil -replace CFBundleIdentifier -string com.selene.native "$app_path/Info.plist"
plutil -replace CFBundleName -string Selene "$app_path/Info.plist"
plutil -replace CFBundleShortVersionString -string 1.1.0 "$app_path/Info.plist"
plutil -replace CFBundleVersion -string 1 "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :CFBundleSupportedPlatforms array" "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :CFBundleSupportedPlatforms:0 string iPhoneSimulator" "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :MinimumOSVersion string 17.0" "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :UIDeviceFamily array" "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :UIDeviceFamily:0 integer 1" "$app_path/Info.plist"
/usr/libexec/PlistBuddy -c "Add :UIDeviceFamily:1 integer 2" "$app_path/Info.plist"

codesign --force --sign - "$app_path"
ditto -c -k --keepParent "$app_path" "$archive_path"

file "$app_path/Selene"
codesign --verify --deep --strict "$app_path"
ls -lh "$app_path" "$archive_path"