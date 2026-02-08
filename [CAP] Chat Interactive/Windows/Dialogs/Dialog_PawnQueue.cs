// Dialog_PawnQueue.cs
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
// A dialog window for managing the pawn assignment queue
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_PawnQueue : Window
    {
        private Vector2 pawnScrollPosition = Vector2.zero;
        private Vector2 queueScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        private Pawn selectedPawn = null;
        private string selectedUsername = string.Empty;
        private string selectedUserPlatformID = string.Empty;
        private List<Pawn> availablePawns = new List<Pawn>();
        private List<Pawn> filteredPawns = new List<Pawn>();

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_PawnQueue()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;

            RefreshAvailablePawns();
            FilterPawns();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Update search if query changed
            if (searchQuery != lastSearch || filteredPawns.Count == 0)
            {
                FilterPawns();
            }

            // Header - increased height to accommodate two rows
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f); // Increased from 40f to 70f
            DrawHeader(headerRect);

            // Main content area - adjusted position
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - CloseButSize.y); // Adjusted from 45f to 75f
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Custom title with larger font and underline effect
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 400f, 35f);
            //string titleText = $"Pawn Queue Management - {GetQueueManager().GetQueueSize()} waiting";
            string titleText = "RICS.Header.PawnQueueManagement".Translate(GetQueueManager().GetQueueSize());

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

            // Search bar on the left with label
            Rect searchLabelRect = new Rect(0f, controlsY, 80f, controlsHeight);
            Text.Font = GameFont.Medium; // Medium font for the label
            Widgets.Label(searchLabelRect, "RICS.Search".Translate() + ":");
            Text.Font = GameFont.Small;

            Rect searchRect = new Rect(85f, controlsY, 200f, controlsHeight);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Action buttons - right aligned
            float buttonWidth = 120f;
            float spacing = 10f;
            float x = rect.width - (buttonWidth * 3 + spacing * 2);

            // Select Random button
            Rect randomRect = new Rect(x, controlsY, buttonWidth, controlsHeight);
            if (Widgets.ButtonText(randomRect, "RICS.Button.SelectRandom".Translate()))
            {
                SelectRandomViewer();
            }
            x += buttonWidth + spacing;

            // Send Offer button
            Rect offerRect = new Rect(x, controlsY, buttonWidth, controlsHeight);
            if (Widgets.ButtonText(offerRect, "RICS.Button.SendOffer".Translate()) && selectedPawn != null && !string.IsNullOrEmpty(selectedUsername))
            {
                SendPawnOffer(selectedUsername, selectedUserPlatformID, selectedPawn);
            }
            x += buttonWidth + spacing;

            // Clear Queue button
            Rect clearRect = new Rect(x, controlsY, buttonWidth, controlsHeight);
            if (Widgets.ButtonText(clearRect, "RICS.Button.ClearQueue".Translate()))
            {
                //Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                //    "Are you sure you want to clear the entire pawn queue?",
                //    () => GetQueueManager().ClearQueue(),
                //    true
                //));

                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RICS.Confirm.ClearQueue".Translate(),
                    () => GetQueueManager().ClearQueue(),
                    true
                ));
            }

            Widgets.EndGroup();
        }

        private void DrawContent(Rect rect)
        {
            // Layout similar to ViewerManager
            float listWidth = 300f;
            float detailsWidth = rect.width - listWidth - 10f;

            Rect pawnListRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect detailsRect = new Rect(rect.x + listWidth + 10f, rect.y, detailsWidth, rect.height);

            DrawPawnList(pawnListRect);
            DrawQueueDetails(detailsRect);
        }

        private void DrawPawnList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header Available Pawns
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(headerRect, "RICS.Header.AvailablePawns".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Pawn list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 60f; // Larger for pawn info
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredPawns.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref pawnScrollPosition, viewRect);
            {
                float y = 0f;
                for (int i = 0; i < filteredPawns.Count; i++)
                {
                    Pawn pawn = filteredPawns[i];
                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Draw pawn row
                    DrawPawnRow(buttonRect, pawn, i);

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawPawnRow(Rect rect, Pawn pawn, int index)
        {
            bool isSelected = selectedPawn == pawn;

            // Alternate background
            if (index % 2 == 0)
            {
                Widgets.DrawLightHighlight(rect);
            }

            // Selection highlight
            if (isSelected)
            {
                Widgets.DrawHighlightSelected(rect);
            }

            // Pawn portrait (small)
            Rect portraitRect = new Rect(rect.x + 5f, rect.y + 5f, 50f, 50f);
            DrawSmallPawnPortrait(portraitRect, pawn);

            // Pawn info - with proper spacing
            Rect infoRect = new Rect(portraitRect.xMax + 10f, rect.y + 5f, rect.width - portraitRect.width - 20f, 50f);
            DrawPawnInfo(infoRect, pawn);

            // Click handler
            if (Widgets.ButtonInvisible(rect))
            {
                selectedPawn = pawn;
            }
        }

        private void DrawSmallPawnPortrait(Rect rect, Pawn pawn)
        {
            try
            {
                if (pawn != null && !pawn.Dead)
                {
                    RenderTexture portrait = PortraitsCache.Get(pawn, rect.size, Rot4.South, default(Vector3), 1f, true, true, true, true, null, null, false);
                    if (portrait != null)
                    {
                        GUI.DrawTexture(rect, portrait);
                    }
                }
                else if (pawn != null && pawn.Dead)
                {
                    // Gray out dead pawns
                    GUI.color = Color.gray;
                    Widgets.DrawRectFast(rect, new Color(0.3f, 0.3f, 0.3f));
                    GUI.color = Color.white;
                }

                // Border
                Widgets.DrawBox(rect, 1);
            }
            catch
            {
                // Fallback
                Widgets.DrawRectFast(rect, new Color(0.3f, 0.3f, 0.3f));
            }
        }

        private void DrawPawnInfo(Rect rect, Pawn pawn)
        {
            Text.Anchor = TextAnchor.UpperLeft;

            // Calculate centered positions for 2 lines in the available 50px height
            float lineHeight = 22f; // Increased from 20f to 22f
            float totalHeight = lineHeight * 2;
            float startY = rect.y + (50f - totalHeight) / 2f; // Center in the 50px info area

            // Line 1: Name • Age • Gender
            //string nameAgeGender = $"{pawn.Name?.ToStringShort ?? "Unnamed"} • {pawn.ageTracker.AgeBiologicalYears} • {GetGenderSymbol(pawn)}";
            string nameAgeGender = "RICS.Pawn.NameAgeGender".Translate(
                pawn.Name?.ToStringShort ?? "RICS.Pawn.Unnamed".Translate(),
                pawn.ageTracker.AgeBiologicalYears,
                GetGenderSymbol(pawn)
            );

            Rect line1Rect = new Rect(rect.x, startY, rect.width, lineHeight);
            Widgets.Label(line1Rect, nameAgeGender);

            // Line 2: Race • Xenotype (if Biotech)
            string raceXenotype = pawn.def.label.CapitalizeFirst();
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                XenotypeDef xenotype = pawn.genes.Xenotype;
                if (xenotype != null && xenotype != XenotypeDefOf.Baseliner)
                {
                    raceXenotype += $" • {xenotype.label}";
                }
            }
            Rect line2Rect = new Rect(rect.x, startY + lineHeight, rect.width, lineHeight);
            Widgets.Label(line2Rect, raceXenotype);

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string GetGenderSymbol(Pawn pawn)
        {
            return pawn.gender switch
            {
                Gender.Male => "RICS.Pawn.Gender.Male".Translate() + " ♂️",
                Gender.Female => "RICS.Pawn.Gender.Female".Translate() + " ♀️",
                _ => "RICS.Pawn.Gender.Other".Translate() + " ⚧️"
            };
        }

        private void DrawQueueDetails(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            if (selectedPawn == null)
            {
                // No pawn selected message
                Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                // Widgets.Label(messageRect, "Select a pawn to assign from queue");
                Widgets.Label(messageRect, "RICS.Label.SelectPawnToAssign".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Header with pawn info - increased height for bio card layout with assign section
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 250f); // Increased from 220f to 250f
            DrawPawnHeader(headerRect, selectedPawn);

            // Queue list below - adjusted position
            Rect queueRect = new Rect(rect.x, rect.y + 260f, rect.width, rect.height - 270f);
            DrawQueueList(queueRect);
        }

        private void DrawPawnHeader(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);

            // Layout constants
            float portraitSize = 80f;
            float leftColWidth = 200f;
            float rightColWidth = rect.width - portraitSize - leftColWidth - 30f;
            float lineHeight = 22f;
            float currentY = rect.y + 10f;

            // Pawn portrait (larger)
            Rect portraitRect = new Rect(rect.x + 10f, rect.y + 10f, portraitSize, portraitSize);
            DrawSmallPawnPortrait(portraitRect, pawn);

            // Left column (bio info)
            Rect leftColRect = new Rect(portraitRect.xMax + 10f, currentY, leftColWidth, rect.height - 20f);

            // Right column (skills)
            Rect rightColRect = new Rect(leftColRect.xMax + 10f, currentY, rightColWidth, rect.height - 20f);

            // Draw left column content
            DrawPawnBioInfo(leftColRect, pawn, lineHeight);

            // Draw right column content
            DrawPawnSkills(rightColRect, pawn, lineHeight);

            // Username input and assign button at bottom
            Rect usernameRect = new Rect(leftColRect.x, rect.yMax - 35f, leftColWidth - 100f, 25f);
            selectedUsername = Widgets.TextField(usernameRect, selectedUsername);

            Rect assignRect = new Rect(usernameRect.xMax + 5f, usernameRect.y, 95f, 25f);
            if (Widgets.ButtonText(assignRect, "RICS.Assign".Translate()) && !string.IsNullOrEmpty(selectedUsername))
            {
                // Use direct assignment instead of sending offer
                AssignPawnDirectly(selectedUsername, selectedUserPlatformID, pawn);
            }
        }

        private void DrawPawnBioInfo(Rect rect, Pawn pawn, float lineHeight)
        {
            float currentY = rect.y;
            Text.Anchor = TextAnchor.UpperLeft;

            // Name, Gender, Age
            // string nameInfo = $"{pawn.Name?.ToStringFull ?? "Unnamed"} • {GetGenderSymbol(pawn)} • {pawn.ageTracker.AgeBiologicalYears}";
            string nameInfo = "RICS.Pawn.NameGenderAge".Translate(
                pawn.Name?.ToStringFull ?? "RICS.Pawn.Unnamed".Translate(),
                GetGenderSymbol(pawn),
                pawn.ageTracker.AgeBiologicalYears
            );
            Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), nameInfo);
            currentY += lineHeight;

            // Race and Xenotype
            string raceInfo = pawn.def.label.CapitalizeFirst();
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                XenotypeDef xenotype = pawn.genes.Xenotype;
                if (xenotype != null && xenotype != XenotypeDefOf.Baseliner)
                {
                    raceInfo += $" • {xenotype.label}";
                }
            }
            Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), raceInfo);
            currentY += lineHeight + 5f;

            // Backstories
            if (pawn.story != null)
            {
                if (pawn.story.Childhood != null)
                {
                    string childhoodTitle = pawn.story.Childhood.TitleCapFor(pawn.gender);
                    Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), $"RICS.Label.Childhood".Translate() + ": {childhoodTitle}");
                    currentY += lineHeight;
                }

                if (pawn.story.Adulthood != null)
                {
                    string adulthoodTitle = pawn.story.Adulthood.TitleCapFor(pawn.gender);
                    Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), $"RICS.Label.Adulthood".Translate() + ": {adulthoodTitle}");
                    currentY += lineHeight + 5f;
                }
            }

            // Traits
            if (pawn.story != null && pawn.story.traits != null && pawn.story.traits.allTraits.Count > 0)
            {
                Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), "RICS.Label.Traits".Translate() + ":");
                currentY += lineHeight;

                foreach (Trait trait in pawn.story.traits.allTraits)
                {
                    Widgets.Label(new Rect(rect.x + 10f, currentY, rect.width - 10f, lineHeight), trait.LabelCap);
                    currentY += lineHeight;
                }
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawPawnSkills(Rect rect, Pawn pawn, float lineHeight)
        {
            float currentY = rect.y;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.Label(new Rect(rect.x, currentY, rect.width, lineHeight), "Skills:");
            currentY += lineHeight;

            // Draw skills in two columns
            float col1Width = 100f;
            float col2Width = rect.width - col1Width - 10f;
            int skillsPerCol = 10; // Roughly half the skills

            var allSkills = pawn.skills.skills.OrderBy(s => s.def.listOrder).ToList();

            for (int i = 0; i < allSkills.Count; i++)
            {
                SkillRecord skill = allSkills[i];
                float colX = (i < skillsPerCol) ? rect.x : rect.x + col1Width + 10f;
                float colY = rect.y + lineHeight + (i % skillsPerCol) * lineHeight;

                // Skill name and level
                string skillText = $"{skill.def.LabelCap}: {skill.Level}";

                // Add passion symbol
                string passionSymbol = skill.passion switch
                {
                    Passion.Major => " 🔥",
                    Passion.Minor => " ♨️",
                    _ => ""
                };
                skillText += passionSymbol;

                Widgets.Label(new Rect(colX, colY, col1Width, lineHeight), skillText);
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawQueueList(Rect rect)
        {
            var queueManager = GetQueueManager();
            var queueList = queueManager.GetQueueList();

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            // string headerText = $"Viewers in Queue ({queueList.Count})";
            string headerText = "RICS.Label.ViewersInQueue".Translate(queueList.Count);
            Widgets.Label(headerRect, headerText);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Queue list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);

            if (queueList.Count == 0)
            {
                // Empty queue message
                Rect emptyRect = new Rect(listRect.x, listRect.y, listRect.width, 50f);
                Text.Anchor = TextAnchor.MiddleCenter;
                // Widgets.Label(emptyRect, "No viewers in queue");
                Widgets.Label(emptyRect, "RICS.Label.NoViewersInQueue".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float rowHeight = 30f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, queueList.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref queueScrollPosition, viewRect);
            {
                float y = 0f;
                for (int i = 0; i < queueList.Count; i++)
                {
                    string platformId = queueList[i];

                    // Convert platform ID to username for display
                    string username = queueManager.GetUsernameFromPlatformId(platformId);
                    string displayName = CapitalizeFirst(username);
                    Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);

                    // Alternate background
                    if (i % 2 == 0)
                    {
                        Widgets.DrawLightHighlight(rowRect);
                    }

                    // Position and username (now showing actual username Capitalized as displayName )
                    Widgets.Label(new Rect(10f, y, 40f, rowHeight), $"#{i + 1}");
                    Widgets.Label(new Rect(50f, y, 200f, rowHeight), displayName);

                    // Action buttons
                    Rect selectRect = new Rect(260f, y, 80f, rowHeight - 4f);
                    if (Widgets.ButtonText(selectRect, "RICS.Button.Select".Translate()))
                    {
                        selectedUsername = username; // Store username for assignment
                        selectedUserPlatformID = platformId;
                    }

                    Rect removeRect = new Rect(345f, y, 80f, rowHeight - 4f);
                    if (Widgets.ButtonText(removeRect, "RICS.Button.Remove".Translate()))
                    {
                        queueManager.RemoveFromQueue(platformId); // Remove by platform ID
                    }

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private string CapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : "");
        }

        private void RefreshAvailablePawns()
        {
            availablePawns.Clear();

            if (Current.Game == null) return;

            // Get all player pawns that are alive and not assigned
            var assignmentManager = GetQueueManager();
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.RaceProps.Humanlike && !pawn.Dead && pawn.Faction?.IsPlayer == true)
                    {
                        // Check if pawn is already assigned to a viewer
                        string assignedUser = assignmentManager.GetUsernameForPawn(pawn);
                        if (string.IsNullOrEmpty(assignedUser))
                        {
                            availablePawns.Add(pawn);
                        }
                    }
                }
            }

            // Sort by name for consistency
            availablePawns = availablePawns.OrderBy(p => p.Name?.ToStringFull ?? "").ToList();
        }

        private void FilterPawns()
        {
            lastSearch = searchQuery;
            filteredPawns.Clear();

            var allPawns = availablePawns.AsEnumerable();

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allPawns = allPawns.Where(pawn =>
                    (pawn.Name?.ToStringFull?.ToLower().Contains(searchLower) ?? false) ||
                    pawn.def.label.ToLower().Contains(searchLower)
                );
            }

            filteredPawns = allPawns.ToList();
        }

        private void SelectRandomViewer()
        {
            var queueManager = GetQueueManager();
            var queue = queueManager.GetQueueList();

            if (queue.Count == 0)
            {
                Messages.Message("RICS.Label.NoViewersInQueue".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            // Select random viewer from queue
            string randomViewer = queue[Rand.Range(0, queue.Count)];
            selectedUsername = randomViewer;

            // Auto-select first available pawn if none selected
            if (selectedPawn == null && filteredPawns.Count > 0)
            {
                selectedPawn = filteredPawns[0];
            }

            // Messages.Message($"Selected {randomViewer} from queue", MessageTypeDefOf.NeutralEvent);
            Messages.Message("RICS.Message.SelectedRandomViewer".Translate(randomViewer), MessageTypeDefOf.NeutralEvent);
        }

        private void SendPawnOffer(string username, string platformID, Pawn pawn)
        {
            var queueManager = GetQueueManager();

            // Remove from queue using platform ID
            queueManager.RemoveFromQueue(platformID);

            // Add pending offer using platform ID for security
            queueManager.AddPendingOffer(username, platformID, pawn);

            // Get timeout settings
            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            int timeoutSeconds = settings?.PawnOfferTimeoutSeconds ?? 300;
            int timeoutMinutes = timeoutSeconds / 60;

            // Send chat message TO USERNAME (user-facing)
            string offerMessage = $"🎉 You've been offered {pawn.Name}! Type !acceptpawn within {timeoutMinutes} minutes to claim your pawn!";
            ChatCommandProcessor.SendMessageToUsername(username, offerMessage);

            // Update UI
            RefreshAvailablePawns();
            FilterPawns();

            // Clear selection
            selectedUsername = "";
            selectedUserPlatformID = "";
            if (filteredPawns.Count > 0)
                selectedPawn = filteredPawns[0];
            else
                selectedPawn = null;

            Messages.Message($"Sent pawn offer to {username}", MessageTypeDefOf.PositiveEvent);
        }

        // NEW METHOD: Direct assignment without sending offer
        private void AssignPawnDirectly(string username, string platformID, Pawn pawn)
        {
            var queueManager = GetQueueManager();

            // Validate that we have a valid platform ID
            if (string.IsNullOrEmpty(platformID))
            {
                // Try to find the viewer and get their platform ID
                Viewer viewer = Viewers.GetViewer(username);
                if (viewer != null)
                {
                    platformID = viewer.GetPrimaryPlatformIdentifier();
                    //Logger.Debug($"Found platform ID for {username}: {platformID}");
                }
                else
                {
                    // Show warning and abort the assignment
                    //Messages.Message($"Cannot assign pawn - viewer '{username}' not found in database.", MessageTypeDefOf.RejectInput);
                    Messages.Message("RICS.Message.ViewerNotFound".Translate(username), MessageTypeDefOf.RejectInput);

                    //Logger.Warning($"Cannot assign pawn to {username} - viewer not found in database");
                    return; // Abort the assignment
                }
            }

            // Remove from queue using platform ID
            queueManager.RemoveFromQueue(platformID);

            // Directly assign the pawn - now we have a valid platformID
            queueManager.AssignPawnToViewerDialog(username, platformID, pawn);

            // Send confirmation message to chat (user-facing)
            // string assignMessage = $"🎉 You have been assigned {pawn.Name}! Use !mypawn to check your pawn's status.";
            string assignMessage = "RICS.Message.PawnAssigned".Translate(pawn.Name.ToStringFull);
            
            ChatCommandProcessor.SendMessageToUsername(username, assignMessage);

            // Update UI
            RefreshAvailablePawns();
            FilterPawns();

            // Clear selection
            selectedUsername = "";
            selectedUserPlatformID = "";
            if (filteredPawns.Count > 0)
                selectedPawn = filteredPawns[0];
            else
                selectedPawn = null;

            // Messages.Message($"Assigned pawn directly to {username}", MessageTypeDefOf.PositiveEvent);
            Messages.Message("RICS.Message.PawnAssignedDirectly".Translate(username), MessageTypeDefOf.PositiveEvent);
        }

        private GameComponent_PawnAssignmentManager GetQueueManager()
        {
            return CAPChatInteractiveMod.GetPawnAssignmentManager();
        }
    }
}