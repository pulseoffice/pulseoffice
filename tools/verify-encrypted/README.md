# verify-encrypted

Checks that the reference suite opens the password-encrypted ODS and ODP files written by PulseOffice.

```bash
tools/verify-encrypted/verify-encrypted.sh
```

1. `EncryptedSampleExport` (test in `PulseOffice.Formats.Odf.Tests`) writes `encrypted.ods` and `encrypted.odp`
   (AES-256-CBC, PBKDF2) into the folder named by `PULSEOFFICE_ENCRYPTED_SAMPLES`.
2. The reference suite, run headless with a temporary profile, executes `verify_encrypted.py`: it loads each file with
   the password and stores it in plain ODF and in PDF.
3. The script checks that the plain copies contain the text written by PulseOffice.

`REFERENCE_SUITE` overrides the path of the reference suite's binary. Files encrypted for OpenPGP recipients cannot be
checked this way: the reference suite decrypts them only through GnuPG and the user's keyring.
