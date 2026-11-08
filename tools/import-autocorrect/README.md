# import-autocorrect

Converts the autocorrect lists found in the external reference clone into one JSON per language, in the
`pulseoffice.autocorrect` format, inside `src/PulseOffice.Text.Spelling/Data/autocorrect/`.

```bash
python3 tools/import-autocorrect/import-autocorrect.py --source <reference clone>
```

The script regenerates all the JSON files, copies the MPL 2.0 licence to `LICENSE-MPL-2.0.txt` and writes `NOTICE`
with the provenance. Entries are not filtered: normalisation (`.*` wildcards, spaces) is done by the application's
reader, the same for these JSON files and for installed autocorrect files.

The data is under MPL 2.0 (see `NOTICE`); the script is ours.
