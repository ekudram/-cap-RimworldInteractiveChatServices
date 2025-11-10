// MessageHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Utility class for sending in-game letters/notifications
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
                // Create letter with look targets if provided
                Letter letter = LetterMaker.MakeLetter(label, message, letterDef, lookTargets);
                Find.LetterStack.ReceiveLetter(letter);
                Logger.Debug($"Sent letter: {label} - {message} with lookTargets: {lookTargets != null}");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error sending letter: {ex.Message}");
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