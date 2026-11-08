#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Objects"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_OBJECT_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~ObjectRoundTripTests.IFileDiPartenzaSonoQuelliDelCampione" > /dev/null
convert() {
  mkdir -p "$work/out"
  "$soffice" -env:UserInstallation=file://$profile --headless --convert-to "$2" --outdir "$work/out" "$work/$1" > /dev/null 2>&1
  mv "$work/out/${1%.*}.${3}" "$target/$4"
}
convert objects.odt odt odt objects.lo.odt
convert objects.docx "docx:MS Word 2007 XML" docx objects.lo.docx
convert objects.odt "docx:MS Word 2007 XML" docx objects.odt.lo.docx
convert objects.docx odt odt objects.docx.lo.odt
rm -rf "$work" "$profile"
