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
using Verse.AI.Group;

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

                AiMapLocation location = null;
                Map relevantMap = null;
                IntVec3 sliceCenter = IntVec3.Invalid;
                try
                {
                    LookTargets lt = null;
                    if (let is ChoiceLetter cl)
                        lt = cl.lookTargets;

                    location = AIChatBotService.TryCreateMapLocationFromLookTargets(lt);

                    if (lt != null && !lt.targets.NullOrEmpty())
                    {
                        var primary = lt.TryGetPrimaryTarget();
                        if (primary.IsValid && primary.IsMapTarget)
                        {
                            relevantMap = primary.Map;
                            if (relevantMap == null && primary.HasThing && primary.Thing != null)
                                relevantMap = primary.Thing.MapHeld ?? primary.Thing.Map;
                            if (primary.HasThing && primary.Thing != null)
                            {
                                var pos = primary.Thing.PositionHeld.IsValid ? primary.Thing.PositionHeld : primary.Thing.Position;
                                if (pos.IsValid) sliceCenter = pos;
                            }
                            else if (primary.Cell.IsValid)
                                sliceCenter = primary.Cell;
                        }
                    }
                }
                catch { /* best effort */ }

                if (relevantMap == null)
                    relevantMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;

                if (location != null && !sliceCenter.IsValid)
                    sliceCenter = new IntVec3(location.x, location.y, location.z);

                AiMapSlicePayload mapSlice = null;
                if (relevantMap != null && sliceCenter.IsValid)
                    mapSlice = AiMapSliceBuilder.TryBuild(relevantMap, sliceCenter, AiMapSliceBuilder.SliceSizeLetter);
                else if (location != null)
                    mapSlice = AiMapSliceBuilder.TryBuildFromLocation(location, AiMapSliceBuilder.SliceSizeLetter);

                string mapDesc = location?.mapLabel ?? AIChatBotService.GetRichMapDescription(relevantMap);
                string factionNote = BuildInvolvedFactionNote(let);

                LookTargets letterTargets = null;
                if (let is ChoiceLetter clForPawns)
                    letterTargets = clForPawns.lookTargets;
                // Letters (mental break, etc.) often name people in the body; still add Who: with kind + detail.
                string whoNote = AiNotificationHelpers.BuildInvolvedPawnsNote(
                    letterTargets,
                    title + " " + body,
                    letterKindHint: null);

                string forceNote = BuildThreatForceNote(let, relevantMap, title, body);

                string notification = $"{botName} this has occurred in the colony on {mapDesc}{AIChatBotService.FormatCoordsProse(location)}: {title}.";
                if (!string.IsNullOrWhiteSpace(whoNote))
                    notification += $" {whoNote}";
                if (!string.IsNullOrWhiteSpace(factionNote))
                    notification += $" {factionNote}";
                if (!string.IsNullOrWhiteSpace(forceNote))
                    notification += $" {forceNote}";
                if (!string.IsNullOrWhiteSpace(body))
                    notification += $" {body}";
                if (!string.IsNullOrWhiteSpace(mapSlice?.summary))
                    notification += $" [Area: {mapSlice.summary}]";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?._aiChatBotService?.NotifyColonyEvent(notification, location, mapSlice);
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

        /// <summary>
        /// For raid / manhunter letters, count hostiles actually on the map so Masie
        /// can say "14 raiders" / "8 manhunter wargs" instead of guessing.
        /// Omits the note if nothing is spawned yet (do not invent 0).
        /// </summary>
        private static string BuildThreatForceNote(Letter let, Map map, string title, string body)
        {
            try
            {
                if (map == null || map.Disposed)
                    return null;

                string text = ((title ?? "") + " " + (body ?? "")).ToLowerInvariant();
                bool manhunter = text.Contains("manhunter");
                bool raidLike = manhunter
                    || text.Contains("raid")
                    || text.Contains("siege")
                    || text.Contains("sapper")
                    || text.Contains("mech cluster")
                    || text.Contains("mechanoid cluster")
                    || text.Contains("infestation")
                    || text.Contains("shambler");

                if (!raidLike)
                    return null;

                Faction raidFaction = TryGetLetterFaction(let);

                if (manhunter)
                    return CountManhunterForce(map);

                return CountRaidForce(map, raidFaction);
            }
            catch
            {
                return null;
            }
        }

        private static Faction TryGetLetterFaction(Letter let)
        {
            try
            {
                if (let is ChoiceLetter cl)
                {
                    try
                    {
                        var fi = typeof(ChoiceLetter).GetField("relatedFaction");
                        if (fi?.GetValue(cl) is Faction rf && rf != null && !rf.IsPlayer)
                            return rf;
                    }
                    catch { /* no field */ }

                    if (cl.lookTargets != null && !cl.lookTargets.targets.NullOrEmpty())
                    {
                        foreach (var t in cl.lookTargets.targets)
                        {
                            if (!t.IsValid || !t.HasThing || t.Thing == null) continue;
                            Faction f = t.Thing.Faction;
                            if (f != null && !f.IsPlayer)
                                return f;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string CountRaidForce(Map map, Faction raidFaction)
        {
            var hostiles = new List<Pawn>();
            try
            {
                var spawned = map.mapPawns?.AllPawnsSpawned;
                if (spawned != null)
                {
                    foreach (var p in spawned)
                    {
                        if (p == null || p.Dead || p.Destroyed) continue;
                        if (p.IsPrisonerOfColony) continue;
                        if (!p.HostileTo(Faction.OfPlayer)) continue;
                        if (raidFaction != null && p.Faction != raidFaction) continue;
                        hostiles.Add(p);
                    }
                }
            }
            catch { }

            if (hostiles.Count == 0)
                hostiles = CountHostileLordPawns(map, raidFaction);

            if (hostiles.Count == 0)
                return null;

            string noun = hostiles.All(p => p.RaceProps?.IsMechanoid == true)
                ? (hostiles.Count == 1 ? "mechanoid" : "mechanoids")
                : hostiles.All(p => p.RaceProps?.Insect == true)
                    ? (hostiles.Count == 1 ? "insect" : "insects")
                    : hostiles.All(p => p.RaceProps?.Animal == true)
                        ? (hostiles.Count == 1 ? "hostile animal" : "hostile animals")
                        : (hostiles.Count == 1 ? "raider" : "raiders");

            string kinds = SummarizePawnKinds(hostiles, maxKinds: 3);
            string extra = string.IsNullOrEmpty(kinds) ? "" : $" ({kinds})";
            return $"Force: {hostiles.Count} {noun}{extra}.";
        }

        private static List<Pawn> CountHostileLordPawns(Map map, Faction raidFaction)
        {
            var list = new List<Pawn>();
            try
            {
                var lords = map.lordManager?.lords;
                if (lords == null) return list;
                foreach (var lord in lords)
                {
                    if (lord?.ownedPawns == null) continue;
                    if (raidFaction != null && lord.faction != raidFaction) continue;
                    if (raidFaction == null && (lord.faction == null || !lord.faction.HostileTo(Faction.OfPlayer)))
                        continue;
                    foreach (var p in lord.ownedPawns)
                    {
                        if (p == null || p.Dead) continue;
                        if (!list.Contains(p))
                            list.Add(p);
                    }
                }
            }
            catch { }
            return list;
        }

        private static string CountManhunterForce(Map map)
        {
            var pack = new List<Pawn>();
            try
            {
                var spawned = map.mapPawns?.AllPawnsSpawned;
                if (spawned != null)
                {
                    foreach (var p in spawned)
                    {
                        if (p == null || p.Dead || p.Destroyed) continue;
                        if (!IsManhunter(p)) continue;
                        pack.Add(p);
                    }
                }
            }
            catch { }

            if (pack.Count == 0)
                return null;

            string kinds = SummarizePawnKinds(pack, maxKinds: 3);
            string extra = string.IsNullOrEmpty(kinds) ? "" : $" ({kinds})";
            string noun = pack.Count == 1 ? "manhunter animal" : "manhunter animals";
            return $"Force: {pack.Count} {noun}{extra}.";
        }

        private static bool IsManhunter(Pawn p)
        {
            try
            {
                if (!p.InMentalState || p.MentalStateDef == null)
                    return false;
                if (p.MentalStateDef == MentalStateDefOf.Manhunter)
                    return true;
                if (p.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
                    return true;
                string n = p.MentalStateDef.defName ?? "";
                return n.IndexOf("Manhunter", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static string SummarizePawnKinds(List<Pawn> pawns, int maxKinds)
        {
            try
            {
                var groups = pawns
                    .Where(p => p?.def != null)
                    .GroupBy(p => p.def.label ?? p.def.defName)
                    .OrderByDescending(g => g.Count())
                    .Take(maxKinds)
                    .Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}")
                    .ToList();
                return groups.Count == 0 ? null : string.Join(", ", groups);
            }
            catch
            {
                return null;
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

                var deathLoc = AIChatBotService.TryCreateMapLocationFromThing(pawn);
                AiMapSlicePayload deathSlice = null;
                try
                {
                    if (pawnMap != null)
                    {
                        IntVec3 deathCell = pawn.PositionHeld.IsValid ? pawn.PositionHeld : pawn.Position;
                        if (deathCell.IsValid)
                            deathSlice = AiMapSliceBuilder.TryBuild(pawnMap, deathCell, AiMapSliceBuilder.SliceSizeDeath);
                    }
                    if (deathSlice == null && deathLoc != null)
                        deathSlice = AiMapSliceBuilder.TryBuildFromLocation(deathLoc, AiMapSliceBuilder.SliceSizeDeath);
                }
                catch { /* best effort */ }

                string message = $"{entityDesc}{killerDetail} on {mapLabel}{mapContext}{AIChatBotService.FormatCoordsProse(deathLoc)}";
                if (!string.IsNullOrWhiteSpace(deathSlice?.summary))
                    message += $" [Area: {deathSlice.summary}]";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?.RecordDeath(message, deathLoc, deathSlice);
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

    /// <summary>
    /// Postfix on Messages.Message so the AI bot receives interesting toast notices
    /// (health, threats, outcomes). Technical UI types and RICS admin spam are filtered out.
    /// PawnDeath toasts are skipped (death pipeline already notifies). TaskCompletion off by default.
    /// </summary>
    [HarmonyPatch(typeof(Messages))]
    [HarmonyPatch(nameof(Messages.Message), new[] { typeof(Message), typeof(bool) })]
    public static class Patch_Messages_Message_AINotify
    {
        private static readonly object RateLock = new object();
        private static string _lastNormalizedText;
        private static DateTime _lastWriteUtc = DateTime.MinValue;
        private static readonly Queue<DateTime> _writeTimes = new Queue<DateTime>();

        private const int DedupeWindowSeconds = 4;
        private const int MaxPerMinute = 10;
        private const int MaxTextLength = 500;

        [HarmonyPostfix]
        public static void Postfix(Message msg, bool historical)
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.AIChatBotActive)
                    return;

                if (!settings.AIChatBotForwardGameMessages)
                    return;

                if (msg == null || string.IsNullOrWhiteSpace(msg.text))
                    return;

                MessageTypeDef typeDef = msg.def;
                if (typeDef == null)
                    return;

                // Always technical / UI feedback
                if (typeDef == MessageTypeDefOf.RejectInput ||
                    typeDef == MessageTypeDefOf.CautionInput ||
                    typeDef == MessageTypeDefOf.SilentInput)
                    return;

                // Death pipeline already covers this (Pawn.Kill + letters)
                if (typeDef == MessageTypeDefOf.PawnDeath)
                    return;

                // Job-done spam (construction/haul) — optional
                if (typeDef == MessageTypeDefOf.TaskCompletion && !settings.AIChatBotForwardTaskCompletion)
                    return;

                // Only forward known interest buckets
                if (typeDef != MessageTypeDefOf.ThreatBig &&
                    typeDef != MessageTypeDefOf.ThreatSmall &&
                    typeDef != MessageTypeDefOf.NegativeHealthEvent &&
                    typeDef != MessageTypeDefOf.NegativeEvent &&
                    typeDef != MessageTypeDefOf.PositiveEvent &&
                    typeDef != MessageTypeDefOf.SituationResolved &&
                    typeDef != MessageTypeDefOf.NeutralEvent &&
                    typeDef != MessageTypeDefOf.TaskCompletion)
                    return;

                string cleaned = ChatCommandProcessor.RemoveMarkupTags(msg.text);
                if (string.IsNullOrWhiteSpace(cleaned))
                    return;

                cleaned = cleaned.Trim();
                if (IsTechnicalOrAdminText(cleaned))
                    return;

                if (cleaned.Length > MaxTextLength)
                    cleaned = cleaned.Substring(0, MaxTextLength) + "...";

                string normalized = cleaned.ToLowerInvariant();
                DateTime now = DateTime.UtcNow;

                lock (RateLock)
                {
                    if (_lastNormalizedText == normalized &&
                        (now - _lastWriteUtc).TotalSeconds < DedupeWindowSeconds)
                        return;

                    while (_writeTimes.Count > 0 && (now - _writeTimes.Peek()).TotalSeconds > 60)
                        _writeTimes.Dequeue();

                    if (_writeTimes.Count >= MaxPerMinute)
                        return;

                    _lastNormalizedText = normalized;
                    _lastWriteUtc = now;
                    _writeTimes.Enqueue(now);
                }

                string botName = settings.AIChatBotName ?? "Masie";
                AiMapLocation location = AIChatBotService.TryCreateMapLocationFromLookTargets(msg.lookTargets);
                Map map = ResolveMessageMap(msg);
                if (map == null && location != null)
                    map = Find.Maps?.FirstOrDefault(m => m != null && m.uniqueID == location.mapId);

                AiMapSlicePayload mapSlice = null;
                if (location != null)
                {
                    var cell = new IntVec3(location.x, location.y, location.z);
                    if (map != null && cell.IsValid)
                        mapSlice = AiMapSliceBuilder.TryBuild(map, cell, AiMapSliceBuilder.SliceSizeToast);
                    if (mapSlice == null)
                        mapSlice = AiMapSliceBuilder.TryBuildFromLocation(location, AiMapSliceBuilder.SliceSizeToast);
                }

                string mapDesc = location?.mapLabel ?? AIChatBotService.GetRichMapDescription(map);
                // Medical emergency / break toasts often omit the colonist name in text —
                // identity lives on lookTargets. Enrich with kind (hurt vs breaking) + detail.
                string whoNote = AiNotificationHelpers.BuildInvolvedPawnsNote(
                    msg.lookTargets,
                    cleaned,
                    letterKindHint: typeDef.defName);

                string addressed = $"{botName}, notice on {mapDesc}{AIChatBotService.FormatCoordsProse(location)}: {cleaned}";
                if (!string.IsNullOrWhiteSpace(whoNote))
                    addressed += $" {whoNote}";
                if (!string.IsNullOrWhiteSpace(mapSlice?.summary))
                    addressed += $" [Area: {mapSlice.summary}]";

                // Prefer rawText that includes who when game text is anonymous ("Medical emergency!")
                string rawForBot = string.IsNullOrWhiteSpace(whoNote) ? cleaned : $"{cleaned} {whoNote}";

                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?._aiChatBotService?.NotifyColonyMessage(addressed, rawForBot, typeDef.defName, location, mapSlice);
            }
            catch (Exception ex)
            {
                Logger.Warning($"[RICS AI] Message notification postfix failed (non-fatal): {ex.Message}");
            }
        }

        private static bool IsTechnicalOrAdminText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // Prefix denylist (RICS UI, debug)
            if (text.StartsWith("[RICS]", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("RICS:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Debug", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("[CAP]", StringComparison.OrdinalIgnoreCase))
                return true;

            // Common RICS admin toasts (often NeutralEvent / PositiveEvent without prefix)
            string lower = text.ToLowerInvariant();
            if (lower.Contains("coins awarded") ||
                lower.Contains("viewer coins reset") ||
                lower.Contains("viewer karma reset") ||
                lower.Contains("reconnection initiated") ||
                lower.Contains("store prices reset") ||
                lower.Contains("store items enabled") ||
                lower.Contains("twitch raider name list") ||
                lower.Contains("fake twitch raid") ||
                lower.Contains("must be in a playing game") ||
                lower.Contains("json error saving") ||
                lower.Contains("incidents not saved") ||
                lower.StartsWith("rics: save") ||
                lower.StartsWith("rics: disk") ||
                lower.StartsWith("rics: json"))
                return true;

            // Camera+ / Follow Me Smoothly camera-follow toasts (e.g. "Following Mia.", "No longer following Mia.")
            // These are pure UI and spam the AI if the player tracks colonists.
            if (lower.Contains("following") ||
                lower.Contains("can not follow") ||
                lower.Contains("cannot follow") ||
                lower.Contains("no longer following"))
                return true;

            return false;
        }

        private static Map ResolveMessageMap(Message msg)
        {
            try
            {
                if (msg?.lookTargets != null && !msg.lookTargets.targets.NullOrEmpty())
                {
                    var primary = msg.lookTargets.TryGetPrimaryTarget();
                    if (primary.IsValid && primary.IsMapTarget)
                    {
                        if (primary.Map != null)
                            return primary.Map;
                        if (primary.HasThing && primary.Thing != null)
                            return primary.Thing.MapHeld ?? primary.Thing.Map;
                    }

                    foreach (var t in msg.lookTargets.targets)
                    {
                        if (!t.IsValid || !t.IsMapTarget)
                            continue;
                        if (t.Map != null)
                            return t.Map;
                        if (t.HasThing && t.Thing != null)
                        {
                            var m = t.Thing.MapHeld ?? t.Thing.Map;
                            if (m != null)
                                return m;
                        }
                    }
                }
            }
            catch { /* best effort */ }

            return Find.CurrentMap ?? Find.AnyPlayerHomeMap;
        }
    }

    /// <summary>
    /// Shared helpers so Masie letters/toasts know which pawn(s) are involved
    /// and whether the issue is medical hurt vs mental break (not a generic Who: blob).
    /// </summary>
    internal static class AiNotificationHelpers
    {
        /// <summary>
        /// Prose for Masie — dual contract:
        ///   Hurt: Mia (female free colonist) [downed; bleeding heavily].
        ///   Breaking: Bob (male free colonist) [mental state: berserk].
        ///   Who: [MEDICAL EMERGENCY] Mia (…) — longer detail for LLM.
        /// Masie templates parse Hurt:/Breaking: first (name = first token; detail = [brackets]).
        /// </summary>
        /// <param name="letterKindHint">Optional MessageTypeDef name or free text hint (e.g. NegativeHealthEvent).</param>
        internal static string BuildInvolvedPawnsNote(
            LookTargets lookTargets,
            string existingText = null,
            string letterKindHint = null)
        {
            try
            {
                var pawns = CollectPawnsFromLookTargets(lookTargets);
                if (pawns.Count == 0)
                    return null;

                string existingLower = existingText?.ToLowerInvariant() ?? "";
                string hintLower = letterKindHint?.ToLowerInvariant() ?? "";

                var hurtLines = new List<string>();
                var breakLines = new List<string>();
                var whoParts = new List<string>();

                foreach (var pawn in pawns)
                {
                    if (pawn == null)
                        continue;

                    string name = pawn.LabelShortCap ?? pawn.Name?.ToStringShort ?? "Unknown";
                    name = SanitizePawnNameForBot(name);
                    string baseDesc = Patch_Pawn_Kill_DeathNotifications.BuildDeathEntityDescription(pawn, name);

                    bool moodRisk = TextSuggestsBreakRiskMood(existingLower, hintLower);
                    bool isBreaking = DetectMentalBreak(pawn, existingLower, hintLower);
                    bool isHurt = DetectMedicalCrisis(pawn, existingLower, hintLower, moodRisk);
                    string kindLabel = ResolveKindLabel(isBreaking, isHurt, moodRisk, existingLower, hintLower);

                    var mentalDetails = new List<string>();
                    var medicalDetails = new List<string>();
                    if (isBreaking)
                        AppendMentalBreakDetails(pawn, mentalDetails);
                    if (moodRisk && !isBreaking)
                        AppendBreakRiskMoodDetails(existingLower, mentalDetails);
                    if (isHurt || HasAnyHealthRedFlag(pawn))
                        AppendMedicalDetails(pawn, medicalDetails, allowPlaceholder: isHurt && !moodRisk);
                    else if (!isBreaking && !isHurt && !moodRisk)
                    {
                        AppendMentalBreakDetails(pawn, mentalDetails);
                        AppendMedicalDetails(pawn, medicalDetails, allowPlaceholder: false);
                        if (mentalDetails.Count == 0 && medicalDetails.Count == 0)
                            kindLabel = null;
                    }

                    if (mentalDetails.Count > 0 && isBreaking)
                        isBreaking = true;
                    if (medicalDetails.Count > 0)
                        isHurt = true;
                    if (kindLabel == null)
                        kindLabel = ResolveKindLabel(isBreaking, isHurt, moodRisk, existingLower, hintLower);

                    // Masie Hurt: / Breaking: — short [bracket] detail for TTS templates
                    // Mood break-risk must not emit Hurt: (that makes Masie call a doctor).
                    if (isHurt && kindLabel != "BREAK RISK")
                    {
                        var shortMed = ShortenDetailsForBotBracket(medicalDetails, maxItems: 4);
                        hurtLines.Add(shortMed.Count > 0
                            ? $"{baseDesc} [{string.Join("; ", shortMed)}]"
                            : baseDesc);
                    }

                    if (isBreaking || kindLabel == "MENTAL BREAK" || kindLabel == "MENTAL BREAK + MEDICAL")
                    {
                        // Bot looks for "mental state: X" inside brackets
                        var shortBreak = new List<string>();
                        foreach (var d in mentalDetails)
                        {
                            if (d.StartsWith("breaking:", StringComparison.OrdinalIgnoreCase))
                                shortBreak.Add("mental state: " + d.Substring("breaking:".Length).Trim());
                            else
                                shortBreak.Add(d);
                        }
                        shortBreak = ShortenDetailsForBotBracket(shortBreak, maxItems: 3);
                        breakLines.Add(shortBreak.Count > 0
                            ? $"{baseDesc} [{string.Join("; ", shortBreak)}]"
                            : baseDesc);
                    }

                    // Rich Who: for LLM / logging
                    var allDetails = new List<string>();
                    allDetails.AddRange(mentalDetails);
                    allDetails.AddRange(medicalDetails);

                    string whoPart;
                    if (!string.IsNullOrEmpty(kindLabel))
                    {
                        whoPart = allDetails.Count > 0
                            ? $"[{kindLabel}] {baseDesc} — {string.Join("; ", allDetails)}"
                            : $"[{kindLabel}] {baseDesc}";
                    }
                    else
                    {
                        whoPart = allDetails.Count > 0
                            ? $"{baseDesc} — {string.Join("; ", allDetails)}"
                            : baseDesc;
                    }
                    whoParts.Add(whoPart);
                }

                if (whoParts.Count == 0 && hurtLines.Count == 0 && breakLines.Count == 0)
                    return null;

                var sb = new StringBuilder();
                // Order matches Masie preference: Hurt: then Breaking: then Who:
                if (hurtLines.Count > 0)
                    sb.Append("Hurt: ").Append(string.Join("; ", hurtLines)).Append('.');
                if (breakLines.Count > 0)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append("Breaking: ").Append(string.Join("; ", breakLines)).Append('.');
                }
                if (whoParts.Count > 0)
                {
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append("Who: ").Append(string.Join("; ", whoParts)).Append('.');
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string SanitizePawnNameForBot(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";
            name = name.Trim();
            while (name.Length > 0 && (name[0] == '[' || name[0] == '(' || name[0] == '<' || name[0] == '"'))
                name = name.Substring(1).TrimStart();
            return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
        }

        /// <summary>Cap bracket payload so TTS templates stay short.</summary>
        private static List<string> ShortenDetailsForBotBracket(List<string> details, int maxItems)
        {
            if (details == null || details.Count == 0)
                return new List<string>();

            var result = new List<string>();
            foreach (var d in details)
            {
                if (string.IsNullOrWhiteSpace(d))
                    continue;
                if (d.StartsWith("tendable wounds:", StringComparison.OrdinalIgnoreCase)
                    || d.StartsWith("tendable wound:", StringComparison.OrdinalIgnoreCase)
                    || d.StartsWith("severe/life-threatening:", StringComparison.OrdinalIgnoreCase))
                {
                    if (!result.Any(x => x.IndexOf("wounds", StringComparison.OrdinalIgnoreCase) >= 0))
                        result.Add("severe wounds");
                    continue;
                }
                result.Add(d);
                if (result.Count >= maxItems)
                    break;
            }
            return result;
        }

        private static string ResolveKindLabel(bool isBreaking, bool isHurt, bool moodRisk, string textLower, string hintLower)
        {
            if (isBreaking && isHurt)
                return "MENTAL BREAK + MEDICAL";
            if (isBreaking)
                return "MENTAL BREAK";
            if (moodRisk && isHurt)
                return "BREAK RISK + MEDICAL";
            if (moodRisk)
                return "BREAK RISK";
            if (isHurt)
                return "MEDICAL EMERGENCY";

            // Text/hint only (pawn not yet in state, or delayed)
            if (TextSuggestsBreakRiskMood(textLower, hintLower))
                return "BREAK RISK";
            if (TextSuggestsMentalBreak(textLower, hintLower))
                return "MENTAL BREAK";
            if (TextSuggestsMedical(textLower, hintLower) && !TextSuggestsBreakRiskMood(textLower, hintLower))
                return "MEDICAL EMERGENCY";

            return null;
        }

        private static bool DetectMentalBreak(Pawn pawn, string textLower, string hintLower)
        {
            try
            {
                if (pawn.InMentalState)
                    return true;
            }
            catch { }

            return TextSuggestsMentalBreak(textLower, hintLower);
        }

        private static bool DetectMedicalCrisis(Pawn pawn, string textLower, string hintLower, bool moodRisk)
        {
            // Mood break-risk toasts are not medical. Do not treat MessageType
            // NegativeHealthEvent as a wound when the body says "break risk".
            if (moodRisk)
                return HasAnyHealthRedFlag(pawn);

            if (TextSuggestsMedical(textLower, hintLower))
                return true;

            return HasAnyHealthRedFlag(pawn);
        }

        /// <summary>
        /// Vanilla alert titles: Minor/Major/Extreme break risk — poor mood, not InMentalState.
        /// Must not be treated as MEDICAL EMERGENCY or as an actual mental break.
        /// </summary>
        private static bool TextSuggestsBreakRiskMood(string textLower, string hintLower)
        {
            string s = textLower + " " + hintLower;
            return s.Contains("break risk")
                || s.Contains("major break risk")
                || s.Contains("minor break risk")
                || s.Contains("extreme break risk");
        }

        private static void AppendBreakRiskMoodDetails(string textLower, List<string> details)
        {
            if (details == null)
                return;
            string level = "break risk";
            if (textLower != null)
            {
                if (textLower.Contains("extreme break risk"))
                    level = "extreme break risk";
                else if (textLower.Contains("major break risk"))
                    level = "major break risk";
                else if (textLower.Contains("minor break risk"))
                    level = "minor break risk";
            }
            details.Add("mood: " + level);
        }

        private static bool TextSuggestsMentalBreak(string textLower, string hintLower)
        {
            string s = textLower + " " + hintLower;
            return s.Contains("mental break")
                || s.Contains("mental state")
                || s.Contains("mental breakdown")
                || s.Contains("breakdown")
                || s.Contains("psychotic")
                || s.Contains("berserk")
                || s.Contains("catatonic")
                || s.Contains("social fight")
                || s.Contains("gave up")
                || s.Contains("tantrum")
                || s.Contains("on a tear")
                || s.Contains("is having a") && s.Contains("break");
        }

        private static bool TextSuggestsMedical(string textLower, string hintLower)
        {
            string s = textLower + " " + hintLower;
            return s.Contains("medical")
                || s.Contains("emergency")
                || s.Contains("negativehealthevent")
                || s.Contains("needs treatment")
                || s.Contains("need treatment")
                || s.Contains("wounded")
                || s.Contains("bleeding")
                || s.Contains("blood loss")
                || s.Contains("infection")
                || s.Contains("disease")
                || s.Contains("heart attack")
                || s.Contains("anesthetized")
                || s.Contains("downed")
                || s.Contains("tended")
                || s.Contains("injur");
        }

        private static bool HasAnyHealthRedFlag(Pawn pawn)
        {
            try
            {
                if (pawn == null || pawn.health?.hediffSet == null)
                    return false;
                if (pawn.Downed || pawn.health.ShouldBeDead() || pawn.health.InPainShock)
                    return true;
                if (pawn.health.hediffSet.BleedRateTotal > 0.01f)
                    return true;

                var bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodLoss != null && bloodLoss.Severity >= 0.15f)
                    return true;

                foreach (var h in pawn.health.hediffSet.hediffs)
                {
                    if (h == null || h.def == null)
                        continue;
                    if (h.TendableNow())
                        return true;
                    if (h.def.lethalSeverity > 0f && h.Severity >= h.def.lethalSeverity * 0.5f)
                        return true;
                    try
                    {
                        if (h.IsCurrentlyLifeThreatening)
                            return true;
                    }
                    catch
                    {
                        if (h.def.isBad && h.CurStage != null && h.CurStage.lifeThreatening)
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static void AppendMentalBreakDetails(Pawn pawn, List<string> details)
        {
            try
            {
                if (pawn == null || !pawn.InMentalState || pawn.MentalStateDef == null)
                    return;

                string ms = pawn.MentalStateDef.label ?? pawn.MentalStateDef.defName;
                if (string.IsNullOrWhiteSpace(ms))
                    ms = "unknown mental state";

                details.Add($"breaking: {ms}");

                // Category helps Masie (e.g. Aggressive vs Misc)
                try
                {
                    var cat = pawn.MentalStateDef.category;
                    if (cat != MentalStateCategory.Undefined)
                        details.Add($"break type: {cat}");
                }
                catch { }
            }
            catch { /* best effort */ }
        }

        private static void AppendMedicalDetails(Pawn pawn, List<string> details, bool allowPlaceholder = false)
        {
            try
            {
                if (pawn?.health?.hediffSet == null)
                    return;

                var hs = pawn.health.hediffSet;

                if (pawn.health.ShouldBeDead())
                    details.Add("near death");
                if (pawn.Downed)
                    details.Add("downed");
                if (pawn.health.InPainShock)
                    details.Add("pain shock");

                float bleed = hs.BleedRateTotal;
                if (bleed > 0.01f)
                {
                    if (bleed >= 1.0f)
                        details.Add("bleeding critically");
                    else if (bleed >= 0.5f)
                        details.Add("bleeding heavily");
                    else if (bleed >= 0.15f)
                        details.Add("bleeding");
                    else
                        details.Add("light bleeding");
                }

                var bloodLoss = hs.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
                if (bloodLoss != null && bloodLoss.Severity >= 0.12f)
                {
                    string sev =
                        bloodLoss.Severity >= 0.7f ? "critical" :
                        bloodLoss.Severity >= 0.45f ? "severe" :
                        bloodLoss.Severity >= 0.25f ? "moderate" : "mild";
                    details.Add($"{sev} blood loss");
                }

                // Tendable / life-threatening wounds
                var tendableLabels = new List<string>();
                var severeLabels = new List<string>();
                int tendableCount = 0;

                foreach (var h in hs.hediffs)
                {
                    if (h == null || h.def == null)
                        continue;

                    bool lifeThreatening = false;
                    try
                    {
                        lifeThreatening = h.IsCurrentlyLifeThreatening;
                    }
                    catch
                    {
                        try
                        {
                            lifeThreatening = h.CurStage != null && h.CurStage.lifeThreatening;
                        }
                        catch { }
                    }

                    if (!lifeThreatening && h.def.lethalSeverity > 0f && h.Severity >= h.def.lethalSeverity * 0.55f)
                        lifeThreatening = true;

                    bool tendable = false;
                    try { tendable = h.TendableNow(); }
                    catch { }

                    bool isInjury = h is Hediff_Injury;
                    if (!tendable && !lifeThreatening && !isInjury)
                        continue;

                    // Skip pure buffs / implants
                    if (!h.def.isBad && !tendable)
                        continue;

                    string label;
                    try
                    {
                        label = h.Label ?? h.LabelBase ?? h.def.label ?? h.def.defName;
                    }
                    catch
                    {
                        label = h.def.defName;
                    }

                    if (string.IsNullOrWhiteSpace(label))
                        continue;

                    if (label.Length > 40)
                        label = label.Substring(0, 37) + "...";

                    if (lifeThreatening && severeLabels.Count < 4
                        && !severeLabels.Any(x => x.Equals(label, StringComparison.OrdinalIgnoreCase)))
                        severeLabels.Add(label);

                    if (tendable)
                    {
                        tendableCount++;
                        if (tendableLabels.Count < 4
                            && !tendableLabels.Any(x => x.Equals(label, StringComparison.OrdinalIgnoreCase)))
                            tendableLabels.Add(label);
                    }
                    else if (isInjury && h.def.isBad && tendableLabels.Count < 4
                             && !tendableLabels.Any(x => x.Equals(label, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Untended or already-tended injury still useful as wound list for Masie
                        if (h.Severity >= 0.2f)
                            tendableLabels.Add(label);
                    }
                }

                if (severeLabels.Count > 0)
                    details.Add("severe/life-threatening: " + string.Join(", ", severeLabels));

                if (tendableCount > 0)
                {
                    string woundList = tendableLabels.Count > 0
                        ? string.Join(", ", tendableLabels)
                        : "untreated injuries";
                    details.Add(tendableCount == 1
                        ? $"tendable wound: {woundList}"
                        : $"tendable wounds: {woundList} ({tendableCount} total)");
                }
                else if (allowPlaceholder && severeLabels.Count == 0 && bleed <= 0.01f && !pawn.Downed)
                {
                    // Only for real medical toasts — never for mood break-risk
                    if (details.Count == 0)
                        details.Add("needs medical attention");
                }
            }
            catch { /* best effort */ }
        }

        internal static List<Pawn> CollectPawnsFromLookTargets(LookTargets lookTargets)
        {
            var result = new List<Pawn>();
            if (lookTargets == null || lookTargets.targets.NullOrEmpty())
                return result;

            try
            {
                foreach (var t in lookTargets.targets)
                {
                    if (!t.IsValid || !t.HasThing || t.Thing == null)
                        continue;

                    Pawn p = t.Thing as Pawn;
                    if (p == null && t.Thing is Corpse corpse)
                        p = corpse.InnerPawn;

                    if (p != null && !result.Contains(p))
                        result.Add(p);

                    if (result.Count >= 4)
                        break;
                }
            }
            catch { /* best effort */ }

            return result;
        }
    }
}


