# format-bench

Measures peak memory and open and save times of sheets in ODS and XLSX, and compares two versions of the reader on a list of files.

```bash
dotnet build -c Release
/usr/bin/time -l bin/Release/net10.0/format-bench write ods big.ods 500000
/usr/bin/time -l bin/Release/net10.0/format-bench read ods big.ods
bin/Release/net10.0/format-bench dump ods elenco.txt > riassunti.txt
bin/Release/net10.0/format-bench resave ods originale.ods risalvato.ods
```

- `write`: synthetic sheet of N rows by 10 columns (8 numbers, 2 texts), then save.
- `read`: open and count the cells.
- `dump`: for each file in the list, a summary of the model read (cells, styles, merges, comments, objects, sizes, names); with `DETAIL=1` the full summary.
- `resave`: opens and resaves, to compare the original and ours in another office suite.
- `resaveall FORMAT list.txt folder`: resaves every file in the list (reader chosen by extension) into the folder, in the requested format.

On macOS peak memory is read from `/usr/bin/time -l` ("maximum resident set size"): the .NET counter returns zero.

`write`/`read` also accept `odp`, `pptx`, `odt`, `docx` (N slides or paragraphs). The
`allocated` value is the memory allocated by the write or read alone, without building the model. With
`ALLOC=1` the bench also prints allocations sampled by type (`GCAllocationTick`), the quickest way
to see where they come from without an external profiler:

```bash
ALLOC=1 dotnet bin/Release/net10.0/format-bench.dll write pptx big.pptx 400
```
