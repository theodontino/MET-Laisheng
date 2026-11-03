# UI12 — restore empty-tile actions and gradual specialization

2026-10-25. Existing checkout retained; no player save modified.

## Tile panel regression

UI11 selected-tile rendering threw KeyNotFoundException for `production` when a resource had no receipt row, including normal empty plots. This interrupted all subsequent panel sections and footer creation. Absent receipt fields now contribute zero to the displayed net amount; un-settled state is still labeled pending.

The actual panel renderer was called in isolated Unity edit-mode objects for 122 cases, including empty/occupied/water tiles and editable/read-only access. All constructed the three footer actions, with correct empty-tile button availability. This is an engineering regression check, not a Player visual/input acceptance.

Upgrade progress now explicitly says the original building continues operating at reduced efficiency; new construction says production starts on completion. Existing rules retain 65% upgrade operation factor and zero new-build production before completion. No simulation rule changed for construction operation.

## Specialization

User confirmed reductions should occur by tier, and also include housing and services. T2 missing side abilities now retain half of their T1 base; T3 final definitions stay as previously specialized. Existing already-gradual changes and primary capabilities remain unchanged.

| T2 | Restored secondary base capabilities |
|---|---|
| Residence | food 1.9 |
| Agriculture | housing 12 |
| Public center | medical 6; education 4 |
| Clinic | education 4; entertainment 8; commercial 8 |
| Elementary school | medical 6; entertainment 8; commercial 8 |
| Power station | material 1.6; goods 0.3 |
| Extraction | goods 0.3; energy 1.5 |
| Yard | goods 0.3; energy 1.5 |

145 focused checks passed across actual upgrade completion, previews, service fields/allocation pools, housing relocation, unchanged primary capabilities, and source/bundled-catalog equality. These are functional checks, not balance validation.

The player's current read-only district and its crisis/retirement history were inspected without writes. No reappointment exception or save repair is included without the user's pending answer. Existing autonomous districts remain read-only.

App: `build/ui12-delivery/MET Laisheng UI12.app`.
ZIP: `build/ui12-delivery/MET-Laisheng-0.7.0-ui.12-macOS-universal.zip`.
Release: https://github.com/theodontino/MET-Laisheng/releases/tag/v0.7.0-ui.12
