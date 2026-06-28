// Dialog_ViewerPawns.cs
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
// A debug dialog window for viewing pawn assignment data
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_ViewerPawns : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private GameComponent_PawnAssignmentManager assignmentManager;

        public override Vector2 InitialSize => new Vector2(800f, 600f);

        public Dialog_ViewerPawns()
        {
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            optionalTitle = "RICS.ViewerPawns.Title".Translate();

            assignmentManager = Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (assignmentManager == null)
            {
                Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "No pawn assignment manager found!");
                return;
            }

            // Header with counts and fix button
            Rect headerRect = new Rect(0f, 0f, inRect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, $"Pawn Assignments: {assignmentManager.viewerPawnAssignments.Count} entries");
            Text.Font = GameFont.Small;

            // Fix button
            Rect fixButtonRect = new Rect(inRect.width - 150f, 35f, 140f, 30f);
            if (Widgets.ButtonText(fixButtonRect, "RICS.ViewerPawns.FixAssignments".Translate()))
            {
                assignmentManager.FixAllPawnAssignments();
                // Refresh the window to show updated data
            }

            // Main content area
            Rect contentRect = new Rect(0f, 70f, inRect.width, inRect.height - 70f - CloseButSize.y);
            DrawAssignmentList(contentRect);
        }

        private void DrawAssignmentList(Rect rect)
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Header row
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            DrawHeaderRow(headerRect);

            // List content
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 25f;
            var assignments = assignmentManager.viewerPawnAssignments.ToList();
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, assignments.Count * rowHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = 0f;
                for (int i = 0; i < assignments.Count; i++)
                {
                    var assignment = assignments[i];
                    Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight - 2f);

                    // Alternate background
                    if (i % 2 == 0)
                    {
                        Widgets.DrawLightHighlight(rowRect);
                    }

                    DrawAssignmentRow(rowRect, assignment.Key, assignment.Value, i + 1);

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawHeaderRow(Rect rect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;

            float platformWidth = rect.width * 0.4f;
            float thingIdWidth = rect.width * 0.4f;
            float statusWidth = rect.width * 0.2f;

            // Platform ID header
            Rect platformHeader = new Rect(rect.x + 5f, rect.y, platformWidth - 10f, rect.height);
            Widgets.Label(platformHeader, "RICS.ViewerPawns.PlatformID".Translate());

            // ThingID header
            Rect thingIdHeader = new Rect(rect.x + platformWidth, rect.y, thingIdWidth - 10f, rect.height);
            Widgets.Label(thingIdHeader, "RICS.ViewerPawns.ThingID".Translate());

            // Status header
            Rect statusHeader = new Rect(rect.x + platformWidth + thingIdWidth, rect.y, statusWidth - 10f, rect.height);
            Widgets.Label(statusHeader, "RICS.ViewerPawns.Status".Translate());

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawAssignmentRow(Rect rect, string platformId, string thingId, int index)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            float platformWidth = rect.width * 0.4f;
            float thingIdWidth = rect.width * 0.4f;
            float statusWidth = rect.width * 0.2f;

            // Platform ID
            Rect platformRect = new Rect(rect.x + 5f, rect.y, platformWidth - 10f, rect.height);
            string platformDisplay = platformId;

            // Try to find username for this platform ID
            string username = assignmentManager.GetUsernameFromPlatformId(platformId);
            if (username != platformId) // If we found a proper username
            {
                platformDisplay = $"{username} ({platformId})";
            }

            Widgets.Label(platformRect, platformDisplay);

            // ThingID
            Rect thingIdRect = new Rect(rect.x + platformWidth, rect.y, thingIdWidth - 10f, rect.height);
            Widgets.Label(thingIdRect, thingId);

            // Status
            Rect statusRect = new Rect(rect.x + platformWidth + thingIdWidth, rect.y, statusWidth - 10f, rect.height);

            Pawn pawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
            if (pawn == null)
            {
                GUI.color = Color.red;
                Widgets.Label(statusRect, "RICS.ViewerPawns.NotFound".Translate());
            }
            else if (pawn.Dead)
            {
                GUI.color = Color.yellow;
                Widgets.Label(statusRect, "RICS.ViewerPawns.Deceased".Translate());
            }
            else
            {
                GUI.color = Color.green;
                Widgets.Label(statusRect, "RICS.ViewerPawns.Active".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            // Add a tooltip with more info
            string tooltip = $"Assignment #{index}\n" +
                           $"Platform ID: {platformId}\n" +
                           $"ThingID: {thingId}\n" +
                           $"Pawn: {(pawn == null ? "NOT FOUND" : pawn.Name?.ToStringFull ?? "Unnamed")}\n" +
                           $"Status: {(pawn == null ? "Missing" : pawn.Dead ? "Dead" : "Alive")}\n" +
                           $"Username: {username}";

            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}