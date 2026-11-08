#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Tables"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_TABLE_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~TableRoundTripTests.IFileDiPartenzaSonoQuelliDelCampione" > /dev/null
convert() {
  mkdir -p "$work/out"
  "$soffice" -env:UserInstallation=file://$profile --headless --convert-to "$2" --outdir "$work/out" "$work/$1" > /dev/null 2>&1
  mv "$work/out/${1%.*}.${3}" "$target/$4"
}
convert tables.odt odt odt tables.lo.odt
convert tables.docx "docx:MS Word 2007 XML" docx tables.lo.docx
convert tables.odt "docx:MS Word 2007 XML" docx tables.odt.lo.docx
convert tables.docx odt odt tables.docx.lo.odt
rm -rf "$work" "$profile"
