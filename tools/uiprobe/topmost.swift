import AppKit
import CoreGraphics
import Foundation

let arguments = CommandLine.arguments
guard arguments.count >= 4, let pid = Int(arguments[1]), let x = Double(arguments[2]), let y = Double(arguments[3]) else {
    FileHandle.standardError.write("uso: topmost <pid> <x> <y> [--ignore-front]   (coordinate di schermo)\n".data(using: .utf8)!)
    exit(2)
}

let front = NSWorkspace.shared.frontmostApplication?.processIdentifier ?? -1
guard Int(front) == pid || arguments.contains("--ignore-front") else {
    print("in primo piano c'è il pid \(front), non \(pid)")
    exit(3)
}

let windows = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] ?? []
let point = CGPoint(x: x, y: y)
let systemLayers: Set<String> = ["Dock", "Window Server", "SystemUIServer", "Control Center", "Centro di Controllo", "WindowManager"]
let covering = windows.first { window in
    guard let bounds = window[kCGWindowBounds as String] as? [String: Double],
          (window[kCGWindowAlpha as String] as? Double ?? 1) > 0 else { return false }
    if (window[kCGWindowLayer as String] as? Int ?? 0) != 0,
       systemLayers.contains(window[kCGWindowOwnerName as String] as? String ?? "") { return false }
    return CGRect(x: bounds["X"] ?? 0, y: bounds["Y"] ?? 0, width: bounds["Width"] ?? 0, height: bounds["Height"] ?? 0).contains(point)
}
guard let top = covering, (top[kCGWindowOwnerPID as String] as? Int) == pid else {
    print("nel punto \(Int(x)),\(Int(y)) la finestra in cima è di \(covering?[kCGWindowOwnerName as String] as? String ?? "nessuno")")
    exit(4)
}
print("ok: finestra del pid \(pid) in cima in \(Int(x)),\(Int(y))")
