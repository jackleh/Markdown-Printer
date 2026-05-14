#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_FILE="$PROJECT_ROOT/MDPrinter.csproj"
PUBLISH_DIR="$PROJECT_ROOT/artifacts/publish/mac/MarkdownPrinter-maccatalyst-x64"
DIST_DIR="$PROJECT_ROOT/artifacts/dist"

rm -rf "$PUBLISH_DIR"
mkdir -p "$DIST_DIR"
mkdir -p "$PUBLISH_DIR"

cd "$PROJECT_ROOT"

dotnet restore "$PROJECT_FILE" -r maccatalyst-x64

dotnet publish \
  -c Release \
  -f net10.0-maccatalyst \
  -r maccatalyst-x64 \
  --no-restore \
  -p:CreatePackage=true \
  -o "$PUBLISH_DIR"

APP_PATH="$(find "$PUBLISH_DIR" "$PROJECT_ROOT/bin/Release/net10.0-maccatalyst" -type d -name '*.app' | head -n 1)"

if [[ -z "$APP_PATH" ]]; then
    echo "No .app bundle was produced. Run this script on macOS with Xcode and the Mac Catalyst workload installed." >&2
    exit 1
fi

ZIP_PATH="$DIST_DIR/MarkdownPrinter-maccatalyst.app.zip"
rm -f "$ZIP_PATH"

ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ZIP_PATH"

echo "Created Mac distributable:"
echo "  $ZIP_PATH"
