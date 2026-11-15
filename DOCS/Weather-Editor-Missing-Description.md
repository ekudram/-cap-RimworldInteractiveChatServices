# Weather editor crash: missing description

The weather editor dies on one row. The title is drawn, then `DrawWeatherInfo` reads `weather.Description.Length`. That description is null, so RimWorld reports:

`Exception filling window for CAP_ChatInteractive.Dialog_WeatherEditor: System.NullReferenceException`

The throw happens inside `BeginScrollView`. `EndScrollView` never runs. Later frames then log "Mouse position stack is not empty" and "more calls to BeginScrollView than EndScrollView". Those lines are the side effect of the same throw.

Chat buys from the same list, so a broken row could also be purchased.

## The two defs

Both are indoor map skies. Each `WeatherDef` has a label and no `<description>`. The matching `GameConditionDef` has the description, and points at the weather. RICS only reads `WeatherDef`, so the description never arrives.

Vanilla Quests Expanded - Deadlife, workshop `3497226454`.

File: `1.6/Defs/Quests/AncientSiloMapGenerator.xml`

- Weather def: `VQE_AncientSilo`
- Label: ancient silo
- No description
- `canOccurAsRandomForcedEvent` is false
- Game condition `VQE_AncientSiloUnderground` says "It's dark and damp here."

Vanilla Quests Expanded - Ancients, workshop `3618306875`. This is the row in the stack trace.

File: `1.6/Defs/Quests/AncientComplexMapGenerator.xml`

- Weather def: `VQEA_AncientComplex`
- Label: ancient complex
- No description
- `canOccurAsRandomForcedEvent` is false
- Game condition `VQEA_AncientComplex` says "It's dark and damp here."

The public Deadlife GitHub tree does not have a file named Weather. The weather def sits in the map-generator XML, which is why a search for a weather file misses it.

## What RICS does now

A weather with no def name, label, or description is not added to the buyable list. A weather is also left off when a game condition uses it, allows underground, and hides that condition from the UI. That is the ancient silo and ancient complex pattern. `allowUnderground` defaults to true, so that flag alone is not enough. Blood rain, the palls, and the Royalty weather controller stay. A saved row keeps its price and enabled setting. The log is a warning and names the def, the label, and the mod. The editor also treats a null description as blank and catches one bad row so the scroll view can finish.
