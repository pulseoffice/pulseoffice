#!/bin/sh
set -e
repo=$(cd "$(dirname "$0")/.." && pwd)
target="$repo/tests/PulseOffice.Formats.OpenXml.Tests/Fixtures/Review"
work=$(mktemp -d)
profile=$(mktemp -d)
soffice=${SOFFICE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
mkdir -p "$target"
PULSE_REVIEW_FIXTURES="$work" dotnet test "$repo/tests/PulseOffice.Formats.OpenXml.Tests" --filter "FullyQualifiedName~ReviewRoundTripTests.IFileDiPartenzaSonoQuelliDelCampione" > /dev/null
convert() {
  mkdir -p "$work/out"
  "$soffice" -env:UserInstallation=file://$profile --headless --convert-to "$2" --outdir "$work/out" "$work/$1" > /dev/null 2>&1
  mv "$work/out/${1%.*}.${3}" "$target/$4"
}
convert review.odt odt odt review.lo.odt
convert review.docx "docx:MS Word 2007 XML" docx review.lo.docx
convert review.odt "docx:MS Word 2007 XML" docx review.odt.lo.docx
convert review.docx odt odt review.docx.lo.odt
rm -rf "$work" "$profile"
