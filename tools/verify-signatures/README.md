# verify-signatures

Asks the reference suite, without a user interface, whether the digital signatures of ODF and OOXML files are valid.

```bash
tools/verify-signatures/verify-signatures.sh signed.odt signed.docx …
```

The script creates a throw-away profile under `$TMPDIR`, writes a macro module into it and runs the reference suite
headless. The module opens each file as a zip storage (the same storage the reference suite uses to sign, for ODF and
OOXML alike) and asks its signature service to verify the document content signatures. One line per file: path,
number of signatures, then for each signature whether it is valid, the certificate status (1 = not trusted, as for a
self-signed certificate), whether it is partial (always true for OOXML) and the signer.

Calibration: the reference suite's own signed test documents give valid and invalid signatures as expected. A storage
opened in package or OOXML mode hides the signature streams and reports zero signatures. `REFERENCE_SUITE` overrides
the path of the reference suite's binary.
