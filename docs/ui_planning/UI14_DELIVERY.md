# UI14 — MET Civic icon library

2026-10-25. User requested attractive reusable icons after UI13 screenshots showed flattened HUD glyphs. This iteration creates a source-based original icon library and connects it to the existing native Unity UI. No gameplay, RNG, save schema or player state is changed.

## Assets and integration

- 52 original SVGs with stable semantic IDs, Chinese names and compatibility aliases. Common resources, navigation, services, environment, construction, tasks/Bonus and controls are covered.
- One 32-unit grid, 1.9-unit rounded stroke and translucent fill. White transparent PNGs at 32 and 128 pixels take the host interface's existing colors.
- Editable sources, offline searchable gallery, preview sheet, usage/provenance notes and export tool live in `assets/ui/icons/met-civic-v1/` and `tools/build_city_icons.mjs`.
- Unity loads and caches the 128-pixel `Resources/CityIcons/` textures through the generated `CityIconCatalog` and existing `CityHudGlyph` API. Default texture import is transparent, uncompressed, bilinear/clamp, without mipmaps.
- The concrete stretching defect had two causes: glyph geometry used the full nonsquare host bounds, and operating headings forced their icon children to take extra width. Glyphs now draw inside a centered square; heading icon width is fixed and labels receive the spare width. Panel frames keep their full-rect geometry.
- Housing, employment and construction now use distinct symbols; Archives uses the archive symbol. The fiscal currency caption is corrected to 通货. Page meter icons use a readable 24-unit size, scaled with text.

The set is original SVG construction, not an image-model generation or copied CK/Vic artwork. LicenseRef-MET-Project-Original applies; it is a reusable MET project asset pack, not declared CC0.

## Engineering evidence

Actual Unity 6000.6.5f1 imported the 52 textures; 1,304 focused checks passed for catalog selection, transparent import settings, UV direction and centered square geometry in 24×24, 360×24, 24×180, 32×32 and 80×80 hosts. The first run exposed only an ambiguous reflection overload in the check script; selecting VertexHelper explicitly corrected it.

The existing V3 workspace suite passed 185 actual page renders and 4,040 assertions across UI 80/100/120% and text 100/125/150%. There were no missing-icon warnings. The known Unity Editor SearchDatabase exception is retained in the raw log and explicitly separated from game assertions.

The source artwork preview was inspected, and PNG alpha bounds were checked to prevent edge clipping. These are asset/engineering checks, not a new Player screenshot or real mouse/keyboard acceptance. As requested, visual and interaction acceptance remains with the user. Default fullscreen stays 2560×1440.

## Delivery

- Icon library: `build/ui14-delivery/MET-Civic-Icons-v1.zip`
- Application: `build/ui14-delivery/MET Laisheng UI14.app`
- Application package: `build/ui14-delivery/MET-Laisheng-0.7.0-ui.14-macOS-universal.zip`
- Version: `v0.7.0-ui.14`, independent source snapshot based on UI13; the branch and existing unstaged work are preserved.
- Native build and package results are recorded separately in `evidence/ui14/`. No Developer ID identity or notarization is claimed.
