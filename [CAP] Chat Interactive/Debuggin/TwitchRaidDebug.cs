// Source/RICS/Debug/TwitchRaidDebug.cs
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

using CAP_ChatInteractive.Incidents;
using LudeonTK;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace CAP_ChatInteractive.Debug
{
    public static class TwitchRaidDebug
    {
        [DebugAction("CAP", "Trigger Fake Twitch Raid (10 raiders)", allowedGameStates = AllowedGameStates.Playing)]
        public static void TriggerFakeTwitchRaid()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            {
                Messages.Message("Must be in a playing game with a map open.", MessageTypeDefOf.RejectInput);
                return;
            }

            // Clear any old list
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();

            // Add 10 fake usernames (CaptoLamia + Twitch Raider 2-10)
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Add("CaptoLamia");
            for (int i = 2; i <= 10; i++)
            {
                IncidentWorker_TwitchRaid.CurrentRaidUsernames.Add($"TwitchRaider{i}");
            }

            // Trigger the raid using our custom worker
            var worker = new IncidentWorker_TwitchRaid
            {
                def = IncidentDefOf.RaidEnemy
            };

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
            parms.forced = true;
            parms.points = StorytellerUtility.DefaultThreatPointsNow(Find.CurrentMap) * 1.5f;

            bool success = worker.TryExecute(parms);

            if (success)
            {
                Messages.Message("Fake Twitch Raid triggered with 10 named raiders!", MessageTypeDefOf.PositiveEvent);
                Logger.Twitch("DEBUG: Fake Twitch Raid with 10 named raiders started via debug action.");
            }
            else
            {
                Messages.Message("Failed to trigger fake Twitch Raid.", MessageTypeDefOf.NegativeEvent);
            }
        }

        [DebugAction("CAP", "Clear Twitch Raider Names", allowedGameStates = AllowedGameStates.Playing)]
        public static void ClearTwitchRaiderNames()
        {
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            Messages.Message("Twitch raider name list cleared.", MessageTypeDefOf.SilentInput);
        }
    }
}