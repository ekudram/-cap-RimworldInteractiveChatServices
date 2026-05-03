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

// Source/RICS/Debug/TwitchRaidDebug.cs
using CAP_ChatInteractive.Incidents;
using LudeonTK;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Debug
{
    public static class TwitchRaidDebug
    {
        [DebugAction("CAP", "Trigger Fake Twitch Raid (Join Window)", allowedGameStates = AllowedGameStates.Playing)]
        public static void TriggerFakeTwitchRaidWithJoin()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
            {
                Messages.Message("Must be in a playing game with a map open.", MessageTypeDefOf.RejectInput);
                return;
            }

            var twitchService = CAPChatInteractiveMod.Instance.TwitchService;
            if (twitchService == null)
            {
                Messages.Message("TwitchService not found.", MessageTypeDefOf.NegativeEvent);
                return;
            }

            // This will open the dialog window and start the 45-second join period
            twitchService.StartRaidJoinCollection("CaptoLamia", 10);

            // Add 10 fake usernames (Twitch Raider 2-10)
            for (int i = 2; i <= 10; i++)
            {
                twitchService.ProcessUserJoined($"TwitchRaider{i}");
            }
            Messages.Message("Fake Twitch Raid join window opened!\nType !joinraid in chat or click 'START RAID NOW!'", MessageTypeDefOf.PositiveEvent);
        }

        [DebugAction("CAP", "Clear Twitch Raider Names", allowedGameStates = AllowedGameStates.Playing)]
        public static void ClearTwitchRaiderNames()
        {
            IncidentWorker_TwitchRaid.CurrentRaidUsernames.Clear();
            Messages.Message("Twitch raider name list cleared.", MessageTypeDefOf.SilentInput);
        }
    }
}