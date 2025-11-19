// Dialog_ViewerManager.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A dialog window for managing viewers in the chat interactive system
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_ViewerManager : Window
    {
        private Vector2 viewerScrollPosition = Vector2.zero;
        private Vector2 detailsScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private ViewerSortMethod sortMethod = ViewerSortMethod.Username;
        private bool sortAscending = true;
        private Viewer selectedViewer = null;
        private List<Viewer> filteredViewers = new List<Viewer>();

        // Ban confirmation
        private bool showBanConfirmation = false;
        private string banConfirmationMessage = "";
        private bool showRemoveConfirmation = false;

        // Mass action confirmations
        private bool showResetCoinsConfirmation = false;
        private bool showResetKarmaConfirmation = false;
        private bool showAwardCoinsConfirmation = false;

        private int coinsEditAmount = 0;
        private string coinsEditBuffer = "0";
        private int karmaEditAmount = 0;
        private string karmaEditBuffer = "0";

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Dialog_ViewerManager()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Viewer Management";

            FilterViewers();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Update search if query changed
            if (searchQuery != lastSearch || filteredViewers.Count == 0)
            {
                FilterViewers();
            }

            // Header
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f); // Increased from 40f to 70f
            DrawHeader(headerRect);

            // Main content area
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - CloseButSize.y);
            DrawContent(contentRect);

            // Handle confirmations
            HandleConfirmations();
        }

        public override void PostClose()
        {
            base.PostClose();

            Viewers.SaveViewers();
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Custom title with larger font and underline effect - matching Store Editor
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.Orange;
            Rect titleRect = new Rect(0f, 0f, 400f, 35f);
            string titleText = $"Viewer Management - Viewers ({Viewers.All.Count})";
            if (filteredViewers.Count != Viewers.All.Count)
                titleText += $" - Filtered: {filteredViewers.Count}";

            // Draw title
            Widgets.Label(titleRect, titleText);

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - positioned below the title
            float controlsY = titleRect.yMax + 5f;
            float controlsHeight = 30f;

            // Search bar with label - matching Store Editor
            Rect searchLabelRect = new Rect(0f, controlsY, 80f, controlsHeight);
            Text.Font = GameFont.Medium; // Medium font for the label
            Widgets.Label(searchLabelRect, "Search:");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(85f, controlsY, 250f, controlsHeight);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons - adjusted position
            Rect sortRect = new Rect(345f, controlsY, 300f, controlsHeight);
            DrawSortButtons(sortRect);

            // Mass action button - adjusted position
            float buttonWidth = 110f;
            Rect actionsRect = new Rect(rect.width - buttonWidth, controlsY, buttonWidth, 30f);
            if (Widgets.ButtonText(actionsRect, "Mass Actions →"))
            {
                ShowMassActionsMenu();
            }

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            // Sort by Username
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Username"))
            {
                if (sortMethod == ViewerSortMethod.Username)
                    sortAscending = !sortAscending;
                else
                    sortMethod = ViewerSortMethod.Username;
                SortViewers();
            }
            x += buttonWidth + spacing;

            // Sort by Coins
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Coins"))
            {
                if (sortMethod == ViewerSortMethod.Coins)
                    sortAscending = !sortAscending;
                else
                    sortMethod = ViewerSortMethod.Coins;
                SortViewers();
            }
            x += buttonWidth + spacing;

            // Sort by Karma
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Karma"))
            {
                if (sortMethod == ViewerSortMethod.Karma)
                    sortAscending = !sortAscending;
                else
                    sortMethod = ViewerSortMethod.Karma;
                SortViewers();
            }

            Widgets.EndGroup();
        }
        // fix this to force order
        private void ShowMassActionsMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Award Coins to Active", () => {
                    showAwardCoinsConfirmation = true;
                }),
                new FloatMenuOption("Reset All Coins", () => {
                    showResetCoinsConfirmation = true;
                }),
                new FloatMenuOption("Reset All Karma", () => {
                    showResetKarmaConfirmation = true;
                }),
                new FloatMenuOption("---", null), // Separator
                new FloatMenuOption("View Statistics", ShowStatistics)
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawContent(Rect rect)
        {
            // Make viewer list narrower like in TraitsEditor
            float listWidth = 250f; // Reduced from 300f
            float detailsWidth = rect.width - listWidth - 10f;

            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect detailsRect = new Rect(rect.x + listWidth + 10f, rect.y, detailsWidth, rect.height);

            DrawViewerList(listRect);
            DrawViewerDetails(detailsRect);
        }

        private void DrawViewerList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header - CENTERED
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter; // Add this line
            Widgets.Label(headerRect, "Viewers");
            Text.Anchor = TextAnchor.UpperLeft; // Reset to default
            Text.Font = GameFont.Small;

            // Viewer list - Match Traits approach exactly
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 35f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredViewers.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref viewerScrollPosition, viewRect);
            {
                // REMOVED: Widgets.BeginGroup - Traits doesn't use it

                float y = 0f;
                for (int i = 0; i < filteredViewers.Count; i++)
                {
                    // Draw directly like Traits does - no separate method call
                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Capitalize first letter of username
                    string displayName = CapitalizeFirst(filteredViewers[i].Username);

                    // Apply role coloring and highlight if selected
                    Color buttonColor = GetViewerRoleColor(filteredViewers[i]);
                    bool isSelected = selectedViewer == filteredViewers[i];

                    // Use different style for selected vs normal
                    if (isSelected)
                    {
                        GUI.color = buttonColor * 1.3f;
                    }
                    else
                    {
                        GUI.color = buttonColor;
                    }

                    if (Widgets.ButtonText(buttonRect, displayName))
                    {
                        selectedViewer = filteredViewers[i];
                    }
                    GUI.color = Color.white;

                    y += rowHeight;
                }

                // REMOVED: Widgets.EndGroup
            }
            Widgets.EndScrollView();
        }

        private string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
        }

        private Color GetViewerRoleColor(Viewer viewer)
        {
            if (viewer.IsBanned) return Color.red;
            if (viewer.IsBroadcaster) return new Color(0.9f, 0.3f, 0.3f);
            if (viewer.IsModerator) return new Color(0.2f, 0.8f, 0.2f);
            if (viewer.IsVip) return new Color(0.8f, 0.6f, 0.2f);
            if (viewer.IsSubscriber) return new Color(0.4f, 0.6f, 1f);
            return Color.white;
        }

        private void DrawViewerDetails(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            if (selectedViewer == null)
            {
                // No viewer selected message
                Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(messageRect, "Select a viewer to see details");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Header with username
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            string headerText = CapitalizeFirst(selectedViewer.Username);
            if (selectedViewer.IsBanned) headerText += " 🚫 BANNED";
            else if (selectedViewer.IsBroadcaster) headerText += " ⭐ BROADCASTER";
            else if (selectedViewer.IsModerator) headerText += " 🛡️ MODERATOR";
            else if (selectedViewer.IsVip) headerText += " 💎 VIP";
            else if (selectedViewer.IsSubscriber) headerText += " 🔔 SUBSCRIBER";

            Widgets.Label(headerRect, headerText);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Details content with scrolling
            Rect contentRect = new Rect(rect.x, rect.y + 50f, rect.width, rect.height - 60f);
            DrawViewerDetailsContent(contentRect);
        }

        private void DrawViewerDetailsContent(Rect rect)
        {
            float contentWidth = rect.width - 30f; // More padding on the right
            float viewHeight = CalculateDetailsHeight(contentWidth);
            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(viewHeight, rect.height)); // Start at 0,0

            Widgets.BeginScrollView(rect, ref detailsScrollPosition, viewRect);
            {
                float y = 0f;
                float sectionHeight = 28f;
                float leftPadding = 15f; // Consistent left padding for all content

                // Pawn Assignment Section - ADD THIS NEW SECTION
                if (Current.Game != null)
                {
                    DrawPawnAssignmentSection(ref y, viewRect.width, leftPadding);
                    y += 20f;
                }

                // Platform IDs section
                Rect platformLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                string platformLabel = "Platform IDs:";
                if (selectedViewer.PlatformUserIds.Count == 0)
                {
                    platformLabel += " ⚠ NO PLATFORM IDs (User may be invalid)";
                    GUI.color = Color.yellow;
                }
                Widgets.Label(platformLabelRect, platformLabel);
                GUI.color = Color.white;
                y += sectionHeight;

                if (selectedViewer.PlatformUserIds.Count > 0)
                {
                    foreach (var platformId in selectedViewer.PlatformUserIds)
                    {
                        Rect platformRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);

                        // Show which platform ID is being used for pawn assignment
                        string platformText = $"{platformId.Key}: {platformId.Value}";
                        var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
                        if (assignmentManager != null)
                        {
                            // Check if this specific platform ID has a pawn assignment
                            string testIdentifier = $"{platformId.Key}:{platformId.Value}";
                            if (assignmentManager.viewerPawnAssignments.ContainsKey(testIdentifier))
                            {
                                platformText += " ✓ (Active Assignment)";
                            }
                        }

                        Widgets.Label(platformRect, platformText);
                        y += sectionHeight - 5f;
                    }
                }
                else
                {
                    Rect noPlatformsRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(noPlatformsRect, "No platform IDs - using username fallback");
                    y += sectionHeight - 5f;
                }
                y += 10f;

                // Economy section
                Rect economyLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(economyLabelRect, "Economy:");
                y += sectionHeight;

                // Coins row
                DrawEconomyRow(ref y, viewRect.width, "Coins", selectedViewer.Coins,
                    ref coinsEditAmount, ref coinsEditBuffer,
                    (amount) => { selectedViewer.SetCoins(amount); Viewers.SaveViewers(); },
                    0, int.MaxValue, leftPadding);

                y += 10f;

                // Karma row
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                DrawEconomyRow(ref y, viewRect.width, "Karma", selectedViewer.Karma,
                    ref karmaEditAmount, ref karmaEditBuffer,
                    (amount) => {
                        // Enforce karma limits from settings
                        int clampedAmount = Mathf.Clamp(amount, settings.MinKarma, settings.MaxKarma);
                        selectedViewer.SetKarma(clampedAmount);
                        Viewers.SaveViewers();
                    },
                    settings.MinKarma, settings.MaxKarma, leftPadding);

                y += 20f;

                // Activity section
                Rect activityLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(activityLabelRect, "Activity:");
                y += sectionHeight;

                Rect messagesRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                Widgets.Label(messagesRect, $"Messages: {selectedViewer.MessageCount}");
                y += sectionHeight;

                Rect lastSeenRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                string lastSeenText = $"Last Seen: {selectedViewer.LastSeen:g}";
                if (selectedViewer.IsActive())
                    lastSeenText += " (Active Now)";
                else
                    lastSeenText += $" ({selectedViewer.GetTimeSinceLastActivity().TotalMinutes:F0} minutes ago)";
                Widgets.Label(lastSeenRect, lastSeenText);
                y += sectionHeight + 20f;

                // Ban hammer section - centered but with proper container
                Rect banRect = new Rect(leftPadding, y, viewRect.width - (leftPadding * 2), sectionHeight * 2f);
                DrawBanSection(banRect);
            }
            Widgets.EndScrollView();
        }

        private void DrawPawnAssignmentSection(ref float y, float width, float leftPadding)
        {
            float sectionHeight = 28f;
            float pawnPortraitSize = 80f; // Size for the pawn portrait

            // Section header
            Rect pawnHeaderRect = new Rect(leftPadding, y, width, sectionHeight);
            Widgets.Label(pawnHeaderRect, "Assigned Pawn:");
            y += sectionHeight;
            Logger.Debug($"Before: GetViewerPawn result for {selectedViewer.Username}:");
            Pawn viewerPawn = GetViewerPawn(selectedViewer);

            // DEBUG
            Logger.Debug($"After: GetViewerPawn result for {selectedViewer.Username}:");
            Logger.Debug($"  - viewerPawn is null: {viewerPawn == null}");
            if (viewerPawn != null)
            {
                Logger.Debug($"  - Pawn Name: {viewerPawn.Name}");
                Logger.Debug($"  - Pawn ThingID: {viewerPawn.ThingID}");
                Logger.Debug($"  - Pawn Dead: {viewerPawn.Dead}");
                Logger.Debug($"  - Pawn Spawned: {viewerPawn.Spawned}");
                Logger.Debug($"  - Pawn Map: {viewerPawn.Map?.Parent?.Label ?? "None"}");
            }
            else
            {
                // Also debug what identifier we're looking for
                string platformId = selectedViewer.GetPrimaryPlatformIdentifier();
                Logger.Debug($"  - Platform ID used: {platformId}");

                var assignmentManager = Current.Game.GetComponent<GameComponent_PawnAssignmentManager>();
                bool hasAssignment = assignmentManager.viewerPawnAssignments.ContainsKey(platformId);
                Logger.Debug($"  - Has assignment in dictionary: {hasAssignment}");

                if (hasAssignment)
                {
                    string thingId = assignmentManager.viewerPawnAssignments[platformId];
                    Logger.Debug($"  - Stored ThingID: {thingId}");

                    // Try to find the pawn directly
                    Pawn directPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                    Logger.Debug($"  - Direct FindPawnByThingId result: {directPawn != null}");
                    if (directPawn != null)
                    {
                        Logger.Debug($"  - Direct pawn Name: {directPawn.Name}");
                        Logger.Debug($"  - Direct pawn ThingID: {directPawn.ThingID}");
                    }
                }
            }

            bool hasPawn = viewerPawn != null && !viewerPawn.Dead;

            if (hasPawn)
            {
                // Draw pawn portrait
                Rect portraitRect = new Rect(leftPadding + 10f, y, pawnPortraitSize, pawnPortraitSize);
                DrawPawnPortrait(portraitRect, viewerPawn);

                // Draw pawn info to the right of the portrait
                float infoX = leftPadding + pawnPortraitSize + 20f;
                float infoWidth = width - infoX - leftPadding;

                // Pawn name
                Rect nameRect = new Rect(infoX, y, infoWidth, sectionHeight);
                string pawnName = viewerPawn.Name?.ToStringFull ?? "Unnamed";
                Widgets.Label(nameRect, $"Name: {pawnName}");
                y += sectionHeight;

                // Pawn type and status
                Rect typeRect = new Rect(infoX, y, infoWidth, sectionHeight);
                string pawnRace = viewerPawn.def.label.CapitalizeFirst(); // "human" becomes "Human"
                string healthStatus = viewerPawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                Widgets.Label(typeRect, $"Race: {pawnRace}, Health: {healthStatus}");
                y += sectionHeight;

                // Location
                Rect locationRect = new Rect(infoX, y, infoWidth, sectionHeight);
                string location = viewerPawn.Map?.Parent.Label ?? "World";
                if (viewerPawn.Dead) location = "DECEASED";
                else if (!viewerPawn.Spawned) location = "Off-map";
                Widgets.Label(locationRect, $"Location: {location}");
                y += sectionHeight;

                // Unassign button
                Rect unassignRect = new Rect(infoX, y, 120f, sectionHeight);
                if (Widgets.ButtonText(unassignRect, "Unassign Pawn"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Unassign {pawnName} from {selectedViewer.Username}?\nThis will allow them to buy a new pawn.",
                        () => {
                            UnassignPawn(selectedViewer);
                            Messages.Message($"Unassigned {pawnName} from {selectedViewer.Username}", MessageTypeDefOf.NeutralEvent);
                        },
                        true
                    ));
                }
                y += sectionHeight;

                // Move y down to account for portrait height
                y = Mathf.Max(y, portraitRect.y + pawnPortraitSize + 10f);
            }
            else
            {
                // No pawn assigned
                Rect noPawnRect = new Rect(leftPadding + 10f, y, width, sectionHeight);
                Widgets.Label(noPawnRect, "No pawn assigned in current game");
                y += sectionHeight;

                // Show if they have a pawn in another game or if pawn is dead
                if (viewerPawn != null && viewerPawn.Dead)
                {
                    Rect deadPawnRect = new Rect(leftPadding + 10f, y, width, sectionHeight);
                    Widgets.Label(deadPawnRect, "Assigned pawn is deceased");
                    y += sectionHeight;

                    // Cleanup button for dead pawn
                    Rect cleanupRect = new Rect(leftPadding + 10f, y, 120f, sectionHeight);
                    if (Widgets.ButtonText(cleanupRect, "Clear Assignment"))
                    {
                        UnassignPawn(selectedViewer);
                        Messages.Message($"Cleared deceased pawn assignment for {selectedViewer.Username}", MessageTypeDefOf.NeutralEvent);
                    }
                    y += sectionHeight;
                }
            }
        }

        private void DrawPawnPortrait(Rect rect, Pawn pawn)
        {
            try
            {
                // Draw background
                Widgets.DrawMenuSection(rect);

                // Draw pawn portrait
                Rect portraitRect = rect.ContractedBy(4f);
                GUI.color = Color.white;

                if (pawn != null && !pawn.Dead)
                {
                    // Use RimWorld's portrait renderer
                    RenderTexture portrait = PortraitsCache.Get(pawn, portraitRect.size, Rot4.South, default(Vector3), 1f, true, true, true, true, null, null, false);
                    if (portrait != null)
                    {
                        GUI.DrawTexture(portraitRect, portrait);
                    }
                }
                else if (pawn != null && pawn.Dead)
                {
                    // Cross out dead pawns - use a simple gray texture instead
                    Texture2D grayTex = BaseContent.GreyTex;
                    GUI.DrawTexture(portraitRect, grayTex);

                    // Draw red X over the portrait
                    Widgets.DrawLine(rect.position, new Vector2(rect.xMax, rect.yMax), Color.red, 2f);
                    Widgets.DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMax), Color.red, 2f);
                }

                // Draw a border
                Widgets.DrawBox(rect, 2);
            }
            catch (Exception ex)
            {
                // Fallback if portrait rendering fails
                Widgets.DrawRectFast(rect, new Color(0.3f, 0.3f, 0.3f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "Portrait\nError");
                Text.Anchor = TextAnchor.UpperLeft;
                Logger.Warning($"Error drawing pawn portrait: {ex.Message}");
            }
        }



        private void DrawEconomyRow(ref float y, float width, string label, int currentValue,
            ref int editAmount, ref string editBuffer, Action<int> onSet,
            int minValue, int maxValue, float leftPadding = 0f)
        {
            float sectionHeight = 28f;
            float inputWidth = 80f;
            float buttonWidth = 60f;
            float spacing = 5f;

            // Label
            Rect labelRect = new Rect(leftPadding, y, 60f, sectionHeight);
            Widgets.Label(labelRect, label + ":");

            // Current value
            Rect valueRect = new Rect(leftPadding + 65f, y, 80f, sectionHeight);
            Widgets.Label(valueRect, currentValue.ToString());

            // Numeric input field - FIXED: Using ref parameters to persist values
            Rect inputRect = new Rect(leftPadding + 200f, y, inputWidth, sectionHeight);
            Widgets.TextFieldNumeric(inputRect, ref editAmount, ref editBuffer, 0, maxValue);

            // Buttons
            float buttonsStartX = leftPadding + 200f + inputWidth + spacing;

            Rect giveRect = new Rect(buttonsStartX, y, buttonWidth, sectionHeight);
            if (Widgets.ButtonText(giveRect, "Give") && editAmount > 0)
            {
                int newValue = Mathf.Min(currentValue + editAmount, maxValue);
                onSet?.Invoke(newValue);
            }

            Rect takeRect = new Rect(buttonsStartX + buttonWidth + spacing, y, buttonWidth, sectionHeight);
            if (Widgets.ButtonText(takeRect, "Take") && editAmount > 0)
            {
                int newValue = Mathf.Max(currentValue - editAmount, minValue);
                onSet?.Invoke(newValue);
            }

            Rect setRect = new Rect(buttonsStartX + (buttonWidth + spacing) * 2, y, buttonWidth, sectionHeight);
            if (Widgets.ButtonText(setRect, "Set") && editAmount >= minValue && editAmount <= maxValue)
            {
                onSet?.Invoke(editAmount);
            }

            y += sectionHeight;
        }

        private float CalculateDetailsHeight(float width)
        {
            if (selectedViewer == null) return 100f;

            float height = 50f; // Header space

            // Pawn section height
            Pawn viewerPawn = GetViewerPawn(selectedViewer);
            bool hasPawn = viewerPawn != null && !viewerPawn.Dead;
            if (hasPawn)
            {
                height += 28f; // Section header
                height += 80f; // Portrait height
                height += 20f; // Spacing
            }
            else
            {
                height += 28f * 2; // No pawn message + spacing
            }

            height += 28f; // Platform label
            height += selectedViewer.PlatformUserIds.Count * 23f; // Platform IDs
            height += 38f; // Economy label + spacing
            height += 28f * 2; // Coins and Karma rows
            height += 28f * 3; // Activity info
            height += 56f; // Ban section

            return height + 40f; // Extra padding
        }

        private void DrawBanSection(Rect rect)
        {
            Widgets.DrawMenuSection(rect.ContractedBy(5f));

            Rect innerRect = rect.ContractedBy(10f);
            Text.Anchor = TextAnchor.MiddleCenter;

            if (selectedViewer.IsBanned)
            {
                Widgets.Label(innerRect.TopHalf(), "🚫 THIS VIEWER IS BANNED");

                // Split the bottom half for two buttons
                Rect unbanButtonRect = new Rect(innerRect.x, innerRect.y + innerRect.height / 2, innerRect.width / 2 - 5f, innerRect.height / 2);
                Rect removeButtonRect = new Rect(innerRect.x + innerRect.width / 2 + 5f, innerRect.y + innerRect.height / 2, innerRect.width / 2 - 5f, innerRect.height / 2);

                if (Widgets.ButtonText(unbanButtonRect, "UNBAN VIEWER"))
                {
                    selectedViewer.IsBanned = false;
                    Viewers.SaveViewers();
                    Messages.Message($"{selectedViewer.Username} has been unbanned", MessageTypeDefOf.PositiveEvent);
                }

                if (Widgets.ButtonText(removeButtonRect, "REMOVE USER"))
                {
                    showRemoveConfirmation = true;
                }
            }
            else
            {
                Widgets.Label(innerRect.TopHalf(), "User Management");

                // Split the bottom half for two buttons
                Rect banButtonRect = new Rect(innerRect.x, innerRect.y + innerRect.height / 2, innerRect.width / 2 - 5f, innerRect.height / 2);
                Rect removeButtonRect = new Rect(innerRect.x + innerRect.width / 2 + 5f, innerRect.y + innerRect.height / 2, innerRect.width / 2 - 5f, innerRect.height / 2);

                if (Widgets.ButtonText(banButtonRect, "BAN VIEWER"))
                {
                    showBanConfirmation = true;
                    banConfirmationMessage = $"Are you sure you want to ban {selectedViewer.Username}?\n\nThis will also remove any pawn assignments.";
                }

                if (Widgets.ButtonText(removeButtonRect, "REMOVE USER"))
                {
                    showRemoveConfirmation = true;
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void HandleConfirmations()
        {
            // Ban confirmation
            if (showBanConfirmation)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    banConfirmationMessage,
                    () => {
                        selectedViewer.IsBanned = true;

                        // ADD THIS LINE: Remove pawn assignments when banning
                        UnassignPawn(selectedViewer);

                        Viewers.SaveViewers();
                        Messages.Message($"{selectedViewer.Username} has been banned and pawn assignments removed", MessageTypeDefOf.NegativeEvent);
                        showBanConfirmation = false;
                    },
                    true
                ));
                showBanConfirmation = false;
            }

            // Mass action confirmations
            if (showAwardCoinsConfirmation)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Award coins to all active viewers?",
                    () => {
                        Viewers.AwardActiveViewersCoins();
                        Messages.Message("Coins awarded to active viewers", MessageTypeDefOf.PositiveEvent);
                        showAwardCoinsConfirmation = false;
                    }
                ));
                showAwardCoinsConfirmation = false;
            }

            if (showResetCoinsConfirmation)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset ALL viewer coins to starting amount?",
                    () => {
                        Viewers.ResetAllCoins();
                        Messages.Message("All viewer coins reset", MessageTypeDefOf.NeutralEvent);
                        showResetCoinsConfirmation = false;
                    }
                ));
                showResetCoinsConfirmation = false;
            }

            if (showResetKarmaConfirmation)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset ALL viewer karma to starting amount?",
                    () => {
                        Viewers.ResetAllKarma();
                        Messages.Message("All viewer karma reset", MessageTypeDefOf.NeutralEvent);
                        showResetKarmaConfirmation = false;
                    }
                ));
                showResetKarmaConfirmation = false;
            }

            // Remove user confirmation
            if (showRemoveConfirmation)
            {
                // CAPTURE the username before removal to avoid null reference
                string usernameToRemove = selectedViewer.Username;

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Permanently remove {usernameToRemove} from the viewer list?\n\n" +
                    "This will:\n" +
                    "• Remove all their data (coins, karma, history)\n" +
                    "• Remove any pawn assignments\n" +
                    "• Cannot be undone!",
                    () => {
                        // Remove pawn assignments first
                        UnassignPawn(selectedViewer);

                        // Remove from viewers list
                        Viewers.All.Remove(selectedViewer);
                        Viewers.SaveViewers();

                        // Clear selection and refresh
                        selectedViewer = null;
                        FilterViewers();

                        // Use the captured username here instead of selectedViewer.Username
                        Messages.Message($"{usernameToRemove} has been permanently removed from the viewer list", MessageTypeDefOf.NeutralEvent);
                        showRemoveConfirmation = false;
                    },
                    true,
                    "REMOVE USER"
                ));
                showRemoveConfirmation = false;
            }
        }

        private void ShowStatistics()
        {
            var activeViewers = Viewers.GetActiveViewers();
            int totalCoins = Viewers.All.Sum(v => v.Coins);
            int totalKarma = Viewers.All.Sum(v => v.Karma);
            int bannedCount = Viewers.All.Count(v => v.IsBanned);

            string stats = $"Total Viewers: {Viewers.All.Count}\n" +
                          $"Active Viewers: {activeViewers.Count}\n" +
                          $"Banned Viewers: {bannedCount}\n" +
                          $"Total Coins in Circulation: {totalCoins}\n" +
                          $"Average Coins per Viewer: {totalCoins / Mathf.Max(1, Viewers.All.Count)}\n" +
                          $"Average Karma: {totalKarma / Mathf.Max(1, Viewers.All.Count)}";

            Find.WindowStack.Add(new Dialog_MessageBox(stats, "Viewer Statistics"));
        }

        private void FilterViewers()
        {
            lastSearch = searchQuery;
            filteredViewers.Clear();

            var allViewers = Viewers.All.AsEnumerable();

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allViewers = allViewers.Where(viewer =>
                    viewer.Username.ToLower().Contains(searchLower) ||
                    viewer.DisplayName.ToLower().Contains(searchLower)
                );
            }

            filteredViewers = allViewers.ToList();
            SortViewers();
        }

        private void SortViewers()
        {
            switch (sortMethod)
            {
                case ViewerSortMethod.Username:
                    filteredViewers = sortAscending ?
                        filteredViewers.OrderBy(v => v.Username).ToList() :
                        filteredViewers.OrderByDescending(v => v.Username).ToList();
                    break;
                case ViewerSortMethod.Coins:
                    filteredViewers = sortAscending ?
                        filteredViewers.OrderBy(v => v.Coins).ToList() :
                        filteredViewers.OrderByDescending(v => v.Coins).ToList();
                    break;
                case ViewerSortMethod.Karma:
                    filteredViewers = sortAscending ?
                        filteredViewers.OrderBy(v => v.Karma).ToList() :
                        filteredViewers.OrderByDescending(v => v.Karma).ToList();
                    break;
            }
        }

        private Pawn GetViewerPawn(Viewer viewer)
        {
            var assignmentManager = Current.Game.GetComponent<GameComponent_PawnAssignmentManager>();

            // Get the platform ID (like "twitch:58513264")
            string platformId = viewer.GetPrimaryPlatformIdentifier();
            Logger.Debug($"At:  GetViewerPawn PVM:{platformId}");

            // Use the internal method that takes the identifier directly
            return assignmentManager.GetAssignedPawnIdentifier(platformId);
        }

        private bool HasAssignedPawn(Viewer viewer)
        {
            var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
            return assignmentManager?.HasAssignedPawn(viewer.Username) ?? false;
        }

        private void UnassignPawn(Viewer viewer)
        {
            Logger.Debug($"Unassign pawn for viewer: {viewer.Username}");
            var assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();

            if (assignmentManager != null)
            {
                // Get the platform ID that matches how pawns are stored
                string platformId = viewer.GetPrimaryPlatformIdentifier();
                Logger.Debug($"Using platform ID for unassignment: {platformId}");

                assignmentManager.UnassignPawn(platformId);

                // Also remove from queue if present
                assignmentManager.RemoveFromQueue(viewer.Username);
            }
        }
    }

    public enum ViewerSortMethod
    {
        Username,
        Coins,
        Karma
    }

    // Simple dialog for editing values
    public class Dialog_EditValue : Window
    {
        private string title;
        private string label;
        private int minValue;
        private int maxValue;
        private Action<int> onConfirm;
        private int currentValue;
        private string buffer;

        public override Vector2 InitialSize => new Vector2(200f, 200f); // More reasonable size

        public Dialog_EditValue(string title, string label, int minValue, int maxValue, Action<int> onConfirm, int initialValue = 0)
        {
            this.title = title;
            this.label = label;
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.onConfirm = onConfirm;
            this.currentValue = initialValue;
            this.buffer = initialValue.ToString();

            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }
        public override void DoWindowContents(Rect inRect)
        {
            float padding = 15f;
            float currentY = padding;

            // Title
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(padding, currentY, inRect.width - (padding * 2), 30f);
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;
            currentY += 35f;

            // Label
            Rect labelRect = new Rect(padding, currentY, inRect.width - (padding * 2), 30f);
            Widgets.Label(labelRect, label);
            currentY += 35f;

            // Input field - centered but not too wide
            float inputWidth = 120f;
            Rect inputRect = new Rect((inRect.width - inputWidth) / 2f, currentY, inputWidth, 35f);
            Widgets.TextFieldNumeric(inputRect, ref currentValue, ref buffer, minValue, maxValue);
            currentY += 50f; // More space before button

            // Confirm button - centered below input
            float buttonWidth = 100f;
            Rect buttonRect = new Rect((inRect.width - buttonWidth) / 2f, currentY, buttonWidth, 35f);
            if (Widgets.ButtonText(buttonRect, "Confirm"))
            {
                onConfirm?.Invoke(currentValue);
                this.Close();
            }
        }
    }
}