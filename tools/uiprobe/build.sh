#!/bin/sh
# Compila gli strumenti. Richiede gli strumenti a riga di comando di Xcode.
set -e
cd "$(dirname "$0")"
for tool in click drag keys windowshot topmost; do
    swiftc -O "$tool.swift" -o "$tool"
    echo "compilato: $tool"
done
