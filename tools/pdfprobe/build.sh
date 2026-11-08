#!/bin/sh
set -e
cd "$(dirname "$0")"
swiftc -O pdftext.swift -o pdftext
