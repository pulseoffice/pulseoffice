#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Tools"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_TOOL_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~ToolParityTests.IFileDiPartenzaDegliStrumentiSonoQuelliDelCampione" > /dev/null
mkdir -p "$work/out"
"$soffice" -env:UserInstallation=file://$profile --headless --convert-to odt --outdir "$work/out" "$work/tools.odt" > /dev/null 2>&1
mv "$work/out/tools.odt" "$target/tools.lo.odt"
rm -rf "$work" "$profile"
