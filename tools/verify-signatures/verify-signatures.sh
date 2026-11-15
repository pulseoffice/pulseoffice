#!/usr/bin/env bash
set -u
(( $# > 0 )) || { echo "usage: verify-signatures.sh file..."; exit 2; }
soffice=${REFERENCE_SUITE:-$(cd "$(dirname "$0")/.." && pwd)/reference-suite/run}
work=$(mktemp -d "${TMPDIR:-/tmp}/pulseoffice-reference-signatures.XXXXXX")
profile=$work/profile
report=$work/report.txt
perl -e 'alarm 120; exec @ARGV' "$soffice" --headless --norestore --nologo --terminate_after_init "-env:UserInstallation=file://$profile" > /dev/null 2>&1
module=$profile/user/basic/Standard/Module1.xba
[[ -f $module ]] || { echo "profile not initialised: $profile"; exit 1; }
{
  echo '<?xml version="1.0" encoding="UTF-8"?>'
  echo '<!DOCTYPE script:module PUBLIC "-//OpenOffice.org//DTD OfficeDocument 1.0//EN" "module.dtd">'
  echo '<script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">'
  echo 'Sub Verify'
  echo '  On Error GoTo Failed'
  echo "  Dim files($(( $# - 1 ))) As String"
  i=0
  for f in "$@"; do echo "  files($i) = \"$(realpath "$f")\""; i=$(( i + 1 )); done
  echo '  Dim out As String'
  echo '  factory = createUnoService("com.sun.star.embed.StorageFactory")'
  echo '  signer = createUnoServiceWithArguments("com.sun.star.security.DocumentDigitalSignatures", Array("1.2", False))'
  echo '  Dim format As New com.sun.star.beans.PropertyValue'
  echo '  format.Name = "StorageFormat" : format.Value = "ZipFormat"'
  echo '  For k = 0 To UBound(files)'
  echo '    storage = factory.createInstanceWithArguments(Array(ConvertToURL(files(k)), 1, Array(format)))'
  echo '    infos = signer.verifyDocumentContentSignatures(storage, Nothing)'
  echo '    out = out &amp; files(k) &amp; Chr(9) &amp; (UBound(infos) + 1)'
  echo '    For j = 0 To UBound(infos)'
  echo '      out = out &amp; Chr(9) &amp; "valid=" &amp; infos(j).SignatureIsValid &amp; " certificate=" &amp; infos(j).CertificateStatus &amp; " partial=" &amp; infos(j).PartialDocumentSignature &amp; " signer=" &amp; infos(j).Signer.SubjectName'
  echo '    Next j'
  echo '    out = out &amp; Chr(10)'
  echo '  Next k'
  echo '  f = FreeFile'
  echo "  Open \"$report\" For Output As #f"
  echo '  Print #f, out'
  echo '  Close #f'
  echo '  Exit Sub'
  echo 'Failed:'
  echo '  g = FreeFile'
  echo "  Open \"$report\" For Output As #g"
  echo '  Print #g, "error " &amp; Err &amp; " line " &amp; Erl &amp; ": " &amp; Error$'
  echo '  Close #g'
  echo 'End Sub'
  echo '</script:module>'
} > "$module"
perl -e 'alarm 180; exec @ARGV' "$soffice" --headless --norestore --nologo "-env:UserInstallation=file://$profile" "macro:///Standard.Module1.Verify" > /dev/null 2>&1
cat "$report"
