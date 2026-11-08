import CoreGraphics
import Foundation

let arguments = CommandLine.arguments

guard arguments.count >= 3 else {
    FileHandle.standardError.write("uso: windowshot <processo> <file.png>\n".data(using: .utf8)!)
    exit(2)
}

let owner = arguments[1]
let output = arguments[2]
let popup = arguments.contains("--popup")
let pid = arguments.firstIndex(of: "--pid").flatMap { index in index + 1 < arguments.count ? Int(arguments[index + 1]) : nil }

let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
guard let windows = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else {
    FileHandle.standardError.write("elenco delle finestre non disponibile\n".data(using: .utf8)!)
    exit(1)
}

let candidates = windows.filter { window in
    (window[kCGWindowOwnerName as String] as? String) == owner
        && (pid == nil || (window[kCGWindowOwnerPID as String] as? Int) == pid)
        && (popup ? (window[kCGWindowLayer as String] as? Int ?? 0) > 0 : (window[kCGWindowLayer as String] as? Int) == 0)
}

let largest = candidates.max { left, right in
    func area(_ window: [String: Any]) -> Double {
        guard let bounds = window[kCGWindowBounds as String] as? [String: Double] else { return 0 }
        return (bounds["Width"] ?? 0) * (bounds["Height"] ?? 0)
    }
    return area(left) < area(right)
}

guard let target = largest, let number = target[kCGWindowNumber as String] as? Int else {
    FileHandle.standardError.write("nessuna finestra di \(owner)\n".data(using: .utf8)!)
    exit(1)
}

let capture = Process()
capture.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
capture.arguments = ["-x", "-o", "-l\(number)", output]
try capture.run()
capture.waitUntilExit()
exit(capture.terminationStatus)
