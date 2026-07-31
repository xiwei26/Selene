#!/bin/zsh

set -euo pipefail

script_dir=${0:A:h}
ios_root=${script_dir:h}
source_root="$ios_root/Selene"
build_root="$ios_root/build/Tests"
test_binary="$build_root/ContractTests"

mkdir -p "$build_root"

# Contract tests compile Foundation/network code on macOS. Skip the app shell:
# SwiftUI views, the @main App entry, and AVKit player coordinators.
sources=("${(@f)$(
  find "$source_root" -name '*.swift' -print \
    | grep -v '/Views/' \
    | grep -v '/App/SeleneApp.swift' \
    | grep -v '/Design/' \
    | grep -v '/Stores/FeatureStores.swift' \
    | grep -v '/Stores/AppModel.swift' \
    | sort
)}")

# Defensive: drop anything that still pulls UI frameworks.
filtered=()
for source in "${sources[@]}"; do
  if grep -qE '^import (SwiftUI|UIKit|AVKit)$' "$source"; then
    echo "Skipping UI-bound source: $source"
    continue
  fi
  filtered+=("$source")
done
sources=("${filtered[@]}")

echo "Compiling ${#sources[@]} sources…"

arch=$(uname -m)
case "$arch" in
  arm64) triple="arm64-apple-macos14.0" ;;
  *)     triple="x86_64-apple-macos14.0" ;;
esac

swiftc \
    -parse-as-library \
    -O \
    -target "$triple" \
    -module-name SeleneTests \
    "${sources[@]}" \
    "$ios_root/Tests/LunaTVClientContractSmokeTests.swift" \
    -o "$test_binary"

"$test_binary"
