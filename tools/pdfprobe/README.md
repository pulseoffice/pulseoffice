# pdfprobe — really reading the PDFs we produce

`pdftext` opens a PDF with **PDFKit**, that is with the system's own reader, and prints its
page count, sizes and text. It is needed because our tests only re-read what we
write ourselves: a file that passes our checks may well be rejected by a real reader.

```bash
cd tools/pdfprobe && ./build.sh
./pdftext /tmp/sample.pdf
```

With `--chars` it also prints every character with its x and y in points (origin at bottom left), to check that positions do not drift.

To look at the page instead of reading it:

```bash
qlmanage -t -s 900 -o /tmp file.pdf
```

macOS only. On Windows and Linux `pdftotext` or `mutool` will be needed.
