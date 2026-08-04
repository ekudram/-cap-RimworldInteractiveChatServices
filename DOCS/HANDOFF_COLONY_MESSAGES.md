# Handoff for Grok Build on Mini (MASIE) — Colony toast messages + map slices

**Date:** 2026-08-04 (map slice update)  
**Prior:** 2026-08-01 coords + toasts  
**From:** Main PC RICS / Capto  
**To:** Grok Build working on **MASIE** mini

---

## What changed this session (2026-08-04) — mapSlice

When RICS already has a **real map cell** for a letter, toast, or death batch, it now also attaches a compact **`mapSlice`** so Masie can “see” the area around the event (MAPGRID design from `MAPGRID_grok_report.pdf`).

### New RICS code

| File | Role |
|------|------|
| `AI/AiMapSliceBuilder.cs` | Builds terrain grid + nearby pawns/cover/notable + summary |
| `AI/AIChatBotService.cs` | `NotifyColonyEvent` / `NotifyColonyMessage` write optional `mapSlice` |
| `Harmony/Patch_LetterStack_Notifications.cs` | Letters (slice 20), toasts (12), deaths (12) build slice when cell known |
| `CAPChatInteractive_GameComponent.cs` | Death batch flush includes last death’s `location` + `mapSlice` |

### Slice sizes

| Source | Size |
|--------|------|
| Letter (`colony_event`) | **20** (odd → 21 after clamp) |
| Toast (`colony_message`) | **12** → **13** |
| Death batch | **12** → **13** (from **last** death in the batch) |

### Envelope (toast example)

```json
{
  "type": "colony_message",
  "timestamp": "ISO-8601 UTC",
  "messageType": "NegativeHealthEvent",
  "text": "Mia has gotten a bad cut…",
  "message": "Masie, notice on the home colony at (142, 87): Mia has gotten a bad cut… [Area: 2 player pawns; sandbags present.]",
  "location": {
    "x": 142,
    "y": 0,
    "z": 87,
    "cell": "(142, 0, 87)",
    "mapId": 0,
    "mapLabel": "the home colony map",
    "isPlayerHome": true
  },
  "mapSlice": {
    "center": { "x": 142, "z": 87 },
    "relativeToColony": "18 cells NNE of colony center",
    "directionFromNorth": 22,
    "sliceSize": 13,
    "terrainGrid": {
      "size": 13,
      "northIsTop": true,
      "legend": {
        ".": "Soil", "g": "Gravel", "s": "Sand", "r": "Rock",
        "f": "Floor", "#": "Wall/impassable", "w": "Water", "m": "Mud", "?": "Other"
      },
      "grid": [ "....####.....", "..." ]
    },
    "cover": [
      { "type": "Sandbags", "relX": -3, "relZ": 2, "size": "1x1" }
    ],
    "pawns": [
      {
        "name": "Bob",
        "faction": "Player",
        "status": "Standing",
        "drafted": true,
        "health": "Healthy",
        "weapon": "Assault rifle",
        "relX": -2,
        "relZ": 1,
        "job": "Standing guard"
      }
    ],
    "notableThings": [
      { "type": "Corpse", "label": "Human corpse", "relX": 1, "relZ": 0 }
    ],
    "summary": "2 player pawns (1 drafted); sandbags present."
  }
}
```

### Rules

- `location` / `mapSlice` **only** when a real cell was resolved — never invented.
- Relative coords: `relX/relZ` vs event center; **north = top** of terrain grid (+Z).
- Caps: cover 25, pawns 20, notable 15; summary ~240 chars.
- Prose message may append `[Area: {summary}]` for quick TTS context without reading the whole grid.
- Fail soft: build errors omit `mapSlice` only.

### What Masie should do with mapSlice

1. Prefer **`mapSlice.summary`** + **`pawns`** for spoken reactions (who is nearby, downed, enemies).
2. Use **`relativeToColony`** for direction flavor (“north of base”).
3. Optional: glance at **cover** / **terrainGrid** for raids/fights — **do not** read the full grid aloud.
4. If `mapSlice` missing, behave as before (text + optional `location` only).

### Files path (unchanged)

```
AI_Commands/events/msg_*.json   ← toasts (colony_message)
AI_Commands/events/event_*.json ← letters + death batches (colony_event)
```

---

## Background (2026-08-01) — toasts + coords

Interesting **in-game message bar toasts** (`Messages.Message`) are written next to letters:

```
AI_Commands/events/msg_yyyyMMdd_HHmmss_fff_<id>.json
```

`location` is present only when RICS resolved a real map cell (lookTargets / pawn position). Prose uses **(x, z)**; full cell is in JSON. Same `location` object is used on `colony_event` (letters). Death batch lines embed `at (x, z)` in text; batch JSON can now also carry last death `location` + `mapSlice`.

### Already filtered on RICS (bot can trust)

| Dropped | Why |
|---------|-----|
| `RejectInput`, `CautionInput`, `SilentInput` | Pure UI |
| `PawnDeath` | Existing death batch / letters |
| `TaskCompletion` | Default off (job spam); optional setting |
| Text starting with `[RICS]`, `RICS:`, `Debug`, `[CAP]` | Admin |
| Known admin phrases | coins reset, reconnection, store admin |
| Text containing `following` / `can not follow` | Camera+ / Follow Me camera UI spam |
| Dedupe 4s same text; max ~10/min | Rate limit |

**Forwarded types:** ThreatBig/Small, NegativeHealthEvent, Negative/PositiveEvent, SituationResolved, NeutralEvent (+ TaskCompletion if enabled).

Setting: **Forward interesting game messages** (default on) in RICS AI tab.

---

## What Masie should do (messages)

Same poll folder as `colony_event` (`events/`).

1. If `type == "colony_message"`:
   - Prefer **short** TTS (one sentence) in `/mode rimworld`
   - Or inject into context only if you add a quiet mode
   - **Batch** if ≥3 files arrive within ~10s → one summary
2. Do **not** treat like a full storyteller letter monologue
3. Prompt hint: react to colonists, health, threats; use mapSlice summary when present
4. Delete/process file after handling (same as events)
5. Log: `[MESSAGE FILE] …`

### Minimal patch if you want ship fast

Map unknown types / `colony_message` through the existing `colony_event` speak path first; refine short/batch later.

```python
# conceptual
if payload.get("type") in ("colony_event", "colony_message"):
    text = payload.get("message") or payload.get("text") or ""
    loc = payload.get("location")
    slice_ = payload.get("mapSlice")
    if slice_ and slice_.get("summary"):
        # optional inject: nearby situation
        pass
    if payload.get("type") == "colony_message":
        queue_short_notice(text)
    else:
        handle_colony_event(text)
```

---

## Verify

1. AI bot enabled in RICS; Forward game messages on.
2. Cause a health/threat toast with a look target (colonist).
3. Inspect `events/msg_*.json` → `location` + `mapSlice` (pawns/cover/summary/grid).
4. Raid letter → `event_*.json` with larger `sliceSize`.
5. Death batch → summary text; optional last-death `mapSlice`.
6. Masie `/mode rimworld` → short notice; no grid spam in TTS.

Letters (`colony_event`) and gamestate (`latest.json`) otherwise unchanged.

---

## Design reference

`C:\RimWorldChatbot\Python_3_12\Report PDF Grok\MAPGRID_grok_report.pdf` — full MAPGRID intent (relative coords, terrain chars, cover, pawns, summary).
