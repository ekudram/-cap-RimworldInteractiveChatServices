// Patch_LetterStack_Notifications.cs
// Copyright (c) Captolamia
// Part of RICS (Rimworld Interactive Chat Services) — AGPLv3

using HarmonyLib;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.AI
{
    /// <summary>
    /// Postfix on LetterStack.ReceiveLetter so we can notify the external AI ChatBot
    /// whenever any letter is shown to the player (storyteller incidents + viewer events).
    /// Now includes rich map context (home colony, gravship raid site, temp event map, etc.)
    /// so the bot has the location information needed to understand the situation.
    /// </summary>
    [HarmonyPatch(typeof(LetterStack))]
    [HarmonyPatch(nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_LetterStack_ReceiveLetter
    {
        [HarmonyPostfix]
        public static void Postfix(Letter let)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.AIChatBotActive)
                    return;

                // Skip letters that shouldn't be shown (safety)
                if (let == null || !let.CanShowInLetterStack)
                    return;

                // Build a clean notification message starting with the bot's name
                string botName = settings.AIChatBotName ?? "Masie";
                string title = let.Label.ToString() ?? let.def?.label ?? "Event";

                string body = "";
                if (let is ChoiceLetter choiceLetter && !choiceLetter.Text.NullOrEmpty())
                {
                    body = ChatCommandProcessor.RemoveMarkupTags(choiceLetter.Text.ToString());
                    // Keep it reasonably short for the bot context window
                    if (body.Length > 2000)
                        body = body.Substring(0, 2000) + "...";
                }

                // Resolve the most relevant map for this letter (lookTargets if available, else current/home)
                Map relevantMap = null;
                try
                {
                    if (let is ChoiceLetter cl && cl.lookTargets != null && !cl.lookTargets.targets.NullOrEmpty())
                    {
                        var primary = cl.lookTargets.TryGetPrimaryTarget();
                        if (primary.IsValid && primary.HasThing && primary.Thing != null && primary.Thing.Map != null)
                            relevantMap = primary.Thing.Map;
                    }
                }
                catch { /* best effort */ }

                if (relevantMap == null)
                    relevantMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;

                string mapDesc = AIChatBotService.GetRichMapDescription(relevantMap);

                // Include rich map context so the bot understands *where* the event is happening
                // (home colony, gravship raid, temp event map, remote site, etc.)
                string notification = $"{botName} this has occurred in the colony on {mapDesc}: {title}.";
                if (!string.IsNullOrWhiteSpace(body))
                    notification += $" {body}";

                // Get the service from the current game (reliable after game start)
                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?._aiChatBotService?.NotifyColonyEvent(notification);
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS AI] Letter notification postfix failed (non-fatal): {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Postfix on Pawn.Kill to catch deaths for the AI bot.
    /// Builds richer death info for the improved AI (Masie):
    /// - Faction attached so bot knows if it's a raiding faction.
    /// - Scaria on animals is reported (Masie understands Scaria as "like Rabies").
    /// - No-faction animals described as "woodland creature".
    /// - Much better map identification (home colony vs event-generated maps from gravship raids etc.).
    /// Messages are batched in GameComponent (throttled) for raid detection.
    /// </summary>
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch(nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_DeathNotifications
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            try
            {
                if (__instance == null || !__instance.Dead)
                    return;

                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.AIChatBotActive)
                    return;

                Pawn pawn = __instance;
                string name = pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown";

                // Use the shared rich map description (centralized in AIChatBotService) for maximum consistency
                // between deaths and general letter events. The bot gets the same rich location phrases.
                Map pawnMap = pawn.Map;
                string mapDesc = AIChatBotService.GetRichMapDescription(pawnMap);

                // Preserve the exact substrings expected by the raid-volume batch detection in GameComponent.
                string mapLabel = "an unknown location";
                string mapContext = "";
                if (pawnMap != null)
                {
                    bool isHome = pawnMap.IsPlayerHome;
                    if (isHome)
                    {
                        mapLabel = "home colony";
                        mapContext = " (home colony map)";
                    }
                    else
                    {
                        mapLabel = mapDesc;
                        mapContext = " (remote/event map)";
                    }
                }

                // Determine intuitive type + better description for the bot (raiding faction, scaria, woodland creature)
                string entityDesc = pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown creature";

                bool isAnimal = pawn.RaceProps?.Animal ?? false;
                bool isHumanlike = pawn.RaceProps?.Humanlike ?? false;

                if (isAnimal)
                {
                    // Check for Scaria (Masie now understands this as "like Rabies")
                    bool hasScaria = false;
                    try
                    {
                        var scariaDef = DefDatabase<HediffDef>.GetNamedSilentFail("Scaria");
                        if (scariaDef != null && pawn.health?.hediffSet?.HasHediff(scariaDef) == true)
                            hasScaria = true;
                    }
                    catch { }

                    if (hasScaria)
                    {
                        entityDesc = $"{entityDesc} (Scaria-infected animal, like Rabies)";
                    }
                    else if (pawn.Faction != null && !pawn.Faction.IsPlayer)
                    {
                        // Animal with a faction (rare, but could be manhunter pack or tamed by hostiles)
                        string f = pawn.Faction.Name ?? pawn.Faction.def?.label ?? "hostile faction";
                        entityDesc = $"{entityDesc} ({f})";
                    }
                    else
                    {
                        // Wild / no faction animal → call it woodland creature so bot understands context
                        entityDesc = $"{entityDesc} (woodland creature)";
                    }
                }
                else if (isHumanlike || (pawn.RaceProps != null))
                {
                    // Humanlike or other creature: attach faction so bot knows raiding faction etc.
                    if (pawn.Faction != null)
                    {
                        if (pawn.Faction.IsPlayer)
                            entityDesc = $"{entityDesc} (player colonist)";
                        else
                        {
                            string f = pawn.Faction.Name ?? pawn.Faction.def?.label ?? "unknown faction";
                            entityDesc = $"{entityDesc} ({f})";
                        }
                    }
                    else
                    {
                        entityDesc = $"{entityDesc} (no faction)";
                    }
                }

                // Killer / cause extraction: prefer instigator (who/what killed it) + damage
                string killerDetail = "";
                Thing instigator = dinfo.HasValue ? dinfo.Value.Instigator : null;
                DamageDef dmgDef = dinfo.HasValue ? dinfo.Value.Def : null;

                if (instigator is Pawn killerPawn)
                {
                    string killerFaction = (killerPawn.Faction == Faction.OfPlayer || (killerPawn.Faction?.IsPlayer ?? false))
                        ? "player faction"
                        : (killerPawn.Faction?.Name ?? killerPawn.Faction?.def?.label ?? "neutral/hostile");
                    killerDetail = $" was killed by {killerPawn.LabelShortCap ?? "a pawn"} ({killerFaction})";
                    if (dmgDef != null)
                        killerDetail += $" ({dmgDef.label})";
                }
                else if (instigator != null)
                {
                    string instName = instigator.LabelShortCap ?? instigator.def?.label ?? "something";
                    killerDetail = $" was killed by {instName}";
                    if (dmgDef != null)
                        killerDetail += $" ({dmgDef.label})";
                }
                else if (exactCulprit != null && exactCulprit.def != null)
                {
                    killerDetail = $" has died from {exactCulprit.def.label}";
                }
                else if (dmgDef != null)
                {
                    killerDetail = $" has died from {dmgDef.label}";
                }
                else
                {
                    killerDetail = " has died from unknown causes";
                }

                // Special intuitive note for euthanized/slaughtered player-faction animals (for meat and fur)
                string slaughterNote = "";
                bool isPlayerFactionAnimal = pawn.IsAnimal &&
                    (pawn.Faction == Faction.OfPlayer || (pawn.Faction?.IsPlayer ?? false));

                if (isPlayerFactionAnimal)
                {
                    bool looksSlaughtered =
                        (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.ExecutionCut) ||
                        (dinfo.HasValue && dmgDef != null &&
                            (dmgDef.defName.IndexOf("Cut", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                             dmgDef.defName.IndexOf("Stab", System.StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (instigator is Pawn ip && ip.Faction == Faction.OfPlayer && ip.RaceProps != null && ip.RaceProps.Humanlike);

                    if (looksSlaughtered)
                    {
                        // Produce clean intuitive phrasing for the special case requested:
                        // note euthanized + player faction animal + slaughtered for meat and fur
                        string killerBy = "";
                        if (instigator is Pawn ip2)
                        {
                            string ipf = (ip2.Faction == Faction.OfPlayer || (ip2.Faction?.IsPlayer ?? false)) ? "player faction" : (ip2.Faction?.Name ?? ip2.Faction?.def?.label ?? "player");
                            killerBy = $" by {ip2.LabelShortCap ?? "a colonist"} ({ipf})";
                            if (dmgDef != null) killerBy += $" ({dmgDef.label})";
                        }
                        killerDetail = $" was euthanized (slaughtered for meat and fur){killerBy}";
                        slaughterNote = "";
                    }
                }

                // Build intuitive message for the AI bot.
                // Examples:
                //   "Muffalo (woodland creature) was killed by Wolf (woodland creature) (bite) on remote raid site (remote/event map)"
                //   "Raider (Pirates) was killed by Colonist (player colonist) (bullet) on home colony (home colony map)"
                //   "Wolf (Scaria-infected animal, like Rabies) has died from infection on a temporary event map (remote/event map)"
                string message = $"{entityDesc}{killerDetail} on {mapLabel}{mapContext}{slaughterNote}";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?.RecordDeath(message);
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS AI] Death notification postfix failed (non-fatal): {ex.Message}");
            }
        }
    }
}