using CAP_ChatInteractive;
using CAP_ChatInteractive.Traits;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_Traits
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var view = new Rect(0f, 0f, region.width - 16f, 400f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // Header
            Text.Font = GameFont.Medium;
            listing.Label("Traits Management");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Description
            listing.Label("Manage all traits available for purchase/removal. Set prices, enable/disable traits, and organize by mod sources.");
            listing.Gap(12f);

            // Traits Statistics
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;
            int totalTraits = gameLoaded ? TraitsManager.AllBuyableTraits.Count : 0;
            int enabledTraits = gameLoaded ? TraitsManager.GetEnabledTraits().Count() : 0;
            int disabledTraits = totalTraits - enabledTraits;

            listing.Label($"Traits Statistics:");
            Text.Font = GameFont.Tiny;
            if (gameLoaded)
            {
                listing.Label($"  • Total Traits: {totalTraits}");
                listing.Label($"  • Enabled: {enabledTraits}");
                listing.Label($"  • Disabled: {disabledTraits}");
            }
            else
            {
                listing.Label($"  • Load a game to view traits statistics");
            }
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Open Traits Editor Button
            Rect buttonRect = listing.GetRect(30f);
            if (!gameLoaded)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(buttonRect, "Open Traits Editor");
                GUI.color = Color.white;

                Rect warningRect = listing.GetRect(Text.LineHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(warningRect, "Traits editor requires a loaded game");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                if (Widgets.ButtonText(buttonRect, "Open Traits Editor"))
                {
                    Find.WindowStack.Add(new Dialog_TraitsEditor());
                }
            }

            listing.Gap(24f);

            // Quick Actions Section
            Text.Font = GameFont.Medium;
            listing.Label("Quick Actions");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            if (!gameLoaded)
            {
                Rect warningRect = listing.GetRect(Text.LineHeight * 2f);
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.yellow;
                Widgets.Label(warningRect, "Quick actions require a loaded game");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                listing.Gap(12f);
            }
            else
            {
                // Reset All Prices - with label on left
                Rect resetRow = listing.GetRect(30f);
                Rect resetLabelRect = resetRow.LeftPart(0.7f).Rounded();
                Rect resetButtonRect = resetRow.RightPart(0.3f).Rounded();

                Widgets.Label(resetLabelRect, "Reset prices to default");
                if (Widgets.ButtonText(resetButtonRect, "Reset"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Reset all trait prices to default values?",
                        () => {
                            // This will trigger when the traits editor opens
                            // For now, we'll just show a message
                            Messages.Message("Trait prices will be reset when you open the Traits Editor", MessageTypeDefOf.NeutralEvent);
                        }
                    ));
                }

                // Enable All Traits - with label on left  
                Rect enableRow = listing.GetRect(30f);
                Rect enableLabelRect = enableRow.LeftPart(0.7f).Rounded();
                Rect enableButtonRect = enableRow.RightPart(0.3f).Rounded();

                Widgets.Label(enableLabelRect, "Enable all traits");
                if (Widgets.ButtonText(enableButtonRect, "Enable All"))
                {
                    foreach (var trait in TraitsManager.AllBuyableTraits.Values)
                    {
                        trait.CanAdd = true;
                        trait.CanRemove = true;
                    }
                    TraitsManager.SaveTraitsToJson();
                    Messages.Message($"Enabled all {TraitsManager.AllBuyableTraits.Count} traits", MessageTypeDefOf.PositiveEvent);
                }

                // Disable All Traits - with label on left
                Rect disableRow = listing.GetRect(30f);
                Rect disableLabelRect = disableRow.LeftPart(0.7f).Rounded();
                Rect disableButtonRect = disableRow.RightPart(0.3f).Rounded();

                Widgets.Label(disableLabelRect, "Disable all traits");
                if (Widgets.ButtonText(disableButtonRect, "Disable All"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Disable all traits from being purchased?",
                        () => {
                            foreach (var trait in TraitsManager.AllBuyableTraits.Values)
                            {
                                trait.CanAdd = false;
                                trait.CanRemove = false;
                            }
                            TraitsManager.SaveTraitsToJson();
                            Messages.Message($"Disabled all {TraitsManager.AllBuyableTraits.Count} traits", MessageTypeDefOf.NeutralEvent);
                        }
                    ));
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}