#!/bin/zsh
set -u
(( $# > 0 )) || { print "usage: verify-signatures.sh file..."; exit 2; }
soffice=${REFERENCE_SUITE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
work=$(mktemp -d "${TMPDIR:-/tmp}/pulseoffice-reference-signatures.XXXXXX")
profile=$work/profile
report=$work/report.txt
perl -e 'alarm 120; exec @ARGV' "$soffice" --headless --norestore --nologo --terminate_after_init "-env:UserInstallation=file://$profile" > /dev/null 2>&1
module=$profile/user/basic/Standard/Module1.xba
[[ -f $module ]] || { print "profile not initialised: $profile"; exit 1; }
{
  print '<?xml version="1.0" encoding="UTF-8"?>'
  print '<!DOCTYPE script:module PUBLIC "-//OpenOffice.org//DTD OfficeDocument 1.0//EN" "module.dtd">'
  print '<script:module xmlns:script="http://openoffice.org/2000/script" script:name="Module1" script:language="StarBasic">'
  print 'Sub Verify'
  print '  On Error GoTo Failed'
  print "  Dim files($(( $# - 1 ))) As String"
  i=0
  for f in "$@"; do print "  files($i) = \"${f:A}\""; i=$(( i + 1 )); done
  print '  Dim out As String'
  print '  factory = createUnoService("com.sun.star.embed.StorageFactory")'
  print '  signer = createUnoServiceWithArguments("com.sun.star.security.DocumentDigitalSignatures", Array("1.2", False))'
  print '  Dim format As New com.sun.star.beans.PropertyValue'
  print '  format.Name = "StorageFormat" : format.Value = "ZipFormat"'
  print '  For k = 0 To UBound(files)'
  print '    storage = factory.createInstanceWithArguments(Array(ConvertToURL(files(k)), 1, Array(format)))'
  print '    infos = signer.verifyDocumentContentSignatures(storage, Nothing)'
  print '    out = out &amp; files(k) &amp; Chr(9) &amp; (UBound(infos) + 1)'
  print '    For j = 0 To UBound(infos)'
  print '      out = out &amp; Chr(9) &amp; "valid=" &amp; infos(j).SignatureIsValid &amp; " certificate=" &amp; infos(j).CertificateStatus &amp; " partial=" &amp; infos(j).PartialDocumentSignature &amp; " signer=" &amp; infos(j).Signer.SubjectName'
  print '    Next j'
  print '    out = out &amp; Chr(10)'
  print '  Next k'
  print '  f = FreeFile'
  print "  Open \"$report\" For Output As #f"
  print '  Print #f, out'
  print '  Close #f'
  print '  Exit Sub'
  print 'Failed:'
  print '  g = FreeFile'
  print "  Open \"$report\" For Output As #g"
  print '  Print #g, "error " &amp; Err &amp; " line " &amp; Erl &amp; ": " &amp; Error$'
  print '  Close #g'
  print 'End Sub'
  print '</script:module>'
} > "$module"
perl -e 'alarm 180; exec @ARGV' "$soffice" --headless --norestore --nologo "-env:UserInstallation=file://$profile" "macro:///Standard.Module1.Verify" > /dev/null 2>&1
cat "$report"
