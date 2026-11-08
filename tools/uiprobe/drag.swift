import CoreGraphics
import Foundation

// Trascinamento sintetico. Come per il clic, il puntatore va **prima** portato sul punto
// di partenza: la pressione viene risolta dove il puntatore si trova, non dove la si
// dichiara.
let a = CommandLine.arguments
guard a.count == 5 || (a.count == 7 && a[5] == "--mods"),
      let x1 = Double(a[1]), let y1 = Double(a[2]),
      let x2 = Double(a[3]), let y2 = Double(a[4]) else {
    print("uso: drag x1 y1 x2 y2 [--mods ctrl,alt]"); exit(1)
}

var flags: CGEventFlags = []
if a.count == 7 {
    for name in a[6].split(separator: ",") {
        switch name {
        case "cmd": flags.insert(.maskCommand)
        case "shift": flags.insert(.maskShift)
        case "alt": flags.insert(.maskAlternate)
        case "ctrl": flags.insert(.maskControl)
        default: break
        }
    }
}

func post(_ type: CGEventType, _ p: CGPoint) {
    let event = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: p, mouseButton: .left)
    if !flags.isEmpty { event?.flags = flags }
    event?.post(tap: .cghidEventTap)
}

post(.mouseMoved, CGPoint(x: x1, y: y1))
usleep(250_000)
post(.leftMouseDown, CGPoint(x: x1, y: y1))
usleep(120_000)

let steps = 20
for i in 1...steps {
    let t = Double(i) / Double(steps)
    post(.leftMouseDragged, CGPoint(x: x1 + (x2 - x1) * t, y: y1 + (y2 - y1) * t))
    usleep(20_000)
}

usleep(120_000)
post(.leftMouseUp, CGPoint(x: x2, y: y2))
print("trascinato da \(Int(x1)),\(Int(y1)) a \(Int(x2)),\(Int(y2))")
