// TestCommands.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Hello + CaptoLamia tip / presence commands (random RICS & RimWorld quotes).
using Verse;

namespace CAP_ChatInteractive.Commands.TestCommands
{
    public class Hello : ChatCommand
    {
        public override string Name => "hello";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            string user = messageWrapper?.Username;
            if (string.IsNullOrEmpty(user))
                return "Hello! Thanks for testing the chat system!";
            return $"Hello {user}! Thanks for testing the chat system! 🎉";
        }
    }

    public class CaptoLamia : ChatCommand
    {
        public override string Name => "CaptoLamia";

        public override string Execute(ChatMessageWrapper messageWrapper, string[] args)
        {
            bool isCaptoLamia =
                messageWrapper != null
                && string.Equals(messageWrapper.Username, "captolamia", System.StringComparison.OrdinalIgnoreCase)
                && messageWrapper.PlatformUserId == "58513264"
                && string.Equals(messageWrapper.Platform, "twitch", System.StringComparison.OrdinalIgnoreCase);

            if (isCaptoLamia)
            {
                string version = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.modVersion ?? "?";
                string display = !string.IsNullOrEmpty(messageWrapper.DisplayName)
                    ? messageWrapper.DisplayName
                    : messageWrapper.Username;
                return $"😸 Hello {display}! RICS {version}. The mod developer is present in chat!";
            }

            string tip = Tips[Rand.Range(0, Tips.Length)];
            return $"RICS Tip: {tip}";
        }

        /// <summary>Useful RICS tips, RimWorld flavor, and a few dad jokes. Picked at random.</summary>
        private static readonly string[] Tips =
        {
            // ── Useful RICS ───────────────────────────────────────────
            "You can place multiple Rimazon lockers on the map.",
            "Rimazon lockers have settings so different areas can receive specific items.",
            "!mypawn weapon shows stats for the weapon your pawn is holding.",
            "!pricecheck [item] [quality] [material] [qty] shows market value.",
            "!storage [item] shows colony stock of that item.",
            "!weather [type] sets colony weather (when allowed).",
            "!karmasettings shows your current karma settings.",
            "!purchaselist links the streamer's GitHub wish list (if set).",
            "!help opens the wiki for available commands.",
            "!modversion shows the running RICS version.",
            "!study shows the current Anomaly research project.",
            "!research shows the colony's current research project.",
            "!research [name] shows progress on that research project.",
            "Buy a pawn: !pawn, or !pawn human [xenotype] [age] [m/f]. Bare !pawn buys if only one race is on.",
            "!races lists races available for !pawn.",
            "Modded events are off by default — enable them in RICS settings.",
            "Anomaly events are off by default — enable them in RICS settings.",
            "Command Editor: add aliases so one command has many names (or translations).",
            "Example alias: !bald → !bal, or !dar → !raid for Spanish chat.",
            "Freezer locker for food/medicine, workshop locker for weapons — filter per locker.",
            "!dye hair blue recolors your pawn's hair (when dye is set up).",
            "Purchases use fuzzy matching: !event soothe can find Psychic Soothe.",
            "!event psychic soothe triggers that event (if enabled and affordable).",

            // ── Personality ───────────────────────────────────────────
            "Meow!",
            "LenzaRNG is a Pretty Princess!",
            "LenzaRNG, KillerKeo, and JennaDorDor helped test RICS — go watch them stream!",
            "Good prompt engineering: speak to a superintelligent alien who takes everything literally. — Grok",

            // ── RimWorld flavor ───────────────────────────────────────
            "Randy is not a weather report.",
            "Mental break? That's just passion with extra steps.",
            "The chicken is plotting. Trust the chicken.",
            "Steel is temporary. Mountain bases are forever.",
            "Never trust a quiet map. The insects are studying.",
            "Beauty is optional. Cover is not.",
            "If the pawn is happy, the colony lives. If not… wealth redistribution.",

            // ── Dad jokes / puns ──────────────────────────────────────
            "Why did the colonist bring a ladder to the mountain base? To rise above the raids.",
            "I told my pawn a chemistry joke — they had no reaction.",
            "What do you call a rich raider? A wealth redistribution event.",
            "My furniture has feelings. It's a mood-link thing.",
            "Why don't raiders play cards in a siege? Too many cheats and too little cover.",
            "I asked Randy for mercy. He sent a trade caravan. With a siege.",
        };
    }
}
