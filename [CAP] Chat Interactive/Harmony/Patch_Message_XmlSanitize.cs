// Patch_Message_XmlSanitize.cs
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
// Prevent NUL / illegal XML control characters in archived Messages (and letters)
// from aborting save: "'.', hexadecimal value 0x00, is an invalid character."
using CAP_ChatInteractive.Utilities;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.HarmonyPatches
{
    /// <summary>
    /// Sanitize toast text as it enters the message queue / history archive.
    /// </summary>
    [HarmonyPatch(typeof(Messages))]
    [HarmonyPatch(nameof(Messages.Message), new[] { typeof(Message), typeof(bool) })]
    public static class Patch_Messages_Message_XmlSanitize
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Message msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.text))
                return;

            if (XmlTextSanitizer.ContainsIllegalXmlChar(msg.text))
                msg.text = XmlTextSanitizer.Sanitize(msg.text);
        }
    }

    /// <summary>
    /// Last line of defense: strip illegal chars when a Message is written to the save.
    /// Cleans archive entries that already contain NUL from earlier sessions / other mods.
    /// </summary>
    [HarmonyPatch(typeof(Message), nameof(Message.ExposeData))]
    public static class Patch_Message_ExposeData_XmlSanitize
    {
        [HarmonyPrefix]
        public static void Prefix(Message __instance)
        {
            if (Scribe.mode != LoadSaveMode.Saving || __instance == null)
                return;

            if (!string.IsNullOrEmpty(__instance.text) &&
                XmlTextSanitizer.ContainsIllegalXmlChar(__instance.text))
            {
                __instance.text = XmlTextSanitizer.Sanitize(__instance.text);
            }
        }
    }

    /// <summary>
    /// Sanitize ChoiceLetter body when saving (letters also live in the Archive).
    /// </summary>
    [HarmonyPatch(typeof(ChoiceLetter), nameof(ChoiceLetter.ExposeData))]
    public static class Patch_ChoiceLetter_ExposeData_XmlSanitize
    {
        [HarmonyPrefix]
        public static void Prefix(ChoiceLetter __instance)
        {
            if (Scribe.mode != LoadSaveMode.Saving || __instance == null)
                return;

            try
            {
                string body = __instance.Text.ToString();
                if (string.IsNullOrEmpty(body) || !XmlTextSanitizer.ContainsIllegalXmlChar(body))
                    return;

                var field = AccessTools.Field(typeof(ChoiceLetter), "text");
                if (field != null)
                    field.SetValue(__instance, new TaggedString(XmlTextSanitizer.Sanitize(body)));
            }
            catch
            {
                // Never block save of the letter object itself
            }
        }
    }
}
