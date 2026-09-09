Pawn Race Settings

C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\[CAP] Chat Interactive\Source\[CAP] Chat Interactive\Windows\Dialogs\Dialog_PawnRaceSettings.cs

1. Put the Xenotypes that are from Biotech at the top
2. Sort Xenotypes by mod source

example

Biotech Xenotypes:
- Xenotype A
- Xenotype B

Mod 1 Xenotypes:
- Xenotype C
- Xenotype D

Mod 2 Xenotypes:
- Xenotype E
- Xenotype F

3. Make sure we have enough padding in the scroll window.
	a. Add extra space at the bottom of the list to prevent the last item from being cut off.|
	b. We have a streamer that added what looked like more then 50 xenotypes to the list and the last 2 or so were cut off.  We need to make sure that doesn't happen again.


Pricecheck Command

C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\[CAP] Chat Interactive\Source\[CAP] Chat Interactive\Command\CommandHandlers\CommandHandlerPriceCheck.cs

Does this:
Captolamia: !pricecheck
LittleBattousai: @captolamia Command error. Check the game log.

SHould do this:
Captolamia: !pricecheck
LittleBattousai: @captolamia please enter an item name to check the price for. Example: !pricecheck steel







UPdate history.cs

Starting coins max is now 100000 from 10000.

Keep it non technical and simple to understand for the average user.

add the above fixes as we make them and,

we did this fix earlier but was not added to the update history so adding it now

ref:

https://github.com/ekudram/-cap-RimworldInteractiveChatServices/commit/f4c04a3850edd4823d036f49ae8e75430a4c4187

Mod: RICS for RimWorld 1.6
Issue: First !pawn human could NRE in Head/beard render init (story.headType / bodyType null), leave an invisible pawn, and paint garbage pixels on the map edge every frame.
File: Command/CommandHelpers/ItemDeliveryHelper.cs

What changed

All living pawns that go through TryDeliverGeneratedPawn (!pawn, rescue, store animals that use that path) now run EnsureHumanlikeRenderReady before drop pod / GenSpawn / letter portrait.

That helper:

Fills missing bodyType, headType, and hairDef the same way vanilla does on save load (TryGetRandomHeadFromSet, gender/life-stage body).
If a head still cannot be assigned, sets beard to NoBeard so GetHumanlikeBeardSetForPawn is not called.
Replaces PawnRenderer.renderTree and calls EnsureGraphicsInitialized(). A failed Head-node ctor leaves children[i] == null; RecacheRequested then NREs every MapUpdate. Dirtying the old tree is not enough.

If the first rebuild still throws, it strips the beard and tries once more.


also add this to the update history as well:

https://github.com/ekudram/-cap-RimworldInteractiveChatServices/commit/262d800488179518b2fb1ed1ad9807f0faafd3a9

Karma decay defualt is now 0
Max starting cap changed to 100000 from 10000
