# LeviShare leaf and share icon

Created on 2026-10-28 with the built-in image_gen tool. An original two-leaf sprig is combined with a white three-node share symbol on an emerald badge. This is a LeviShare-specific icon, not the unmodified official LeviMC icon.

- `levishare-icon-source.png`: full-resolution generated master. Keep this file when updating the icon.
- `levishare.png`: 256px application header image.
- `levishare.ico`: 16, 24, 32, 48, 64, 128 and 256px Windows entries.
- Rebuild: `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1` from the repository root. The script resizes the saved master; it does not draw the old pixel crate.

The existing GUI and executable already reference `levishare.png` and `levishare.ico`.

## Final generation prompt

Use case: logo-brand. Generate a NEW final Windows desktop app icon for LeviShare. A completely opaque, edge-to-edge flat warm ivory square background, no transparency, no rounded outer tile, no border, no frame. Center a minimal two-leaf sprig: one tall pointed leaf curving up to the upper-right, one smaller pointed leaf projecting to the left, joined by a curved stem going down; thick deep forest-green outer silhouette, flat sage-green leaf fills and one ivory negative-space vein per leaf. At the lower-right of the sprig, overlap the lower stem with an emerald-green circular badge outlined with a modest ivory ring. Inside this badge draw the standard white SHARE symbol, exactly three white circles connected by two white diagonal lines, one circle at left center and the other two at right top and right bottom. Original elegant botanical software logo, immediately readable in a 32px taskbar icon. Sprig uses about 80 percent of canvas height with safe margins, badge about one third of canvas width. Clean flat vector-like raster illustration, hard precise smooth shapes, uniform solid fills only, no gradients, shading, texture, effects or noise. Single square icon, no text, no mockup, no presentation sheet, no glow, no shadow. Background must fill every pixel to all four square edges in solid ivory.
