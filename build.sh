#!/usr/bin/env bash
# Builds CouchGamingNinja.exe (.NET Framework 4.8, already part of Windows 10/11) and runs the core tests.
# Needs Mono (apt install mono-devel). Works on Linux, macOS and in GitHub Actions.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p dist

REFS="-r:System.Web.Extensions.dll -r:System.Core.dll"
CORE="src/AppPaths.cs src/Json.cs src/Legendary.cs src/InstallFinder.cs src/Vdf.cs src/Steam.cs src/Launcher.cs src/RetroBat.cs"

mcs -sdk:4.8 -langversion:7.2 -out:dist/tests.exe $REFS -r:System.Windows.Forms.dll $CORE tests/CoreTests.cs
mono dist/tests.exe
rm dist/tests.exe

mcs -sdk:4.8 -langversion:7.2 -target:winexe -optimize+ -out:dist/CouchGamingNinja.exe \
    $REFS -r:System.Windows.Forms.dll -r:System.Drawing.dll src/*.cs
echo "Built dist/CouchGamingNinja.exe"
