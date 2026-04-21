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

// Source/RICS/Incidents/TwitchRaidWorker.cs
// Source/RICS/Incidents/TwitchRaidWorker.cs
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using TwitchLib.Api.Helix.Models.Extensions.ReleasedExtensions;
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

            var raidPawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Faction != Faction.OfPlayer
                         && p.RaceProps.Humanlike
                         && !p.Dead
                         && !p.IsPrisoner
                         && !p.IsSlave)
                .ToList();

            Logger.Twitch($"Name injection: Found {raidPawns.Count} eligible raid pawns.");

            if (raidPawns.Count == 0)
            {
                Logger.Twitch("WARNING: No eligible raid pawns found.");
                return;
            }

            // Guarantee the first username (usually the streamer) is always used first
            var usernames = CurrentRaidUsernames
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();

            if (usernames.Count == 0)
            {
                CurrentRaidUsernames.Clear();
                return;
            }

            // Shuffle only the remaining usernames after the first one
            var firstUsername = usernames[0];
            var remainingUsernames = usernames.Skip(1).OrderBy(_ => Rand.Value).ToList();

            // Build final ordered list: streamer first, then shuffled others
            var finalUsernames = new List<string> { firstUsername };
            finalUsernames.AddRange(remainingUsernames);

            int index = 0;
            int renamed = 0;

            foreach (var pawn in raidPawns)
            {
                if (index >= finalUsernames.Count) break;

                string twitchName = finalUsernames[index++];

                // Only change the middle name (nickname) — keep original First and Last
                if (pawn.Name is NameTriple triple)
                {
                    pawn.Name = new NameTriple(triple.First, twitchName, triple.Last);
                }
                else
                {
                    // Fallback for pawns without a triple name
                    pawn.Name = new NameSingle(twitchName);
                }

                renamed++;

                // Optional thought (safely skipped if not defined)
                if (pawn.needs?.mood?.thoughts?.memories != null)
                {
                    var thoughtDef = ThoughtDef.Named("RaidFromTwitch");
                    if (thoughtDef != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef, null);
                    }
                }

                Logger.Twitch($"Renamed raid pawn → @{twitchName} (middle name only)");
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
            var research = Find.ResearchManager;
            TechLevel highest = TechLevel.Neolithic;

            foreach (var proj in DefDatabase<ResearchProjectDef>.AllDefs)
            {
                if (proj.IsFinished && proj.techLevel > highest)
                {
                    highest = proj.techLevel;
                }
            }

            Logger.Twitch($"Colony tech level detected: {highest}");
            return highest;
        }
    }
}