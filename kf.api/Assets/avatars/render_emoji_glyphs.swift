// render_emoji_glyphs.swift
//
// Renders the ten emoji glyphs used by the default challenger avatars into
// 64x64 RGBA PNGs with a transparent background.
//
// The Python generator (generate_avatars.py) reads those PNGs from `emoji/`
// and composites them in front of each shield. It stays standard-library only
// — it never needs a font at runtime — so this helper is the offline step
// that produces the glyph bitmaps.
//
// Usage:
//     swiftc -O -o /tmp/render_emoji_glyphs render_emoji_glyphs.swift
//     /tmp/render_emoji_glyphs ./emoji
//
// The output is macOS specific: it uses the system "Apple Color Emoji" font.

import Foundation
import CoreGraphics
import CoreText
import ImageIO
import UniformTypeIdentifiers

let pixelSize = 64

/// One emoji badge: a glyph, or a glyph plus an overlay drawn on top of it.
struct EmojiSpec {
    let name: String
    let glyphs: [(text: String, size: CGFloat, dx: CGFloat, dy: CGFloat)]
}

let specs: [EmojiSpec] = [
    EmojiSpec(name: "knight", glyphs: [("🤺", 58, 0, 0)]),
    EmojiSpec(name: "rose", glyphs: [("🌹", 58, 0, 0)]),
    EmojiSpec(name: "horse", glyphs: [("🐎", 58, 0, 0)]),
    EmojiSpec(name: "sword", glyphs: [("⚔️", 58, 0, 0)]),
    EmojiSpec(name: "helmet", glyphs: [("🪖", 58, 0, 0)]),
    EmojiSpec(name: "castle", glyphs: [("🏰", 58, 0, 0)]),
    EmojiSpec(name: "maiden", glyphs: [("💃", 58, 0, 0)]),
    EmojiSpec(name: "pig", glyphs: [("🐷", 58, 0, 0)]),
    EmojiSpec(name: "cat", glyphs: [("🐱", 58, 0, 0), ("👑", 26, 4, 17)]),
    EmojiSpec(name: "dragon", glyphs: [("🐉", 58, 0, 0)]),
]

func makeContext() -> CGContext? {
    guard let space = CGColorSpace(name: CGColorSpace.sRGB),
          let ctx = CGContext(
              data: nil,
              width: pixelSize,
              height: pixelSize,
              bitsPerComponent: 8,
              bytesPerRow: 0,
              space: space,
              bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue
          )
    else { return nil }
    ctx.clear(CGRect(x: 0, y: 0, width: pixelSize, height: pixelSize))
    return ctx
}

/// Draws one emoji string, centred on `center` by its ink bounds.
func draw(_ text: String, fontSize: CGFloat, center: CGPoint, in ctx: CGContext) {
    let font = CTFontCreateWithName("Apple Color Emoji" as CFString, fontSize, nil)
    let attributes = [kCTFontAttributeName as NSAttributedString.Key: font]
    let attributed = NSAttributedString(string: text, attributes: attributes)
    let line = CTLineCreateWithAttributedString(attributed)
    let bounds = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
    ctx.textPosition = CGPoint(
        x: center.x - bounds.width / 2 - bounds.minX,
        y: center.y - bounds.height / 2 - bounds.minY
    )
    CTLineDraw(line, ctx)
}

func writePNG(_ image: CGImage, to url: URL) {
    guard let dest = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil) else { return }
    CGImageDestinationAddImage(dest, image, nil)
    CGImageDestinationFinalize(dest)
}

let arguments = CommandLine.arguments
guard arguments.count > 1 else {
    FileHandle.standardError.write("usage: render_emoji_glyphs <output-dir>\n".data(using: .utf8)!)
    exit(1)
}

let outputDir = URL(fileURLWithPath: arguments[1])
try? FileManager.default.createDirectory(at: outputDir, withIntermediateDirectories: true)

for spec in specs {
    guard let ctx = makeContext() else { continue }
    let center = CGPoint(x: CGFloat(pixelSize) / 2, y: CGFloat(pixelSize) / 2)
    for glyph in spec.glyphs {
        draw(
            glyph.text,
            fontSize: glyph.size,
            center: CGPoint(x: center.x + glyph.dx, y: center.y + glyph.dy),
            in: ctx
        )
    }
    guard let image = ctx.makeImage() else { continue }
    writePNG(image, to: outputDir.appendingPathComponent("\(spec.name).png"))
    print("wrote \(spec.name).png")
}
