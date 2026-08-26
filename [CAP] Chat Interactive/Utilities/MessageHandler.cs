// MessageHandler.cs
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
// Utility class for sending in-game letters/notifications
using CAP_ChatInteractive.Utilities;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    // COLOR GUIDE:
    // 🟢 GREEN  - Positive chat events, aid, reinforcements
    // 🔵 BLUE   - Medical, healing, rescue, recovery  
    // 🟡 GOLD   - Special events, large rewards, unique occurrences
    // 🟣 PINK   - Relationships, diplomacy, social events
    // Military Aid - Green (already implemented)
    //MessageHandler.SendGreenLetter($"Military Aid by {user.Username}", message);

    //// Future medical commands - Blue  
    //MessageHandler.SendBlueLetter($"Medical Aid by {user.Username}", "Healing supplies have been delivered!");

    //// Large purchases - Gold
    //if (wager >= 5000) 
    //{
    //    MessageHandler.SendGoldLetter($"Major Purchase by {user.Username}", $"{user.Username} made a major investment!");
    //}

    //// Diplomatic events - Pink
    //MessageHandler.SendPinkLetter($"Diplomatic Gift from {user.Username}", "Faction relations have improved!");
public static class MessageHandler
    {
        public static void SendSuccessLetter(string label, string message)
        {
            SendCustomLetter(label, message, LetterDefOf.PositiveEvent);
        }

        public static void SendFailureLetter(string label, string message)
        {
            SendCustomLetter(label, message, LetterDefOf.NegativeEvent);
        }

        public static void SendInfoLetter(string label, string message)
        {
            SendCustomLetter(label, message, LetterDefOf.NeutralEvent);
        }

        public static void SendCustomLetter(string label, string message, LetterDef letterDef, LookTargets lookTargets = null)
        {
            if (Current.Game == null || Find.LetterStack == null)
            {
                Logger.Warning($"Cannot send letter - game not ready: {label} - {message}");
                return;
            }

            try
            {
                // Strip NUL / illegal XML control chars — they break save when archived
                label = XmlTextSanitizer.Sanitize(label) ?? string.Empty;
                message = XmlTextSanitizer.Sanitize(message) ?? string.Empty;

                Letter letter = LetterMaker.MakeLetter(label, message, letterDef, lookTargets);
                Find.LetterStack.ReceiveLetter(letter);
            }
            catch (System.Exception ex)
            {
                Logger.Error($"[MessageHandler] Error sending letter: {ex.Message}");
            }
        }

        /// <summary>
        /// Blue for medical/rescue aid/heal/helpful events/revivals/etc.
        /// </summary>
        public static void SendBlueLetter(string label, string message, LookTargets lookTargets = null)
        {
            var blueLetter = DefDatabase<LetterDef>.GetNamedSilentFail("BlueLetter");
            SendCustomLetter(label, message, blueLetter ?? LetterDefOf.NeutralEvent, lookTargets);
        }

        /// <summary>
        /// Green Letter for positive events from chat interactions
        /// </summary>
        public static void SendGreenLetter(string label, string message, LookTargets lookTargets = null)
        {
            var greenLetter = DefDatabase<LetterDef>.GetNamedSilentFail("GreenLetter");
            SendCustomLetter(label, message, greenLetter ?? LetterDefOf.PositiveEvent, lookTargets);
        }

        /// <summary>
        /// For gold/special events from chat interactions like large rewards, unique occurrences, large buyables, etc.
        /// </summary>
        public static void SendGoldLetter(string label, string message, LookTargets lookTargets = null)
        {
            var goldLetter = DefDatabase<LetterDef>.GetNamedSilentFail("GoldLetter");
            SendCustomLetter(label, message, goldLetter ?? LetterDefOf.PositiveEvent, lookTargets);
        }

        /// <summary>
        /// For relationship/diplomatic events from chat interactions
        /// </summary>
        public static void SendPinkLetter(string label, string message, LookTargets lookTargets = null)
        {
            
            var pinkLetter = DefDatabase<LetterDef>.GetNamedSilentFail("PinkLetter");
            SendCustomLetter(label, message, pinkLetter ?? LetterDefOf.NeutralEvent, lookTargets);
        }
    }
}