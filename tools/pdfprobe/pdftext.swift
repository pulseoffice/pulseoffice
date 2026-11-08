import Foundation
import PDFKit

let arguments = CommandLine.arguments

guard arguments.count > 1, let document = PDFDocument(url: URL(fileURLWithPath: arguments[1])) else {
    FileHandle.standardError.write("uso: pdftext <file.pdf> [--chars]\n".data(using: .utf8)!)
    exit(2)
}

print("pagine: \(document.pageCount)")

for index in 0..<document.pageCount {
    guard let page = document.page(at: index) else { continue }
    let bounds = page.bounds(for: .mediaBox)
    print("--- pagina \(index + 1) \(Int(bounds.width))x\(Int(bounds.height)) pt")
    print(page.string ?? "")
    if arguments.contains("--chars") {
        let count = page.numberOfCharacters
        for position in 0..<count {
            let bounds = page.characterBounds(at: position)
            let text = page.string.map { ($0 as NSString).substring(with: NSRange(location: position, length: 1)) } ?? ""
            print(String(format: "%@\t%.2f\t%.2f", text, bounds.minX, bounds.minY))
        }
    }
}
