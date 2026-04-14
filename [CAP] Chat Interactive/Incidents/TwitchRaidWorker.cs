// Source/RICS/Incidents/TwitchRaidWorker.cs
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
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    /// <summary>
    /// Lightweight wrapper around vanilla IncidentWorker_RaidEnemy.
    /// - Injects Twitch raider usernames
    /// - Limits raider gear to colony tech level (anti-slaughter balance)
    /// All other behavior remains 100% vanilla.
    /// </summary>
    public class IncidentWorker_TwitchRaid : IncidentWorker_RaidEnemy
    {
        // Static reference so TwitchService can pass the current raider list
        public static List<string> CurrentRaidUsernames { get; set; } = new List<string>();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Let vanilla do all the heavy lifting first
            bool baseResult = base.TryExecuteWorker(parms);

            if (!baseResult)
                return false;

            // Inject Twitch usernames
            if (CurrentRaidUsernames != null && CurrentRaidUsernames.Count > 0)
            {
                InjectRaiderNamesIntoRaid(parms);
            }

            // Limit raider gear to colony tech level
            LimitRaiderGearToColonyTech(parms);

            return true;
        }

        private void InjectRaiderNamesIntoRaid(IncidentParms parms)
        {
            if (parms.target is not Map map)
                return;

            var raidPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != Faction.OfPlayer &&
                            p.RaceProps.Humanlike &&
                            !p.Dead &&
                            p.guest != null)
                .ToList();

            if (raidPawns.Count == 0)
                return;

            var usernames = CurrentRaidUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .OrderBy(_ => Rand.Value)
                .ToList();

            int index = 0;
            foreach (var pawn in raidPawns)
            {
                if (index >= usernames.Count) break;

                string twitchName = usernames[index++];
                pawn.Name = new NameTriple(twitchName, twitchName, twitchName);

                // Optional fun thought (create this ThoughtDef in XML if you want it)
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDef.Named("RaidFromTwitch"), null);

                Logger.Twitch($"Renamed raid pawn to Twitch raider: {twitchName}");
            }

            CurrentRaidUsernames.Clear();
        }

        /// <summary>
        /// Caps raider gear to colony tech level using vanilla ResearchManager.IsFinished().
        /// Matches your !raid command logic exactly.
        /// </summary>
        private void LimitRaiderGearToColonyTech(IncidentParms parms)
        {
            if (parms.target is not Map map) return;

            TechLevel colonyTech = GetColonyTechLevel();

            var raidPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != Faction.OfPlayer && p.RaceProps.Humanlike && !p.Dead)
                .ToList();

            int itemsStripped = 0;

            foreach (var pawn in raidPawns)
            {
                // Strip over-tech apparel
                if (pawn.apparel != null)
                {
                    foreach (var apparel in pawn.apparel.WornApparel.ToList())
                    {
                        if (apparel.def.techLevel > colonyTech)
                        {
                            pawn.apparel.Remove(apparel);
                            apparel.Destroy();
                            itemsStripped++;
                        }
                    }
                }

                // Strip over-tech weapon
                if (pawn.equipment?.Primary != null)
                {
                    if (pawn.equipment.Primary.def.techLevel > colonyTech)
                    {
                        pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
                        itemsStripped++;
                    }
                }
            }

            Logger.Twitch($"Raid gear limited to colony tech level: {colonyTech} | Stripped {itemsStripped} over-tech items");
        }

        /// <summary>
        /// Determines colony tech level using exact research milestones you requested.
        /// Uses ResearchManager.IsFinished() — the correct vanilla method.
        /// </summary>
        private static TechLevel GetColonyTechLevel()
        {
            var research = Find.ResearchManager;

            // 1. Ultra / Starflight (end-game)
            if (IsResearchFinished("StarflightBasics"))
                return TechLevel.Ultra;

            // 2. Spacer (Fabrication is a solid spacer gate)
            if (IsResearchFinished("Fabrication"))
                return TechLevel.Spacer;

            // 3. Industrial (Machining is the classic industrial gate)
            if (IsResearchFinished("Machining"))
                return TechLevel.Industrial;

            // 4. Everything before Machining = Neolithic / Tribal
            return TechLevel.Neolithic;
        }

        /// <summary>
        /// Safe helper that returns false if the research def doesn't exist (modded games, etc.).
        /// </summary>
        private static bool IsResearchFinished(string defName)
        {
            var proj = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return proj != null && proj.IsFinished;
        }
    }
} 