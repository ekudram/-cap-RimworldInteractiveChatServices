// Patch_LetterStack_Notifications.cs
// Copyright (c) Captolamia
// Part of RICS (Rimworld Interactive Chat Services) — AGPLv3

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.AI
{
    /// <summary>
    /// Postfix on LetterStack.ReceiveLetter so we can notify the external AI ChatBot
    /// whenever any letter is shown to the player (storyteller incidents + viewer events).
    /// Includes rich map context and involved faction when resolvable (raids, caravans, etc.).
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

                if (let == null || !let.CanShowInLetterStack)
                    return;

                string botName = settings.AIChatBotName ?? "Masie";
                string title = let.Label.ToString() ?? let.def?.label ?? "Event";

                string body = "";
                if (let is ChoiceLetter choiceLetter && !choiceLetter.Text.NullOrEmpty())
                {
                    body = ChatCommandProcessor.RemoveMarkupTags(choiceLetter.Text.ToString());
                    if (body.Length > 2000)
                        body = body.Substring(0, 2000) + "...";
                }

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
                string factionNote = BuildInvolvedFactionNote(let);

                string notification = $"{botName} this has occurred in the colony on {mapDesc}: {title}.";
                if (!string.IsNullOrWhiteSpace(factionNote))
                    notification += $" {factionNote}";
                if (!string.IsNullOrWhiteSpace(body))
                    notification += $" {body}";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?._aiChatBotService?.NotifyColonyEvent(notification);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Letter notification postfix failed (non-fatal): {ex.Message}");
            }
        }

        /// <summary>
        /// Resolve faction(s) from letter look targets (and ChoiceLetter relatedFaction if present)
        /// for raids, friendly raids, caravans, etc.
        /// </summary>
        private static string BuildInvolvedFactionNote(Letter let)
        {
            try
            {
                var factions = new List<Faction>();

                // ChoiceLetter.relatedFaction via reflection-safe pattern (field may exist in 1.6)
                if (let is ChoiceLetter cl)
                {
                    try
                    {
                        var fi = typeof(ChoiceLetter).GetField("relatedFaction");
                        if (fi?.GetValue(cl) is Faction rf && rf != null && !factions.Contains(rf))
                            factions.Add(rf);
                    }
                    catch { /* no relatedFaction field */ }

                    if (cl.lookTargets != null && !cl.lookTargets.targets.NullOrEmpty())
                    {
                        foreach (var t in cl.lookTargets.targets)
                        {
                            if (!t.IsValid || !t.HasThing || t.Thing == null) continue;
                            Faction f = t.Thing.Faction;
                            if (f != null && !factions.Contains(f))
                                factions.Add(f);
                        }
                    }
                }

                // Prefer non-player factions for the note (raiders / traders)
                var notable = factions
                    .Where(f => f != null && !f.IsPlayer && f != Faction.OfPlayer)
                    .Distinct()
                    .Take(3)
                    .ToList();

                if (notable.Count == 0)
                    return null;

                var parts = notable.Select(f =>
                {
                    string name = f.Name ?? f.def?.label ?? "unknown faction";
                    string stance = DescribeFactionStance(f);
                    return $"{name} ({stance})";
                });

                return "Involved faction: " + string.Join("; ", parts) + ".";
            }
            catch
            {
                return null;
            }
        }

        private static string DescribeFactionStance(Faction f)
        {
            try
            {
                if (f == null) return "unknown";
                if (f.HostileTo(Faction.OfPlayer)) return "hostile";
                if (f.PlayerRelationKind == FactionRelationKind.Ally) return "friendly/ally";
                if (f.PlayerRelationKind == FactionRelationKind.Neutral) return "neutral";
                return f.PlayerRelationKind.ToString().ToLowerInvariant();
            }
            catch
            {
                return "unknown";
            }
        }
    }

    /// <summary>
    /// Postfix on Pawn.Kill to catch deaths for the AI bot.
    /// Gender, role (free colonist / colonist / slave / prisoner), origin faction,
    /// killer, map context. Batched in GameComponent.
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

                Map pawnMap = pawn.MapHeld ?? pawn.Map;
                string mapDesc = AIChatBotService.GetRichMapDescription(pawnMap);

                // Preserve substrings expected by raid-volume batch detection in GameComponent.
                string mapLabel = "an unknown location";
                string mapContext = "";
                if (pawnMap != null)
                {
                    if (pawnMap.IsPlayerHome)
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

                string entityDesc = BuildDeathEntityDescription(pawn, name);

                // Killer / cause
                string killerDetail = "";
                Thing instigator = dinfo.HasValue ? dinfo.Value.Instigator : null;
                DamageDef dmgDef = dinfo.HasValue ? dinfo.Value.Def : null;

                if (instigator is Pawn killerPawn)
                {
                    string killerWho = BuildDeathEntityDescription(killerPawn,
                        killerPawn.LabelShortCap ?? killerPawn.Name?.ToStringShort ?? "a pawn");
                    killerDetail = $" was killed by {killerWho}";
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

                // Slaughter note for player animals
                bool isPlayerFactionAnimal = pawn.IsAnimal &&
                    (pawn.Faction == Faction.OfPlayer || (pawn.Faction?.IsPlayer ?? false));

                if (isPlayerFactionAnimal)
                {
                    bool looksSlaughtered =
                        (dinfo.HasValue && dinfo.Value.Def == DamageDefOf.ExecutionCut) ||
                        (dinfo.HasValue && dmgDef != null &&
                            (dmgDef.defName.IndexOf("Cut", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             dmgDef.defName.IndexOf("Stab", StringComparison.OrdinalIgnoreCase) >= 0)) ||
                        (instigator is Pawn ip && ip.Faction == Faction.OfPlayer &&
                         ip.RaceProps != null && ip.RaceProps.Humanlike);

                    if (looksSlaughtered)
                    {
                        string killerBy = "";
                        if (instigator is Pawn ip2)
                        {
                            string ipf = (ip2.Faction == Faction.OfPlayer || (ip2.Faction?.IsPlayer ?? false))
                                ? "player faction"
                                : (ip2.Faction?.Name ?? ip2.Faction?.def?.label ?? "player");
                            killerBy = $" by {ip2.LabelShortCap ?? "a colonist"} ({ipf})";
                            if (dmgDef != null) killerBy += $" ({dmgDef.label})";
                        }
                        killerDetail = $" was euthanized (slaughtered for meat and fur){killerBy}";
                    }
                }

                string message = $"{entityDesc}{killerDetail} on {mapLabel}{mapContext}";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?.RecordDeath(message);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Death notification postfix failed (non-fatal): {ex.Message}");
            }
        }

        /// <summary>
        /// Name + gender + role/origin for bot-readable death and killer lines.
        /// Examples: "Mia (female free colonist, colony member)", "Bob (male, from Pirates)"
        /// </summary>
        internal static string BuildDeathEntityDescription(Pawn pawn, string displayName)
        {
            if (pawn == null)
                return displayName ?? "Unknown";

            var tags = new List<string>();

            // Gender (skip animals with None)
            if (pawn.gender == Gender.Male)
                tags.Add("male");
            else if (pawn.gender == Gender.Female)
                tags.Add("female");

            bool isAnimal = pawn.RaceProps?.Animal ?? false;
            bool isHumanlike = pawn.RaceProps?.Humanlike ?? false;

            if (isAnimal)
            {
                bool hasScaria = false;
                try
                {
                    var scariaDef = DefDatabase<HediffDef>.GetNamedSilentFail("Scaria");
                    if (scariaDef != null && pawn.health?.hediffSet?.HasHediff(scariaDef) == true)
                        hasScaria = true;
                }
                catch { }

                if (hasScaria)
                    tags.Add("Scaria-infected animal, like Rabies");
                else if (pawn.Faction != null && !pawn.Faction.IsPlayer)
                    tags.Add(pawn.Faction.Name ?? pawn.Faction.def?.label ?? "hostile faction");
                else if (pawn.Faction != null && pawn.Faction.IsPlayer)
                    tags.Add("player animal");
                else
                    tags.Add("woodland creature");
            }
            else if (isHumanlike || pawn.RaceProps != null)
            {
                string role = GetPawnRoleLabel(pawn);
                if (!string.IsNullOrEmpty(role))
                    tags.Add(role);

                string origin = GetPawnOriginLabel(pawn);
                if (!string.IsNullOrEmpty(origin) && !tags.Any(t => t.IndexOf(origin, StringComparison.OrdinalIgnoreCase) >= 0))
                    tags.Add(origin);
            }

            if (tags.Count == 0)
                return displayName;

            return $"{displayName} ({string.Join(", ", tags)})";
        }

        /// <summary>
        /// free colonist / colonist / slave / prisoner / guest / faction member
        /// </summary>
        internal static string GetPawnRoleLabel(Pawn pawn)
        {
            if (pawn == null) return null;

            try
            {
                // Most specific first
                if (pawn.IsPrisoner || pawn.IsPrisonerOfColony)
                    return "prisoner";

                if (IsSlaveSafe(pawn))
                    return "slave";

                if (pawn.IsFreeColonist)
                    return "free colonist";

                if (pawn.IsColonist)
                    return "colonist";

                // Guest / visitor on map
                try
                {
                    if (pawn.GuestStatus == GuestStatus.Guest)
                        return "guest";
                }
                catch { /* API variance */ }

                if (pawn.Faction != null && pawn.Faction.IsPlayer)
                    return "player faction member";

                if (pawn.Faction != null && !pawn.Faction.IsPlayer)
                {
                    if (pawn.Faction.HostileTo(Faction.OfPlayer))
                        return "hostile faction member";
                    return "faction member";
                }
            }
            catch { }

            return null;
        }

        /// <summary>Where the pawn "came from" — faction / guest origin.</summary>
        internal static string GetPawnOriginLabel(Pawn pawn)
        {
            if (pawn == null) return null;

            try
            {
                if (pawn.Faction != null && !pawn.Faction.IsPlayer)
                {
                    string f = pawn.Faction.Name ?? pawn.Faction.def?.label;
                    if (!string.IsNullOrEmpty(f))
                        return $"from {f}";
                }

                if (pawn.Faction != null && pawn.Faction.IsPlayer)
                {
                    // Colony member; optional guest join faction not always available
                    try
                    {
                        if (pawn.GuestStatus == GuestStatus.Guest)
                            return "guest of the colony";
                    }
                    catch { }

                    return "colony member";
                }

                if (pawn.Faction == null)
                    return "no faction";
            }
            catch { }

            return null;
        }

        private static bool IsSlaveSafe(Pawn pawn)
        {
            try
            {
                if (!ModsConfig.IdeologyActive)
                    return false;
                return pawn.IsSlave;
            }
            catch
            {
                return false;
            }
        }
    }
}
