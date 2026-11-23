#!/usr/bin/env bash
# Builds "AK Card Scanner.app" for macOS.
# Usage: ./build-app.sh [osx-arm64|osx-x64]   (default: osx-arm64, Apple Silicon)
# Requires the .NET 8 SDK. Run on a Mac to get an ad-hoc signed app;
# elsewhere the bundle is built unsigned and must be signed on the Mac (see README).
set -euo pipefail

RID="${1:-osx-arm64}"
HERE="$(cd "$(dirname "$0")" && pwd)"
OUT="$HERE/dist"
PUB="$OUT/publish-$RID"
APP="$OUT/AK Card Scanner.app"

rm -rf "$PUB" "$APP"
dotnet publish "$HERE/AKCardScanner.Mac.csproj" -c Release -r "$RID" --self-contained true -o "$PUB"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUB/." "$APP/Contents/MacOS/"
rm -f "$APP/Contents/MacOS/"*.pdb
cp "$HERE/Info.plist" "$APP/Contents/Info.plist"
cp "$HERE/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
chmod +x "$APP/Contents/MacOS/AKCardScanner"

if command -v codesign >/dev/null 2>&1; then
  xattr -cr "$APP" || true
  codesign --force --deep -s - "$APP"
  echo "Built and ad-hoc signed: $APP"
else
  echo "Built (unsigned): $APP"
  echo "On the Mac, run: xattr -cr \"AK Card Scanner.app\" && codesign --force --deep -s - \"AK Card Scanner.app\""
fi
