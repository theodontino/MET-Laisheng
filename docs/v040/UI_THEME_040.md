> 此文记录上一轮 Visual 版本。当前源码与新包已更新为来生版，参见 [来生版说明](LAISHENG_041.md) 和 [本轮验收](LAISHENG_ACCEPTANCE_041.md)；下文旧结果不代表本轮验证。

# MET 0.4 UI theme

The interface uses deep navy surfaces with restrained antique-gold accents over the existing bright city art. `content/presentation040/ui_tokens.json` is the shared theme specification. `ui/widgets.gd` reads font family/weights, body/title/metric/button sizes and desktop/touch button minimum heights; `ui/components031/design.gd` reads semantic surfaces, domains, quantities, statuses, slots and icon aliases. The public `W` palette constants remain fixed API values and currently match the corresponding surface tokens.

## Palette

| Role | Color |
| --- | --- |
| Base / canvas | `#0B1622` |
| Surface / panel | `#142437` |
| Raised card / control | `#223549` |
| Main text | `#F1E8D8` |
| Muted text | `#B8B7AF` |
| Accent / focus | `#C6A56B` |
| Warning | `#E0B56D` |
| Danger | `#E58C83` |
| Divider | `#34495B` |
| Outline | `#52697A` |

The body font is 16 logical pixels; panel titles use 24, section titles 22 and large metrics 28. Text scale remains 100%, 125% or 150%. Shared buttons use 40 logical pixels minimum height on desktop and 48 in touch mode.

`ui/components031/design.gd` continues to load the 0.3.1 card presentation catalog and current 0.4 building domains, while taking semantic surfaces, domain colors, quantity colors, statuses and icon aliases from the 0.4 token file. Typography is read by the shared `W` helpers. The former `type` key is replaced with `typography`, which matches the component reader.

## Shared API

`W` is `ui/widgets.gd`. New semantic constants are `BASE`, `SURFACE`, `RAISED`, `TEXT`, `MUTED`, `ACCENT`, `WARNING`, `DANGER`, `SUCCESS`, `INFO`, `OUTLINE`, `DIVIDER` and `FOCUS`. `W.semantic_color(name)` resolves common semantic names and `W.surface_color(name)` resolves the surface roles in the token file. `W.ink_color(color)`, `W.fill_color(color)` and `W.outline_color(color)` adapt the known 0.3.x palette where old call sites still pass explicit colors.

Existing aliases remain available: `PANEL` means `SURFACE`, `SOFT` means `RAISED`, `GOLD` means `WARNING`, `GREEN` means `SUCCESS` and `RED` means `DANGER`. `W.button()` produces the secondary control; wrap it with `W.primary()` for the main action or `W.ghost()` for a quiet action. `W.flat()` stays as a ghost-style compatibility alias. Buttons expose distinct hover, pressed, focus and disabled states. Panels default to no border; selected cards and keyboard focus keep a clear accent outline.

Use the semantic color helpers in new UI. The legacy mapping is applied only inside shared box and label helpers; map art and world rendering colors remain separate.

## Font and icons

The bundled variable font is Noto Sans SC from [Google Fonts](https://github.com/google/fonts/tree/main/ofl/notosanssc), with the matching SIL Open Font License 1.1 at `assets/fonts/OFL-NotoSansSC.txt`. It includes the OpenType `wght` axis; Godot resolves that axis by its canonical name `weight`, so regular uses 400 and semibold uses 600. No macOS system font is copied into the package.

Function icons live at `assets/ui040/{id}.svg`. Monochrome icons use a 32×32 view box, 1.8-pixel rounded strokes and the legacy `#75D7BE` tint marker. A loader may replace that marker to tint the same icon for a domain or interaction state. Existing 0.3.x IDs are retained; `info`, `search`, `plus`, `minus`, `refresh`, `external`, `sort` and `compare` are added. `org_*` emblems retain their individual color fields. `tools/make_ui040.py` regenerates this set from the existing authored SVGs. The token file's motion timings and several layout/icon drawing fields are descriptive design values today; no shared component animation reads them. The current reduced-motion preference is applied to map particles and completion flashes.
