import CoreGraphics
import Foundation

let arguments = CommandLine.arguments
var moveOnly = false
var rightButton = false
var scrollAmount: Int32?
var flags: CGEventFlags = []
var numbers: [Double] = []
var doubleClick = false
var index = 1

while index < arguments.count {
    let argument = arguments[index]

    if argument == "--move" { moveOnly = true; index += 1; continue }
    if argument == "--right" { rightButton = true; index += 1; continue }

    if argument == "--double" {
        doubleClick = true
        index += 1
        continue
    }
    if argument == "--mods", index + 1 < arguments.count {
        for name in arguments[index + 1].split(separator: ",") {
            switch name {
            case "cmd": flags.insert(.maskCommand)
            case "shift": flags.insert(.maskShift)
            case "alt", "option": flags.insert(.maskAlternate)
            case "ctrl", "control": flags.insert(.maskControl)
            default: break
            }
        }

        index += 2
        continue
    }

    if argument == "--scroll", index + 1 < arguments.count, let value = Int32(arguments[index + 1]) {
        scrollAmount = value
        index += 2
        continue
    }

    if let value = Double(argument) { numbers.append(value) }
    index += 1
}

guard numbers.count == 2 else {
    print("uso: click x y [--move] [--right] [--scroll n] [--mods cmd,shift]")
    exit(1)
}

let point = CGPoint(x: numbers[0], y: numbers[1])

func post(_ type: CGEventType, button: CGMouseButton = .left, modifiers: CGEventFlags = []) {
    guard let event = CGEvent(
        mouseEventSource: nil, mouseType: type,
        mouseCursorPosition: point, mouseButton: button) else { return }

    if !modifiers.isEmpty { event.flags = modifiers }
    event.post(tap: .cghidEventTap)
}

post(.mouseMoved)

if let amount = scrollAmount {
    usleep(120_000)

    CGEvent(scrollWheelEvent2Source: nil, units: .line, wheelCount: 1, wheel1: amount, wheel2: 0, wheel3: 0)?
        .post(tap: .cghidEventTap)

    print("rotella \(amount) su \(Int(numbers[0])), \(Int(numbers[1]))")
    exit(0)
}

if moveOnly {
    usleep(60_000)
    post(.mouseMoved)
    print("spostato su \(Int(numbers[0])), \(Int(numbers[1]))")
    exit(0)
}

usleep(250_000)

if rightButton {
    post(.rightMouseDown, button: .right)
    usleep(90_000)
    post(.rightMouseUp, button: .right)
    print("clic destro su \(Int(numbers[0])), \(Int(numbers[1]))")
    exit(0)
}

if doubleClick {
    func postClick(_ down: CGEventType, _ up: CGEventType, _ count: Int64) {
        if let e = CGEvent(mouseEventSource: nil, mouseType: down, mouseCursorPosition: point, mouseButton: .left) {
            e.flags = flags; e.setIntegerValueField(.mouseEventClickState, value: count); e.post(tap: .cghidEventTap)
        }
        usleep(40_000)
        if let e = CGEvent(mouseEventSource: nil, mouseType: up, mouseCursorPosition: point, mouseButton: .left) {
            e.flags = flags; e.setIntegerValueField(.mouseEventClickState, value: count); e.post(tap: .cghidEventTap)
        }
    }
    post(.mouseMoved)
    usleep(60_000)
    postClick(.leftMouseDown, .leftMouseUp, 1)
    usleep(60_000)
    postClick(.leftMouseDown, .leftMouseUp, 2)
    print("doppio clic su \(Int(numbers[0])), \(Int(numbers[1]))")
    exit(0)
}

post(.leftMouseDown, modifiers: flags)
usleep(90_000)
post(.leftMouseUp, modifiers: flags)
print("clic su \(Int(numbers[0])), \(Int(numbers[1]))")
