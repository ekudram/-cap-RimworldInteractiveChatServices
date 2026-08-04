# Handoff for Grok Build on Mini (MASIE) — Colony toast messages

**Date:** 2026-08-01  
**From:** Main PC RICS / Capto  
**To:** Grok Build working on **MASIE** mini

---

## What changed (RICS)

Interesting **in-game message bar toasts** (`Messages.Message`) are written next to letters:

```
AI_Commands/events/msg_yyyyMMdd_HHmmss_fff_<id>.json
```

Envelope:

```json
{
  "type": "colony_message",
  "timestamp": "ISO-8601 UTC",
  "messageType": "NegativeHealthEvent",
  "text": "Mia has gotten a bad cut…",
  "message": "Masie, notice on the home colony at (142, 87): Mia has gotten a bad cut…",
  "location": {
    "x": 142,
    "y": 0,
    "z": 87,
    "cell": "(142, 0, 87)",
    "mapId": 0,
    "mapLabel": "the home colony map",
    "isPlayerHome": true
  }
}
```

`location` is present only when RICS resolved a real map cell (lookTargets / pawn position). Prose uses **(x, z)**; full cell is in JSON. Same `location` object is used on `colony_event` (letters). Death batch lines embed `at (x, z)` in text only.

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

## What Masie should do

Same poll folder as `colony_event` (`events/`).

1. If `type == "colony_message"`:
   - Prefer **short** TTS (one sentence) in `/mode rimworld`
   - Or inject into context only if you add a quiet mode
   - **Batch** if ≥3 files arrive within ~10s → one summary
2. Do **not** treat like a full storyteller letter monologue
3. Prompt hint: react to colonists, health, threats; ignore any residual UI fluff
4. Delete/process file after handling (same as events)
5. Log: `[MESSAGE FILE] …`

### Minimal patch if you want ship fast

Map unknown types / `colony_message` through the existing `colony_event` speak path first; refine short/batch later.

```python
# conceptual
if payload.get("type") in ("colony_event", "colony_message"):
    text = payload.get("message") or payload.get("text") or ""
    if payload.get("type") == "colony_message":
        # quieter / shorter path
        queue_short_notice(text)
    else:
        handle_colony_event(text)
```

---

## Verify

1. AI bot enabled in RICS; Forward game messages on.
2. Cause a health/threat toast in-game (not a reject-input click).
3. On share: `dir %MASIE_AI_COMMANDS_DIR%\events\msg_*.json`
4. Masie `/mode rimworld` → short notice; no spam from clicking invalid UI.

Letters (`colony_event`) and gamestate (`latest.json`) unchanged.
