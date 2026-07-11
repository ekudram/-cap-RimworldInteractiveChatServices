// RescueMeCommandHandler.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
//
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
//
// !rescueme — recover vanished viewer pawns.
// Captured/kidnapped → world PrisonerWillingToJoin site (go get them).
// Left behind (not captive) → drop pod / GenSpawn return home.

using _CAP__Chat_Interactive.Command.CommandHelpers;
using CAP_ChatInteractive.Commands.Cooldowns;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public enum RescueKind
    {
        None,
        Missing,
        Dead,
        Safe,
        Captured,
        LeftBehind
    }

    public class RescueDiagnosis
    {
        public RescueKind Kind;
        public Pawn Pawn;
        public Faction HoldingFaction;
        public Map Map;
        public string DebugNote;
    }

    public static class RescueMeCommandHandler
    {
        public const string CommandName = "rescueme";
        private const int DefaultLeftBehindCost = 400;
        private const int DefaultCapturedCost = 800;
        private const int SiteMinDist = 4;
        private const int SiteMaxDist = 18;

        public static string HandleRescueMe(ChatMessageWrapper message, string[] args)
        {
            try
            {
                if (Current.Game == null || Current.ProgramState != ProgramState.Playing)
                    return "RICS.RMCH.GameNotReady".Translate();

                var globalSettings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (globalSettings == null)
                    return "RICS.RMCH.GenericError".Translate();

                string currency = globalSettings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(message);
                if (viewer == null)
                    return "RICS.RMCH.GenericError".Translate();

                var cmdSettings = CommandSettingsManager.GetSettings(CommandName);
                var cooldownManager = Current.Game.GetComponent<GlobalCooldownManager>();
                if (cooldownManager == null)
                {
                    cooldownManager = new GlobalCooldownManager(Current.Game);
                    Current.Game.components.Add(cooldownManager);
                }

                if (!cooldownManager.CanUseCommand(CommandName, cmdSettings, globalSettings))
                    return "RICS.RMCH.CooldownActive".Translate();

                // Free diagnose: !rescueme status
                bool statusOnly = args != null && args.Length > 0 &&
                                  args[0].Equals("status", StringComparison.OrdinalIgnoreCase);

                RescueDiagnosis diag = Diagnose(message);
                Logger.Debug(
                    $"[RescueMe] kind={diag.Kind} pawn={diag.Pawn?.LabelShort ?? "null"} " +
                    $"holder={diag.HoldingFaction?.Name ?? "none"} note={diag.DebugNote}");

                switch (diag.Kind)
                {
                    case RescueKind.None:
                        return "RICS.RMCH.NoAssignment".Translate();

                    case RescueKind.Missing:
                        return "RICS.RMCH.Missing".Translate();

                    case RescueKind.Dead:
                        return "RICS.RMCH.DeadUseRevive".Translate(diag.Pawn?.LabelShort ?? "pawn");

                    case RescueKind.Safe:
                        string where = diag.Map?.Parent?.LabelCap ?? diag.Map?.ToString() ?? "?";
                        return "RICS.RMCH.AlreadySafe".Translate(diag.Pawn.LabelShort, where);

                    case RescueKind.Captured:
                    case RescueKind.LeftBehind:
                        if (statusOnly)
                            return FormatStatus(diag);
                        break;

                    default:
                        return "RICS.RMCH.GenericError".Translate();
                }

                int cost = GetCost(diag.Kind, cmdSettings);
                if (viewer.Coins < cost)
                {
                    return "RICS.RMCH.CannotAfford".Translate(
                        cost, currency, viewer.Coins, currency);
                }

                bool ok;
                string chatResult;

                if (diag.Kind == RescueKind.Captured)
                    ok = TryStartCapturedRescueMission(diag, message.Username, out chatResult);
                else
                    ok = TryDropPodHome(diag, message.Username, out chatResult);

                if (!ok)
                    return chatResult; // failure message, no charge

                viewer.TakeCoins(cost);
                cooldownManager.RecordCommandUse(CommandName);

                Logger.Message(
                    $"[RescueMe] {message.Username} rescued path={diag.Kind} " +
                    $"pawn={diag.Pawn.LabelShort} cost={cost}");

                return chatResult + " " + "RICS.RMCH.CostPaid".Translate(cost, currency, viewer.Coins, currency);
            }
            catch (Exception ex)
            {
                Logger.Error($"[RescueMe] Error: {ex}");
                return "RICS.RMCH.GenericError".Translate();
            }
        }

        public static RescueDiagnosis Diagnose(ChatMessageWrapper message)
        {
            var result = new RescueDiagnosis { Kind = RescueKind.None };
            var assignment = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
            if (assignment == null)
            {
                result.DebugNote = "no assignment manager";
                return result;
            }

            Pawn pawn = assignment.GetAssignedPawn(message);
            if (pawn == null)
            {
                // Distinguish no assignment vs missing ThingID
                string id = null;
                // HasAssignedPawn returns true even if dead; GetAssignedPawn null means not found
                if (assignment.HasAssignedPawn(message))
                {
                    result.Kind = RescueKind.Missing;
                    result.DebugNote = "assignment exists but pawn not found";
                }
                else
                {
                    result.Kind = RescueKind.None;
                    result.DebugNote = "no assignment";
                }
                return result;
            }

            result.Pawn = pawn;
            result.Map = pawn.Map;

            if (pawn.Dead)
            {
                result.Kind = RescueKind.Dead;
                result.DebugNote = "dead";
                return result;
            }

            Faction holder = FindHoldingFaction(pawn);
            if (holder != null)
            {
                result.Kind = RescueKind.Captured;
                result.HoldingFaction = holder;
                result.DebugNote = $"kidnapped by {holder.Name}";
                return result;
            }

            // Prisoner of non-player (e.g. still on a map)
            if (pawn.IsPrisonerOfColony)
            {
                // Our prison — treat as safe
                result.Kind = RescueKind.Safe;
                result.DebugNote = "player prisoner";
                return result;
            }

            if (pawn.IsPrisoner && pawn.HostFaction != null && pawn.HostFaction != Faction.OfPlayer)
            {
                result.Kind = RescueKind.Captured;
                result.HoldingFaction = pawn.HostFaction;
                result.DebugNote = $"prisoner of {pawn.HostFaction.Name}";
                return result;
            }

            if (IsSafelyAtColony(pawn))
            {
                result.Kind = RescueKind.Safe;
                result.DebugNote = "spawned free player pawn on colony map";
                return result;
            }

            result.Kind = RescueKind.LeftBehind;
            result.DebugNote = pawn.Spawned
                ? $"spawned off-colony on {pawn.Map?.Parent?.LabelCap}"
                : "world/off-map not captive";
            return result;
        }

        /// <summary>True if pawn is a free player colonist currently on a playable colony map.</summary>
        public static bool IsSafelyAtColony(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
                return false;
            if (pawn.Faction != Faction.OfPlayer)
                return false;
            if (pawn.IsPrisoner)
                return false;

            Map map = pawn.Map;
            if (map.IsPlayerHome)
                return true;
            // Nomadic pocket / temp map with free colonists counts as "with the colony"
            if (map.mapPawns?.FreeColonistsSpawned?.Count > 0)
                return true;
            return false;
        }

        public static Faction FindHoldingFaction(Pawn pawn)
        {
            if (pawn == null) return null;

            if (pawn.IsKidnapped())
            {
                foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
                {
                    if (f?.kidnapped?.KidnappedPawnsListForReading == null) continue;
                    if (f.kidnapped.KidnappedPawnsListForReading.Contains(pawn))
                        return f;
                }
            }

            if (pawn.IsPrisoner && pawn.HostFaction != null && pawn.HostFaction != Faction.OfPlayer)
                return pawn.HostFaction;

            return null;
        }

        private static int GetCost(RescueKind kind, CommandSettings cmdSettings)
        {
            int left = cmdSettings != null
                ? cmdSettings.GetCustom<int>("leftBehindCost", DefaultLeftBehindCost)
                : DefaultLeftBehindCost;
            int cap = cmdSettings != null
                ? cmdSettings.GetCustom<int>("capturedCost", DefaultCapturedCost)
                : DefaultCapturedCost;
            if (left < 0) left = DefaultLeftBehindCost;
            if (cap < 0) cap = DefaultCapturedCost;
            return kind == RescueKind.Captured ? cap : left;
        }

        private static string FormatStatus(RescueDiagnosis diag)
        {
            return diag.Kind switch
            {
                RescueKind.Captured => "RICS.RMCH.StatusCaptured".Translate(
                    diag.Pawn.LabelShort, diag.HoldingFaction?.Name ?? "?"),
                RescueKind.LeftBehind => "RICS.RMCH.StatusLeftBehind".Translate(diag.Pawn.LabelShort),
                _ => "RICS.RMCH.GenericError".Translate()
            };
        }

        // ─── Left behind: drop pod home ───────────────────────────────────────

        private static bool TryDropPodHome(RescueDiagnosis diag, string viewerName, out string message)
        {
            message = null;
            Pawn pawn = diag.Pawn;
            if (pawn == null || pawn.Dead)
            {
                message = "RICS.RMCH.GenericError".Translate();
                return false;
            }

            Map map = ItemDeliveryHelper.ResolveDeliveryMap(pawn.Spawned ? pawn : null, allowUndergroundRedirect: false)
                       ?? ItemDeliveryHelper.ResolveDeliveryMap(null, allowUndergroundRedirect: false);

            if (map == null)
            {
                message = "RICS.RMCH.NoMap".Translate();
                return false;
            }

            try
            {
                // Ensure colonist ownership before drop
                if (pawn.Faction != Faction.OfPlayer)
                    pawn.SetFaction(Faction.OfPlayer);

                if (pawn.guest != null && pawn.IsPrisoner)
                    pawn.guest.SetGuestStatus(null);

                // Despawn if on another map
                if (pawn.Spawned)
                    pawn.DeSpawn(DestroyMode.Vanish);

                if (!ItemDeliveryHelper.TryDeliverGeneratedPawn(pawn, map, out IntVec3 pos))
                {
                    message = "RICS.RMCH.DropFailed".Translate(pawn.LabelShort);
                    return false;
                }

                string mapLabel = map.Parent?.LabelCap ?? map.ToString();
                string letterLabel = "RICS.RMCH.Letter.DropLabel".Translate(viewerName);
                string letterText = "RICS.RMCH.Letter.DropBody".Translate(
                    viewerName, pawn.LabelShort, mapLabel, pos.x, pos.z);
                Find.LetterStack.ReceiveLetter(
                    letterLabel, letterText, LetterDefOf.PositiveEvent,
                    new LookTargets(pos, map));

                message = "RICS.RMCH.DropSuccess".Translate(pawn.LabelShort, mapLabel);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[RescueMe] DropPodHome failed: {ex}");
                message = "RICS.RMCH.DropFailed".Translate(pawn.LabelShort);
                return false;
            }
        }

        // ─── Captured: world PrisonerWillingToJoin site ───────────────────────

        private static bool TryStartCapturedRescueMission(RescueDiagnosis diag, string viewerName, out string message)
        {
            message = null;
            Pawn pawn = diag.Pawn;
            Faction holder = diag.HoldingFaction;

            if (pawn == null || pawn.Dead)
            {
                message = "RICS.RMCH.GenericError".Translate();
                return false;
            }

            if (holder == null || holder == Faction.OfPlayer)
            {
                // Captive state without clear holder — fall back to drop pod
                Logger.Debug("[RescueMe] Captured without holder faction — drop-pod fallback");
                return TryDropPodHome(diag, viewerName, out message);
            }

            SitePartDef prisonerPart = DefDatabase<SitePartDef>.GetNamedSilentFail("PrisonerWillingToJoin");
            if (prisonerPart == null)
            {
                Logger.Warning("[RescueMe] PrisonerWillingToJoin site part missing — drop-pod fallback");
                return TryDropPodHomeAfterFreeFromKidnap(diag, viewerName, out message);
            }

            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, SiteMinDist, SiteMaxDist, allowCaravans: true))
            {
                Logger.Warning("[RescueMe] No site tile — drop-pod fallback");
                return TryDropPodHomeAfterFreeFromKidnap(diag, viewerName, out message);
            }

            try
            {
                float points = StorytellerUtility.DefaultSiteThreatPointsNow();
                Site site = SiteMaker.MakeSite(prisonerPart, tile, holder, ifHostileThenMustRemainHostile: true, threatPoints: points);
                if (site == null)
                {
                    message = "RICS.RMCH.MissionFailed".Translate();
                    return false;
                }

                // Free from kidnapped tracker / despawn so we can host on the site
                PreparePawnForSiteCustody(pawn, holder);

                SitePart part = site.parts?.FirstOrDefault(p => p.def == prisonerPart);
                if (part == null)
                {
                    Logger.Warning("[RescueMe] Site missing PrisonerWillingToJoin part — drop fallback");
                    site.Destroy();
                    return TryDropPodHomeAfterFreeFromKidnap(diag, viewerName, out message);
                }

                if (part.things == null)
                    part.things = new ThingOwner<Pawn>(part, true);

                if (!part.things.TryAdd(pawn, canMergeWithExistingStacks: false))
                {
                    // Comp path fallback
                    var comp = site.GetComponent<PrisonerWillingToJoinComp>();
                    if (comp == null || !comp.pawn.TryAdd(pawn, canMergeWithExistingStacks: false))
                    {
                        Logger.Error("[RescueMe] Could not place pawn on rescue site — drop fallback");
                        site.Destroy();
                        return TryDropPodHomeAfterFreeFromKidnap(diag, viewerName, out message);
                    }
                }

                if (pawn.mindState != null)
                    pawn.mindState.WillJoinColonyIfRescued = true;

                Find.WorldObjects.Add(site);

                string factionName = holder.Name;
                string letterLabel = "RICS.RMCH.Letter.MissionLabel".Translate(viewerName);
                string letterText = "RICS.RMCH.Letter.MissionBody".Translate(
                    viewerName, pawn.LabelShort, factionName);
                Find.LetterStack.ReceiveLetter(
                    letterLabel, letterText, LetterDefOf.NeutralEvent, new LookTargets(site));

                message = "RICS.RMCH.MissionSuccess".Translate(pawn.LabelShort, factionName);
                Logger.Message(
                    $"[RescueMe] World rescue site created tile={tile} faction={factionName} pawn={pawn.LabelShort}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[RescueMe] Captured mission failed: {ex}");
                message = "RICS.RMCH.MissionFailed".Translate();
                return false;
            }
        }

        private static void PreparePawnForSiteCustody(Pawn pawn, Faction hostFaction)
        {
            // Remove from all kidnapped lists
            foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f?.kidnapped == null) continue;
                if (f.kidnapped.KidnappedPawnsListForReading.Contains(pawn))
                    f.kidnapped.RemoveKidnappedPawn(pawn);
            }

            if (pawn.Spawned)
                pawn.DeSpawn(DestroyMode.Vanish);

            try
            {
                if (pawn.guest != null && hostFaction != null)
                    pawn.guest.SetGuestStatus(hostFaction, GuestStatus.Prisoner);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[RescueMe] SetGuestStatus soft-fail: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback when site creation fails: free from kidnap and drop pod home.
        /// Still recovers the pawn (weaker fantasy, reliable).
        /// </summary>
        private static bool TryDropPodHomeAfterFreeFromKidnap(RescueDiagnosis diag, string viewerName, out string message)
        {
            if (diag.Pawn != null)
            {
                foreach (Faction f in Find.FactionManager.AllFactionsListForReading)
                {
                    if (f?.kidnapped?.KidnappedPawnsListForReading?.Contains(diag.Pawn) == true)
                        f.kidnapped.RemoveKidnappedPawn(diag.Pawn);
                }
            }

            bool ok = TryDropPodHome(diag, viewerName, out message);
            if (ok)
                message = "RICS.RMCH.DropSuccessFallback".Translate(diag.Pawn.LabelShort) + " " + message;
            return ok;
        }
    }
}
