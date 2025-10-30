// UPDATED Dialog_PawnQueue.cs - Fixed spacing and layout issues
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
        private string selectedUsername = "";
        private List<Pawn> availablePawns = new List<Pawn>();
        private List<Pawn> filteredPawns = new List<Pawn>();

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_PawnQueue()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            optionalTitle = "Pawn Queue Management";

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

            // Header
            Rect headerRect = new Rect(0f, 0f, inRect.width, 40f);
            DrawHeader(headerRect);

            // Main content area
            Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 45f - CloseButSize.y);
            DrawContent(contentRect);
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title with counts - left aligned
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, 300f, 30f); // Wider to prevent cutoff
            string titleText = $"Pawn Queue ({GetQueueManager().GetQueueSize()} waiting)";
            Widgets.Label(titleRect, titleText);
            Text.Font = GameFont.Small;

            // Search bar
            Rect searchRect = new Rect(310f, 5f, 250f, 30f); // Moved right
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Action buttons - right aligned
            float buttonWidth = 120f;
            float spacing = 10f;
            float x = rect.width - (buttonWidth * 3 + spacing * 2);

            // Select Random button
            Rect randomRect = new Rect(x, 5f, buttonWidth, 30f);
            if (Widgets.ButtonText(randomRect, "Select Random"))
            {
                SelectRandomViewer();
            }
            x += buttonWidth + spacing;

            // Send Offer button
            Rect offerRect = new Rect(x, 5f, buttonWidth, 30f);
            if (Widgets.ButtonText(offerRect, "Send Offer") && selectedPawn != null && !string.IsNullOrEmpty(selectedUsername))
            {
                SendPawnOffer(selectedUsername, selectedPawn);
            }
            x += buttonWidth + spacing;

            // Clear Queue button
            Rect clearRect = new Rect(x, 5f, buttonWidth, 30f);
            if (Widgets.ButtonText(clearRect, "Clear Queue"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to clear the entire pawn queue?",
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

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(headerRect, "Available Pawns");
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

            // Name - with proper width calculation
            string pawnName = pawn.Name?.ToStringShort ?? "Unnamed";
            Rect nameRect = new Rect(rect.x, rect.y, rect.width, 18f);
            Widgets.Label(nameRect, pawnName);

            // Race and gender
            string raceGender = $"{pawn.def.label.CapitalizeFirst()} • {pawn.gender.ToString()}";
            Rect raceRect = new Rect(rect.x, rect.y + 18f, rect.width, 16f);
            Widgets.Label(raceRect, raceGender);

            // Health and age - with proper spacing
            string healthAge = $"Health: {pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent()} • Age: {pawn.ageTracker.AgeBiologicalYears}";
            Rect healthRect = new Rect(rect.x, rect.y + 34f, rect.width, 16f);
            Widgets.Label(healthRect, healthAge);

            Text.Anchor = TextAnchor.UpperLeft;
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
                Widgets.Label(messageRect, "Select a pawn to assign from queue");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Header with pawn info - increased height for better spacing
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 90f); // Increased from 80f
            DrawPawnHeader(headerRect, selectedPawn);

            // Queue list below - adjusted position
            Rect queueRect = new Rect(rect.x, rect.y + 100f, rect.width, rect.height - 110f); // Adjusted from 90f/100f
            DrawQueueList(queueRect);
        }

        private void DrawPawnHeader(Rect rect, Pawn pawn)
        {
            Widgets.DrawMenuSection(rect);

            // Larger portrait
            Rect portraitRect = new Rect(rect.x + 10f, rect.y + 10f, 60f, 60f);
            DrawSmallPawnPortrait(portraitRect, pawn);

            // Pawn details - with better spacing
            Rect detailsRect = new Rect(portraitRect.xMax + 10f, rect.y + 10f, rect.width - portraitRect.width - 20f, 60f);

            string pawnName = pawn.Name?.ToStringFull ?? "Unnamed";
            string pawnInfo = $"{pawn.def.label.CapitalizeFirst()} • {pawn.gender.ToString()} • Age: {pawn.ageTracker.AgeBiologicalYears}";
            string healthInfo = $"Health: {pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent()}";
            string skillsInfo = $"Skills: {pawn.skills.skills.Count(s => s.Level > 0)}";

            Text.Anchor = TextAnchor.UpperLeft;

            // Name
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(new Rect(detailsRect.x, detailsRect.y, detailsRect.width, 20f), pawnName);
            Widgets.Label(new Rect(detailsRect.x, detailsRect.y + 20f, detailsRect.width, 18f), pawnInfo);
            Widgets.Label(new Rect(detailsRect.x, detailsRect.y + 38f, detailsRect.width, 18f), $"{healthInfo} • {skillsInfo}");
            Text.Anchor = TextAnchor.UpperLeft;

            // Username input - moved up to avoid divider
            Rect usernameRect = new Rect(detailsRect.x, detailsRect.y + 56f, detailsRect.width - 100f, 25f);
            selectedUsername = Widgets.TextField(usernameRect, selectedUsername);

            // Manual assign button - moved up
            Rect assignRect = new Rect(usernameRect.xMax + 5f, usernameRect.y, 95f, 25f);
            if (Widgets.ButtonText(assignRect, "Assign") && !string.IsNullOrEmpty(selectedUsername))
            {
                SendPawnOffer(selectedUsername, selectedPawn);
            }
        }

        private void DrawQueueList(Rect rect)
        {
            var queueManager = GetQueueManager();
            var queueList = queueManager.GetQueueList();

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            string headerText = $"Viewers in Queue ({queueList.Count})";
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
                Widgets.Label(emptyRect, "No viewers in queue");
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
                    string username = queueList[i];
                    Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);

                    // Alternate background
                    if (i % 2 == 0)
                    {
                        Widgets.DrawLightHighlight(rowRect);
                    }

                    // Position and username
                    Widgets.Label(new Rect(10f, y, 40f, rowHeight), $"#{i + 1}");
                    Widgets.Label(new Rect(50f, y, 200f, rowHeight), username);

                    // Action buttons
                    Rect selectRect = new Rect(260f, y, 80f, rowHeight - 4f);
                    if (Widgets.ButtonText(selectRect, "Select"))
                    {
                        selectedUsername = username;
                    }

                    Rect removeRect = new Rect(345f, y, 80f, rowHeight - 4f);
                    if (Widgets.ButtonText(removeRect, "Remove"))
                    {
                        queueManager.RemoveFromQueue(username);
                    }

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
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
                Messages.Message("Pawn queue is empty.", MessageTypeDefOf.RejectInput);
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

            Messages.Message($"Selected {randomViewer} from queue", MessageTypeDefOf.NeutralEvent);
        }

        private void SendPawnOffer(string username, Pawn pawn)
        {
            var queueManager = GetQueueManager();

            // Remove from queue and add as pending offer
            queueManager.RemoveFromQueue(username);
            queueManager.AddPendingOffer(username, 60); // 60 second timeout

            // Assign pawn to viewer
            queueManager.AssignPawnToViewer(username, pawn);

            // Send chat message
            string offerMessage = $"🎉 @{username} You've been assigned {pawn.Name}! Type !acceptpawn within 60 seconds to claim your pawn!";
            //MessageHandler.SendChatMessage(offerMessage);

            // Update UI
            RefreshAvailablePawns();
            FilterPawns();

            // Clear selection
            selectedUsername = "";
            if (filteredPawns.Count > 0)
                selectedPawn = filteredPawns[0];
            else
                selectedPawn = null;

            Messages.Message($"Sent pawn offer to {username}", MessageTypeDefOf.PositiveEvent);
        }

        private GameComponent_PawnAssignmentManager GetQueueManager()
        {
            return CAPChatInteractiveMod.GetPawnAssignmentManager();
        }
    }
}