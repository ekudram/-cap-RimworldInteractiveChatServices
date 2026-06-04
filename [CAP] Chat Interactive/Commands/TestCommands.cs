// TestCommands.cs
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
// A simple test command that responds with a greeting message
using System;
using Verse;

namespace CAP_ChatInteractive.Commands.TestCommands
{
    public class Hello : ChatCommand
    {
        public override string Name =>  "hello";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            return $"Hello {messageWrapper.Username}! Thanks for testing the chat system! 🎉";
        }
    }

    public class CaptoLamia : ChatCommand
    {
        public override string Name => "CaptoLamia";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            var globalChatSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            // Check if the user is you by username AND platform ID
            bool isCaptoLamia = messageWrapper.Username == "captolamia" &&
                               messageWrapper.PlatformUserId == "58513264" &&
                               messageWrapper.Platform.ToLowerInvariant() == "twitch";

            if (!isCaptoLamia)
            {
                // RICS tips 
                var tips = new[]
                {
                    "You can have multiple Rimworld Lockers",
                    "Rimworld Lockers have settings so you can have Lockers in different areas receiving specific items.",
                    "!mypawn weapon will give you weapon stats for the weapon your pawn is currently holding.",
                    "!pricecheck [item name] [quality] [material] [quantity] will give you the market value of that item.",
                    "!storage [item name] will show you the colony inventory of specific items.",
                    "!weather [weather] will change in the colony",
                    "!karmasettings will show you your current karma settings.",
                    "!purchaselist will tell you the streamers Github link where they have a list of items they want to be purchased for them in the game.",
                    "!help will show you a wiki link to the list of available commands and how to use them.",
                    "!modversion will show you the current version of RICS that is running.",
                    "!study will show you the current anomaly research project being worked on in the colony.",
                    "!research will show you the current research project being worked on in the colony.",
                    "!research [project name] will show you the progress of that research project in the colony.",
                    "To buy a pawn you have to at least select a race.  Example: !pawn human [xenotype] [age] [m/f gender].  Xenotype, age and Gender are optional.",
                    "Events from mods are off by default.  If you want to enable them, you can do so in the RICS settings.",
                    "Anomoly events are off by default.  If you want to enable them, you can do so in the RICS settings.",
                    "!races will list all the races that are available to be bought with the !pawn command.",
                    "Did you know you can add an alias to a command in the Command Editor?  This allows you to have multiple ways to call the same command. For example, you could add an alias of `!bald` to `!bal` so both commands work the same way.  This can also be used to add translations for commands in other languages.  For example, you could add `!dar` as an alias for `!raid` for Spanish speakers.",
                    "The Rimazon Locker has settings so you can set up multiple lockers in different areas of the map and have them receive specific items.  For example, you could have a locker in the freezer that only receives food and medicine, and a locker in the workshop that only receives weapons and apparel.",
                    "The `!dye` command can be used to change hair color.  Example !dye hair blue will change your pawns hair color to blue.",
                    "Many purchase use fuzzy logic in their item matching.  This means that you don't have to type the exact name of an item for it to be recognized.  For example, !event soothe will buy !event psychic soothe.",
                    "Meow!",
                    "Good prompt engineering is basically \"How do I speak to a superintelligent alien who has no cultural context and takes everything literally?\" - Grok",
                    "Ok so the one thing I learned with AI is to be direct. And the english language is if full of ambiquity. -- Captolamia",


                    // Add more tips here later - they will be picked randomly
                };

                string randomTip = tips[Rand.Range(0, tips.Length)];
                return $"RICS Tip: {randomTip}";
            }

            return $"😸 Hello {messageWrapper.DisplayName}! RICS {globalChatSettings.modVersion}. The MOD Developer is present in chat! ";
        }
    }
}