#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/References"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_REFERENCE_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~ReferenceRoundTripTests.IFileDiPartenzaSonoQuelliDelCampione" > /dev/null
convert() {
  mkdir -p "$work/out"
  "$soffice" -env:UserInstallation=file://$profile --headless --convert-to "$2" --outdir "$work/out" "$work/$1" > /dev/null 2>&1
  mv "$work/out/${1%.*}.${3}" "$target/$4"
}
convert references.odt odt odt references.lo.odt
convert references.docx "docx:MS Word 2007 XML" docx references.lo.docx
convert references.odt "docx:MS Word 2007 XML" docx references.odt.lo.docx
convert references.docx odt odt references.docx.lo.odt
rm -rf "$work" "$profile"
