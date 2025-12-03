// Dialog_ChatInteractiveSettings.cs
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
// A dialog window for configuring Chat Interactive settings with multiple tabs
using _CAP__Chat_Interactive;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_ChatInteractiveSettings : Window
    {
        private readonly TabWorker _tabWorker = new();
        private Vector2 _scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(800f, 700f);

        public Dialog_ChatInteractiveSettings()
        {
            doCloseButton = true;
            forcePause = false; // Changed from true to false since this is now the main settings
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;
            optionalTitle = "[CAP] Rimworld Interactive Chat Service";
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // Register all tab drawers

            _tabWorker.AddTab(new TabItem
            {
                Label = "Global",
                Tooltip = "Configure global chat and economy settings",
                ContentDrawer = TabDrawer_Global.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "Twitch",
                Tooltip = "Configure Twitch integration settings",
                ContentDrawer = TabDrawer_Twitch.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "YouTube",
                Tooltip = "Configure YouTube integration settings",
                ContentDrawer = TabDrawer_YouTube.Draw
            });


            _tabWorker.AddTab(new TabItem
            {
                Label = "OAuth",
                Tooltip = "Configure YouTube OAuth settings",
                ContentDrawer = TabDrawer_OAuth.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "Economy",
                Tooltip = "Configure karma and coin economy settings",
                ContentDrawer = TabDrawer_Economy.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "Game Events",
                Tooltip = "Manage events, traits, store items and cooldowns",
                ContentDrawer = TabDrawer_GameEvents.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "Rewards",
                Tooltip = "Configure channel points and lootbox rewards",
                ContentDrawer = TabDrawer_Rewards.Draw
            });
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Calculate areas
            var tabBarRect = new Rect(0f, 0f, inRect.width, Text.LineHeight * 1.5f);
            var tabContentRect = new Rect(0f, tabBarRect.height, inRect.width, inRect.height - tabBarRect.height - CloseButSize.y);

            // Draw tab bar
            GUI.BeginGroup(tabBarRect);
            _tabWorker.Draw(tabBarRect.AtZero(), paneled: true);
            GUI.EndGroup();

            // Draw tab content
            GUI.BeginGroup(tabContentRect);
            _tabWorker.SelectedTab?.Draw(tabContentRect.AtZero());
            GUI.EndGroup();
        }
    }
}