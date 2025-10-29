// MessageHandler.cs
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

        public static void SendCustomLetter(string label, string message, LetterDef letterDef)
        {
            if (Current.Game == null || Find.LetterStack == null)
            {
                Logger.Warning($"Cannot send letter - game not ready: {label} - {message}");
                return;
            }

            try
            {
                Find.LetterStack.ReceiveLetter(label, message, letterDef);
                Logger.Debug($"Sent letter: {label} - {message}");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error sending letter: {ex.Message}");
            }
        }
        /// <summary>
        /// Blue for medical/rescue aid/heal/helpful events/revivals/etc.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="message"></param>
        public static void SendBlueLetter(string label, string message)
        {
            var blueLetter = DefDatabase<LetterDef>.GetNamedSilentFail("BlueLetter");
            SendCustomLetter(label, message, blueLetter ?? LetterDefOf.NeutralEvent);
        }
        /// <summary>
        /// Green Letter for positive events from chat interactions
        /// </summary>
        /// <param name="label"></param>
        /// <param name="message"></param>
        public static void SendGreenLetter(string label, string message)
        {
            var greenLetter = DefDatabase<LetterDef>.GetNamedSilentFail("GreenLetter");
            SendCustomLetter(label, message, greenLetter ?? LetterDefOf.PositiveEvent);
        }
        /// <summary>
        /// For gold/special events from chat interactions like large rewards, unique occurrences, large buyables, etc.
        /// </summary>
        /// <param name="label"></param>
        /// <param name="message"></param>
        public static void SendGoldLetter(string label, string message)
        {
            var goldLetter = DefDatabase<LetterDef>.GetNamedSilentFail("GoldLetter");
            SendCustomLetter(label, message, goldLetter ?? LetterDefOf.PositiveEvent);
        }
        /// <summary>
        /// For relationship/diplomatic events from chat interactions
        /// </summary>
        /// <param name="label"></param>
        /// <param name="message"></param>
        public static void SendPinkLetter(string label, string message)
        {
            var pinkLetter = DefDatabase<LetterDef>.GetNamedSilentFail("PinkLetter");
            SendCustomLetter(label, message, pinkLetter ?? LetterDefOf.NeutralEvent);
        }
    }
}