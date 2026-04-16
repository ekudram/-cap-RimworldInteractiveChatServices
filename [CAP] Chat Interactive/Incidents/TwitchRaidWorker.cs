// Source/RICS/Incidents/TwitchRaidWorker.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. RICS Rimworld Chat Interactive Service
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
// Source/RICS/Incidents/TwitchRaidWorker.cs

using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class IncidentWorker_TwitchRaid : IncidentWorker_RaidEnemy
    {
        public static List<string> CurrentRaidUsernames { get; set; } = new List<string>();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            bool baseResult = base.TryExecuteWorker(parms);
            if (!baseResult)
                return false;

            // Delay name injection by a few ticks so all raid pawns are fully spawned
            if (CurrentRaidUsernames != null && CurrentRaidUsernames.Count > 0)
            {
                LongEventHandler.QueueLongEvent(() =>
                {
                    InjectRaiderNamesIntoRaid(parms);
                }, "Injecting Twitch raider names", false, null);
            }

            LimitRaiderGearToColonyTech(parms);

            return true;
        }

        private void InjectRaiderNamesIntoRaid(IncidentParms parms)
        {
            if (parms.target is not Map map)
                return;

            // Find hostile humanlike pawns with generic names (very reliable after spawn delay)
            var raidPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != Faction.OfPlayer
                         && p.RaceProps.Humanlike
                         && !p.Dead
                         && p.Name is NameTriple triple
                         && (triple.Nick == null || triple.Nick.Length < 6 || triple.Nick == p.kindDef.label))
                .ToList();

            Logger.Twitch($"Name injection (delayed): Found {raidPawns.Count} generic raid pawns.");

            if (raidPawns.Count == 0)
            {
                Logger.Twitch("WARNING: Still no generic raid pawns found even after delay.");
                return;
            }

            var usernames = CurrentRaidUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .OrderBy(_ => Rand.Value)
                .ToList();

            int index = 0;
            int renamed = 0;

            foreach (var pawn in raidPawns)
            {
                if (index >= usernames.Count) break;

                string twitchName = usernames[index++];
                pawn.Name = new NameTriple(twitchName, twitchName, twitchName);
                renamed++;

                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDef.Named("RaidFromTwitch"), null);

                Logger.Twitch($"Renamed raid pawn → @{twitchName}");
            }

            Logger.Twitch($"Successfully renamed {renamed} raid pawns with Twitch usernames.");

            CurrentRaidUsernames.Clear();
        }

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

                if (pawn.equipment?.Primary != null)
                {
                    if (pawn.equipment.Primary.def.techLevel > colonyTech)
                    {
                        pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
                        itemsStripped++;
                    }
                }
            }

            Logger.Twitch($"Gear limited to {colonyTech} | Stripped {itemsStripped} over-tech items");
        }

        private static TechLevel GetColonyTechLevel()
        {
            if (IsResearchFinished("StarflightBasics")) return TechLevel.Ultra;
            if (IsResearchFinished("Fabrication")) return TechLevel.Spacer;
            if (IsResearchFinished("Machining")) return TechLevel.Industrial;
            return TechLevel.Neolithic;
        }

        private static bool IsResearchFinished(string defName)
        {
            var proj = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return proj != null && proj.IsFinished;
        }
    }
}