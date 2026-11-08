#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Formatting"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
PULSE_FORMATTING_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~IFileDiPartenzaSonoQuelliDelCampione" > /dev/null
convert() {
  mkdir -p "$work/out"
  "$soffice" -env:UserInstallation=file://$profile --headless --convert-to "$2" --outdir "$work/out" "$work/$1" > /dev/null 2>&1
  mv "$work/out/${1%.*}.${3}" "$target/$4"
}
convert sample.odt odt odt sample.lo.odt
convert sample.docx "docx:MS Word 2007 XML" docx sample.lo.docx
convert sample.odt "docx:MS Word 2007 XML" docx sample.odt.lo.docx
convert sample.docx odt odt sample.docx.lo.odt
rm -rf "$work" "$profile"
