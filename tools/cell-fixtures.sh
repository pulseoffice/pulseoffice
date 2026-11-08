#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Cells"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_CELL_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~CellParityTests.IFileDiPartenzaDelleCelleSonoQuelliDelCampione" > /dev/null
mkdir -p "$work/out"
"$soffice" -env:UserInstallation=file://$profile --headless --convert-to odt --outdir "$work/out" "$work/cells.odt" > /dev/null 2>&1
mv "$work/out/cells.odt" "$target/cells.lo.odt"
rm -rf "$work" "$profile"
