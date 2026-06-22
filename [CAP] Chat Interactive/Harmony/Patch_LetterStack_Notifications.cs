// Patch_LetterStack_Notifications.cs
// Copyright (c) Captolamia
// Part of RICS (Rimworld Interactive Chat Services) — AGPLv3

using HarmonyLib;
using Verse;

namespace CAP_ChatInteractive.AI
{
    /// <summary>
    /// Postfix on LetterStack.ReceiveLetter so we can notify the external AI ChatBot
    /// whenever any letter is shown to the player (storyteller incidents + viewer events).
    /// This is the single best central hook for "something happened in the colony".
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

                // Skip letters that shouldn't be shown (safety)
                if (let == null || !let.CanShowInLetterStack)
                    return;

                // Build a clean notification message starting with the bot's name
                string botName = settings.AIChatBotName ?? "Masie";
                string title = let.Label.ToString() ?? let.def?.label ?? "Event";

                string body = "";
                if (let is ChoiceLetter choiceLetter && !choiceLetter.Text.NullOrEmpty())
                {
                    body = ChatCommandProcessor.RemoveMarkupTags(choiceLetter.Text.ToString());
                    // Keep it reasonably short for the bot context window
                    if (body.Length > 2000)
                        body = body.Substring(0, 2000) + "...";
                }

                string notification = $"{botName} this has occurred in the colony: {title}.";
                if (!string.IsNullOrWhiteSpace(body))
                    notification += $" {body}";

                // Get the service from the current game (reliable after game start)
                var gameComp = Current.Game?.GetComponent<CAPChatInteractive_GameComponent>();
                gameComp?._aiChatBotService?.NotifyColonyEvent(notification);
            }
            catch (System.Exception ex)
            {
                Logger.Warning($"[RICS AI] Letter notification postfix failed (non-fatal): {ex.Message}");
            }
        }
    }
}