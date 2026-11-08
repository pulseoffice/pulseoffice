#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Master"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_MASTER_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~MasterDocumentParityTests.IFileDiPartenzaDelMasterSonoQuelliDelCampione" > /dev/null
mkdir -p "$work/out"
"$soffice" -env:UserInstallation=file://$profile --headless --convert-to "odm" --outdir "$work/out" "$work/master.odm" > /dev/null 2>&1
mv "$work/out/master.odm" "$target/master.lo.odm"
"$soffice" -env:UserInstallation=file://$profile --headless --convert-to "odt:writerglobal8_writer" --outdir "$work/out" "$work/master.odm" > /dev/null 2>&1
mv "$work/out/master.odt" "$target/master.lo.odt"
rm -rf "$work" "$profile"
