# Deep audit, round 24: digital signatures and encryption

Scope: ODF signatures (XAdES, manifest), OOXML package signatures, PDF signatures, RFC 3161 timestamps
(decision 22), macro signatures, sheet and document password hashes. ODF encryption (AES/Blowfish, PBKDF2/Argon2)
was audited in [package.md](package.md) and not repeated. OOXML encryption (Agile/Standard, `EncryptedPackage`)
**does not exist** in the code base: nothing to audit; encrypted OOXML files are not opened.

Tests: `tests/PulseOffice.Security.Tests/Round24CryptoTests.cs` (each test verified failing before its fix).
Project run alone: 72/72 passing.

## Coverage

| File | Lines read | Outcome |
|---|---|---|
| `src/PulseOffice.Security/XmlSignatures.cs` | 350/350 | **medium** fixed (D2) |
| `src/PulseOffice.Security/PackageSigning.cs` | 267/267 | no defect (entry cap 512 MiB, atomic rewrite) |
| `src/PulseOffice.Security/OoxmlSigning.cs` | 760/760 | **grave** fixed (D1) |
| `src/PulseOffice.Security/OpcPackage.cs` | 190/190 | no defect of its own |
| `src/PulseOffice.Security/CertificateJudge.cs` | 43/43 | decision 22 respected; limits L1, L2 |
| `src/PulseOffice.Security/TrustedTimestamps.cs` | 124/124 | no defect (token tied to the canonical `SignatureValue`, future dates rejected, TSA chain judged at the stamp date) |
| `src/PulseOffice.Security/Signatures.cs` | 95/95 | no defect |
| `src/PulseOffice.Security/MacroSigning.cs` | 103/103 | no defect (length-prefixed fingerprint, no ambiguity) |
| `src/PulseOffice.Security/PdfSigning.cs` | 452/452 | **medium** fixed (D3) |
| `src/PulseOffice.Sheets/PasswordKeys.cs` | 147/147 | no defect (spin count capped at 10 000 000, fixed-time comparison for the agile hash) |
| `src/PulseOffice.Formats.OpenXml/Spreadsheet/OoxmlProtectionKeys.cs` | 30/30 | no defect (cap applied when matching) |
| `src/PulseOffice.Documents/Protection/DocumentProtection.cs` | 100/100 | no defect (cap, `FixedTimeEquals`) |
| `src/PulseOffice.Formats.OpenXml/Wordprocessing/DocxProtectionCodec.cs` | 64/64 | no defect |

## Defects

**D1 — grave — `OoxmlSigning.cs` `MatchesPart` (≈ line 390).** The `?ContentType=` of each manifest reference was
never compared with the part's actual content type. `[Content_Types].xml` is unsigned by the OPC rules, so changing a
signed part's content type (e.g. the main document from `document.main` to `template.main`, or any part to another
type) left the signature intact. Fixed: the declared content type must equal `IOpcParts.ContentTypeOf` (case-insensitive),
otherwise the reference does not match. Test `Changing_the_content_type_of_a_signed_ooxml_part_breaks_the_signature`.

**D2 — medium — `XmlSignatures.cs` `Covered` (≈ line 193).** Everything under `META-INF/` was exempt from coverage,
including `META-INF/manifest.xml`, which carries media types and encryption data. A signature that does not cover the
manifest (from another producer, or after the manifest was added) was accepted. Fixed: when the package has a manifest,
it must be among the signed references (as the reference suite requires for ODF 1.2). Test
`An_odf_signature_that_does_not_cover_the_manifest_is_broken`.

**D3 — medium — `PdfSigning.cs` `Read` (≈ line 96).** An earlier signature that does not reach the end of the file was
reported with its own status whenever *any* later signature covering the whole file was not broken. Anyone could append
an incremental update with arbitrary changes, sign it with a self-signed certificate, and the earlier trusted signature
was still shown as valid. Fixed with `SignatureStrength`: an earlier signature is reported at the weaker of its own status
and the strongest whole-file signature. Test `A_later_untrusted_pdf_signature_does_not_vouch_for_changes_after_a_trusted_one`.

## Declared limits (not changed)

- **L1 Revocation**: `ChainCertificateTrust` uses `X509RevocationMode.Offline` and ignores unknown revocation; only
  cached CRLs count. Going online at every opening is a privacy and latency choice for the user to make.
- **L2 Intermediates**: ODF, OOXML and PDF verification pass only the signer certificate to the chain; intermediate
  certificates carried in `KeyInfo` or in the CMS are not offered as `ExtraStore`, so a chain needing them comes out
  `UntrustedCertificate` (false negative, never a false positive).
- **L3 SHA-1**: digests and `rsa-sha1` signatures are still accepted on reading, for old documents; signing always uses SHA-256.
- **L4** Word's `documentProtection` hash is computed directly over the UTF-16 password, without Word's legacy
  pre-hash: compatibility issue, not a security one.
- Comparisons of digests in signatures are not fixed-time; they compare public values, so timing leaks nothing.

## Live checks

- Open a signed DOCX in Word, change the content type of a part by hand, reopen in PulseOffice: the signature must be broken.
- Sign a PDF, re-sign it with a self-signed certificate: the first signature must no longer show as valid.
