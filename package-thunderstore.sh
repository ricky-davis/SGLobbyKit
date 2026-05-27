#!/usr/bin/env bash
set -euo pipefail

PACKAGE_NAME="LobbyKit"
# Read version from the project's Directory.Build.props (single source of truth)
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' LobbyKit/Directory.Build.props)"
PROJECT="LobbyKit/LobbyKit.csproj"
DLL="LobbyKit/bin/Release/net6.0/LobbyKit.dll"
STAGE_DIR="dist/${PACKAGE_NAME}-${VERSION}"
ZIP_PATH="dist/${PACKAGE_NAME}-${VERSION}.zip"

dotnet build "$PROJECT" -c Release -p:DeployMod=false

rm -rf "$STAGE_DIR" "$ZIP_PATH"
mkdir -p "$STAGE_DIR/Mods"

cp manifest.json README.md CHANGELOG.md icon.png "$STAGE_DIR/"
cp "$DLL" "$STAGE_DIR/Mods/"

(
  cd "$STAGE_DIR"
  zip -r "../${PACKAGE_NAME}-${VERSION}.zip" .
)

echo "Created $ZIP_PATH"
