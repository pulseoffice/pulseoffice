#!/usr/bin/env python3
import argparse
import json
import shutil
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

BLOCK = "{http://openoffice.org/2001/block-list}"
FORMAT = "pulseoffice.autocorrect"
VERSION = 1


def blocks(path):
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8")
    if "<!DOCTYPE" in text or "<!ENTITY" in text:
        raise ValueError(f"{path}: DTDs are not accepted")
    root = ElementTree.fromstring(text)
    return [element for element in root.iter(BLOCK + "block")]


def words(path):
    seen = []
    for element in blocks(path):
        word = (element.get(BLOCK + "abbreviated-name") or "").strip()
        if word and word not in seen:
            seen.append(word)
    return seen


def replacements(path):
    result = {}
    for element in blocks(path):
        shortcut = element.get(BLOCK + "abbreviated-name")
        value = element.get(BLOCK + "name")
        if shortcut and value is not None and shortcut not in result:
            result[shortcut] = value
    return result


def convert(folder, tag):
    return {
        "format": FORMAT,
        "version": VERSION,
        "language": tag,
        "source": f"LibreOffice extras/source/autocorr/lang/{tag}",
        "license": "MPL-2.0",
        "replacements": replacements(folder / "DocumentList.xml"),
        "sentenceExceptions": words(folder / "SentenceExceptList.xml"),
        "wordExceptions": words(folder / "WordExceptList.xml"),
    }


NOTICE = """PulseOffice autocorrect lists
============================

The files <language>.json in this folder are derived from LibreOffice,
https://www.libreoffice.org, path extras/source/autocorr/lang/<language>/
(DocumentList.xml, SentenceExceptList.xml, WordExceptList.xml).

They are distributed under the Mozilla Public License, version 2.0; the full
text is in LICENSE-MPL-2.0.txt next to this notice. The source form of these
files is the LibreOffice repository named above.

Modifications: the block-list XML of each language was converted, without
changing entries, into one JSON document per language with the keys
"replacements", "sentenceExceptions" and "wordExceptions". The conversion is
reproducible with tools/import-autocorrect/import-autocorrect.py.
"""


def main():
    parser = argparse.ArgumentParser(description="Convert LibreOffice autocorrect lists to PulseOffice JSON.")
    parser.add_argument("--source", default="/Volumes/Dati/develop/libreoffice-ref")
    parser.add_argument("--target", default=str(Path(__file__).resolve().parents[2] / "src/PulseOffice.Text.Spelling/Data/autocorrect"))
    arguments = parser.parse_args()

    source = Path(arguments.source)
    languages = source / "extras/source/autocorr/lang"
    target = Path(arguments.target)
    if not languages.is_dir():
        sys.exit(f"Not found: {languages}")

    target.mkdir(parents=True, exist_ok=True)
    for old in target.glob("*.json"):
        old.unlink()

    count = 0
    for folder in sorted(path for path in languages.iterdir() if path.is_dir()):
        if not (folder / "DocumentList.xml").exists():
            continue
        document = convert(folder, folder.name)
        (target / f"{folder.name}.json").write_text(
            json.dumps(document, ensure_ascii=False, indent=1, sort_keys=False) + "\n", encoding="utf-8")
        count += 1

    shutil.copyfile(source / "COPYING.MPL", target / "LICENSE-MPL-2.0.txt")
    (target / "NOTICE").write_text(NOTICE, encoding="utf-8")
    print(f"Converted {count} languages into {target}")


if __name__ == "__main__":
    main()
