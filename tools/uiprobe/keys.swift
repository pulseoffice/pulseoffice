import Carbon
import CoreGraphics
import Foundation

let args = CommandLine.arguments

func flags(from text: String) -> CGEventFlags {
    var result: CGEventFlags = []

    for name in text.split(separator: ",") {
        switch name.lowercased() {
        case "cmd", "command", "meta": result.insert(.maskCommand)
        case "shift": result.insert(.maskShift)
        case "alt", "option": result.insert(.maskAlternate)
        case "ctrl", "control": result.insert(.maskControl)
        default: break
        }
    }

    return result
}

func post(_ code: CGKeyCode, down: Bool, modifiers: CGEventFlags = []) {
    let event = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: down)
    event?.flags = modifiers
    event?.post(tap: .cghidEventTap)
}

let modifierKeys: [(CGEventFlags, CGKeyCode)] = [
    (.maskCommand, 55), (.maskShift, 56), (.maskAlternate, 58), (.maskControl, 59),
]

func flagsChanged(_ code: CGKeyCode, _ flags: CGEventFlags) {
    let event = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true)
    event?.type = .flagsChanged
    event?.flags = flags
    event?.post(tap: .cghidEventTap)
    usleep(20_000)
}

func press(_ code: CGKeyCode, modifiers: CGEventFlags) {
    var held: CGEventFlags = []
    for (flag, key) in modifierKeys where modifiers.contains(flag) {
        held.insert(flag)
        flagsChanged(key, held)
    }
    post(code, down: true, modifiers: modifiers)
    usleep(50_000)
    post(code, down: false, modifiers: modifiers)
    for (flag, key) in modifierKeys.reversed() where modifiers.contains(flag) {
        held.remove(flag)
        flagsChanged(key, held)
    }
}

func warmUp() {
    let shift: CGKeyCode = 56

    post(shift, down: true)
    usleep(30_000)
    post(shift, down: false)
    usleep(80_000)
}

func type(_ text: String, primeEditor: Bool) {
    usleep(150_000)
    warmUp()

    if primeEditor {
        send("a")
        press(51, modifiers: [])
        usleep(40_000)
    }

    send(text)
}

func layoutKeys() -> [UniChar: (CGKeyCode, Bool)] {
    var table: [UniChar: (CGKeyCode, Bool)] = [:]
    guard let source = TISCopyCurrentKeyboardLayoutInputSource()?.takeRetainedValue(),
          let pointer = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData) else { return table }
    let data = Unmanaged<CFData>.fromOpaque(pointer).takeUnretainedValue() as Data
    data.withUnsafeBytes { raw in
        guard let layout = raw.baseAddress?.assumingMemoryBound(to: UCKeyboardLayout.self) else { return }
        for shifted in [false, true] {
            let state: UInt32 = shifted ? UInt32(shiftKey >> 8) & 0xFF : 0
            for code in 0..<128 {
                var dead: UInt32 = 0
                var length = 0
                var chars = [UniChar](repeating: 0, count: 4)
                let status = UCKeyTranslate(layout, UInt16(code), UInt16(kUCKeyActionDown), state, UInt32(LMGetKbdType()),
                                            OptionBits(kUCKeyTranslateNoDeadKeysBit), &dead, 4, &length, &chars)
                if status == noErr, length == 1, table[chars[0]] == nil { table[chars[0]] = (CGKeyCode(code), shifted) }
            }
        }
    }
    return table
}

let keyTable = layoutKeys()

func send(_ text: String) {
    for scalar in text.unicodeScalars {
        let mapped = scalar.value <= 0xFFFF ? keyTable[UniChar(scalar.value)] : nil
        let code = mapped?.0 ?? 0
        guard let down = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true),
              let up = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false) else { continue }
        if mapped?.1 == true {
            down.flags = .maskShift
            up.flags = .maskShift
        }

        var unit = UniChar(scalar.value)
        down.keyboardSetUnicodeString(stringLength: 1, unicodeString: &unit)
        up.keyboardSetUnicodeString(stringLength: 1, unicodeString: &unit)

        down.post(tap: .cghidEventTap)
        usleep(15_000)
        up.post(tap: .cghidEventTap)
        usleep(35_000)
    }
}

var modifiers: CGEventFlags = []
var primeEditor = false
var positional: [String] = []
var index = 1

while index < args.count {
    if args[index] == "--mods", index + 1 < args.count {
        modifiers = flags(from: args[index + 1])
        index += 2
        continue
    }

    if args[index] == "--prime" {
        primeEditor = true
        index += 1
        continue
    }

    positional.append(args[index])
    index += 1
}

if positional.count == 2, positional[0] == "--key" || positional[0] == "--code",
   let code = UInt16(positional[1]) {
    press(CGKeyCode(code), modifiers: modifiers)
    print("tasto \(code)")
} else if positional.count == 1 {
    type(positional[0], primeEditor: primeEditor)
    print("scritto")
} else {
    print("uso: keys \"testo\" [--prime] | keys --key <codice> [--mods cmd,shift]")
    exit(1)
}
