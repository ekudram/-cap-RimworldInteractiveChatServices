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
    /// This is the single best central hook for "something happened in the colony".
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

                string notification = $"{botName} this has occurred in the colony: {title}.";
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
    /// Builds intuitive death info: animal vs pawn, faction (for pawns/animals), killer (instigator + damage), map context,
    /// and special note when a player-faction animal is euthanized/slaughtered for meat and fur.
    /// Messages are batched in GameComponent (throttled) for raid detection from volume + home map.
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

                // Map context (keep the phrases for existing batch raid detection logic in GameComponent)
                string mapLabel = "an unknown location";
                string mapContext = "";
                Map pawnMap = pawn.Map;
                if (pawnMap != null && pawnMap.Parent != null)
                {
                    mapLabel = pawnMap.Parent.Label ?? pawnMap.Parent.def.label ?? "a map";
                    mapContext = pawnMap.IsPlayerHome ? " (home colony map)" : " (remote map)";
                }

                // Determine intuitive type: pawn vs animal (per request)
                string entityKind = "Pawn";
                if (pawn.RaceProps != null)
                {
                    if (pawn.RaceProps.Animal)
                        entityKind = "Animal";
                    else if (pawn.RaceProps.Humanlike)
                        entityKind = "Pawn";
                    else
                        entityKind = "Creature";
                }

                // Faction context if present (player or other)
                string factionPart = "";
                if (pawn.Faction != null)
                {
                    if (pawn.Faction == Faction.OfPlayer || pawn.Faction.IsPlayer)
                        factionPart = " (player faction)";
                    else
                        factionPart = $" ({pawn.Faction.Name ?? pawn.Faction.def?.label ?? "unknown faction"})";
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

                // Build intuitive message: context of what died + faction + killer + special slaughter note
                // Examples (what the bot receives in colony_event):
                //   "Animal Muffalo (player faction) was euthanized (slaughtered for meat and fur) by Bob (player faction) (cut) on Map (home colony map)"
                //   "Pawn Alice (player faction) was killed by Raider (hostiles) (bullet) on Map (home colony map)"
                //   "Animal Wolf has died from infection on Remote (remote map)"
                //   "Pawn Enemy (pirates) was killed by Colonist (player faction) (cut) on ... (home colony map)"
                string message = $"{entityKind} {name}{factionPart}{killerDetail} on {mapLabel}{mapContext}{slaughterNote}";

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