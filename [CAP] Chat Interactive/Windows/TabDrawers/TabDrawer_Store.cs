// TabDrawer_Store.cs
using CAP_ChatInteractive;
using CAP_ChatInteractive.Store; // Add this line
using System.Linq;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive   
{
    public static class TabDrawer_Store
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
            listing.Label("Store Management");
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            // Description
            listing.Label("Manage all items available in the chat store. Set prices, enable/disable items, and organize by categories.");
            listing.Gap(12f);

            // Store Statistics
            int totalItems = CAP_ChatInteractive.Store.StoreInventory.AllStoreItems.Count;
            int enabledItems = CAP_ChatInteractive.Store.StoreInventory.GetEnabledItems().Count();
            int disabledItems = totalItems - enabledItems;

            listing.Label($"Store Statistics:");
            Text.Font = GameFont.Tiny;
            listing.Label($"  • Total Items: {totalItems}");
            listing.Label($"  • Enabled: {enabledItems}");
            listing.Label($"  • Disabled: {disabledItems}");
            Text.Font = GameFont.Small;
            listing.Gap(12f);

            // Open Store Editor Button
            bool gameLoaded = Current.ProgramState == ProgramState.Playing;
            Rect buttonRect = listing.GetRect(30f);

            if (!gameLoaded)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(buttonRect, "Open Store Editor");
                GUI.color = Color.white;

                Rect warningRect = listing.GetRect(Text.LineHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(warningRect, "Store editor requires a loaded game");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                if (Widgets.ButtonText(buttonRect, "Open Store Editor"))
                {
                    Find.WindowStack.Add(new Dialog_StoreEditor());
                }
            }

            listing.Gap(12f);

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
                        "Reset all item prices to their default values?",
                        () =>
                        {
                            foreach (var item in CAP_ChatInteractive.Store.StoreInventory.AllStoreItems.Values)
                            {
                                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(item.DefName);
                                if (thingDef != null)
                                {
                                    item.BasePrice = (int)(thingDef.BaseMarketValue * 1.67f);
                                }
                            }
                            CAP_ChatInteractive.Store.StoreInventory.SaveStoreToJson();
                        }
                    ));
                }

                // Enable All Items - with label on left  
                Rect enableRow = listing.GetRect(30f);
                Rect enableLabelRect = enableRow.LeftPart(0.7f).Rounded();
                Rect enableButtonRect = enableRow.RightPart(0.3f).Rounded();

                Widgets.Label(enableLabelRect, "Enable all items");
                if (Widgets.ButtonText(enableButtonRect, "Enable All"))
                {
                    foreach (var item in CAP_ChatInteractive.Store.StoreInventory.AllStoreItems.Values)
                    {
                        item.Enabled = true;
                    }
                    CAP_ChatInteractive.Store.StoreInventory.SaveStoreToJson();
                }

                // Disable All Items - with label on left
                Rect disableRow = listing.GetRect(30f);
                Rect disableLabelRect = disableRow.LeftPart(0.7f).Rounded();
                Rect disableButtonRect = disableRow.RightPart(0.3f).Rounded();

                Widgets.Label(disableLabelRect, "Disable all items");
                if (Widgets.ButtonText(disableButtonRect, "Disable All"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Disable all items from the store?",
                        () =>
                        {
                            foreach (var item in CAP_ChatInteractive.Store.StoreInventory.AllStoreItems.Values)
                            {
                                item.Enabled = false;
                            }
                            CAP_ChatInteractive.Store.StoreInventory.SaveStoreToJson();
                        }
                    ));
                }
            }

            listing.End();
            Widgets.EndScrollView();

        }
    }
}