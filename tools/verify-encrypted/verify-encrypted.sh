#!/bin/zsh
set -u
here=${0:A:h}
root=${here:h:h}
soffice=${REFERENCE_SUITE:-/Applications/LibreOffice.app/Contents/MacOS/soffice}
work=$(mktemp -d "${TMPDIR:-/tmp}/pulseoffice-reference-encrypted.XXXXXX")
samples=$work/samples
profile=$work/profile
mkdir -p "$samples" "$profile/user/Scripts/python"
cp "$here/verify_encrypted.py" "$profile/user/Scripts/python/"

PULSEOFFICE_ENCRYPTED_SAMPLES=$samples dotnet test "$root/tests/PulseOffice.Formats.Odf.Tests" \
  --filter "FullyQualifiedName~EncryptedSampleExport" > "$work/dotnet.log" 2>&1 || { cat "$work/dotnet.log"; exit 1; }

for name in encrypted.ods encrypted.odp; do
  [[ -f $samples/$name ]] || { print "missing sample $name"; exit 1; }
done

password=$(sed -n 's/.*const string Password = "\(.*\)";/\1/p' "$root/tests/PulseOffice.Formats.Odf.Tests/EncryptedSampleExport.cs")
QO_SAMPLES=$samples QO_PASSWORD=$password perl -e 'alarm 180; exec @ARGV' "$soffice" --headless --norestore --nologo \
  "-env:UserInstallation=file://$profile" \
  "vnd.sun.star.script:verify_encrypted.py\$verify?language=Python&location=user" > "$work/soffice.log" 2>&1

result=0
[[ -f $samples/report.txt ]] && cat "$samples/report.txt" || { print "the reference suite produced no report"; result=1; }
check() {
  local file=$1 entry=$2 marker=$3
  if [[ -f $file ]] && unzip -p "$file" "$entry" | grep -q "$marker"; then
    print "$file:t contains $marker"
  else
    print "$file:t does NOT contain $marker"; result=1
  fi
}
check "$samples/plain-encrypted.ods" content.xml SegretoFoglioPulseOffice
check "$samples/plain-encrypted.odp" content.xml SegretoDiapositivaPulseOffice
for pdf in "$samples"/encrypted.ods.pdf "$samples"/encrypted.odp.pdf; do [[ -s $pdf ]] && print "${pdf:t} written" || result=1; done
print "work folder: $work"
exit $result
