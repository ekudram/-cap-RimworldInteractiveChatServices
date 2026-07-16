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
//
// Settings window — uses RimWorld Verse.TabDrawer + TabRecord (multi-row overflow,
// vanilla TabAtlas look). Tab *content* still lives in TabDrawer_* static drawers.

using _CAP__Chat_Interactive;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_ChatInteractiveSettings : Window
    {
        private enum SettingsTab
        {
            Global,
            Twitch,
            YouTube,
            Kick,
            Economy,
            GameEvents,
            Rewards,
            AI
        }

        private SettingsTab selectedTab = SettingsTab.Global;
        private readonly List<TabRecord> tabs = new List<TabRecord>();

        // Min/max tab chip width for overflow layout (vanilla-style multi-row)
        private const float MinTabWidth = 90f;
        private const float MaxTabWidth = 150f;
        private const float BottomBarHeight = 50f;

        public override Vector2 InitialSize => new Vector2(820f, 720f);

        public Dialog_ChatInteractiveSettings()
        {
            doCloseButton = false;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;
            optionalTitle = "RICS.CIS.OptionalTitle".Translate();
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            // Rebuild every open so we never stack tabs if PreOpen runs again
            RebuildTabs();
            if (!IsValidSelectedTab())
                selectedTab = SettingsTab.Global;
        }

        private bool IsValidSelectedTab()
        {
            foreach (var t in Enum.GetValues(typeof(SettingsTab)))
            {
                if ((SettingsTab)t == selectedTab)
                    return true;
            }
            return false;
        }

        private void RebuildTabs()
        {
            tabs.Clear();

            // selectedGetter keeps Selected state live without mutating TabRecord.selected each frame
            tabs.Add(new TabRecord(
                "RICS.CIS.Global".Translate(),
                () => selectedTab = SettingsTab.Global,
                () => selectedTab == SettingsTab.Global));

            tabs.Add(new TabRecord(
                "RICS.CIS.Twitch".Translate(),
                () => selectedTab = SettingsTab.Twitch,
                () => selectedTab == SettingsTab.Twitch));

            tabs.Add(new TabRecord(
                "RICS.CIS.YouTube".Translate(),
                () => selectedTab = SettingsTab.YouTube,
                () => selectedTab == SettingsTab.YouTube));

            // Kick re-enabled — multi-row TabDrawer has room for more tabs
            tabs.Add(new TabRecord(
                "RICS.CIS.Kick".Translate(),
                () => selectedTab = SettingsTab.Kick,
                () => selectedTab == SettingsTab.Kick));

            tabs.Add(new TabRecord(
                "RICS.CIS.Economy".Translate(),
                () => selectedTab = SettingsTab.Economy,
                () => selectedTab == SettingsTab.Economy));

            tabs.Add(new TabRecord(
                "RICS.CIS.GameEvents".Translate(),
                () => selectedTab = SettingsTab.GameEvents,
                () => selectedTab == SettingsTab.GameEvents));

            tabs.Add(new TabRecord(
                "RICS.CIS.Rewards".Translate(),
                () => selectedTab = SettingsTab.Rewards,
                () => selectedTab == SettingsTab.Rewards));

            tabs.Add(new TabRecord(
                "RICS.CIS.AI".Translate(),
                () => selectedTab = SettingsTab.AI,
                () => selectedTab == SettingsTab.AI));
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (tabs.Count == 0)
                RebuildTabs();

            // How much vertical space the tab strip needs (1+ rows)
            float tabStripHeight = Verse.TabDrawer.GetOverflowTabHeight(
                new Rect(0f, 0f, inRect.width, 200f),
                tabs,
                MinTabWidth,
                MaxTabWidth);
            if (tabStripHeight < Verse.TabDrawer.TabHeight)
                tabStripHeight = Verse.TabDrawer.TabHeight;

            // Content panel must sit fully below the tab strip so scroll views / headers
            // do not paint over the tab titles.
            Rect contentRect = new Rect(
                0f,
                tabStripHeight,
                inRect.width,
                inRect.height - tabStripHeight - BottomBarHeight);

            Widgets.DrawMenuSection(contentRect);

            // DrawTabsOverflow does NOT use the same contract as DrawTabs.
            // DrawTabs draws tabs *above* the given rect (rect.y - TabHeight).
            // DrawTabsOverflow expects a rect whose *top* is the top of the tab strip:
            // for a single row it does baseRect.y += TabHeight, then DrawTabs (tabs land at original y).
            // Passing contentRect here made tabs and content share the same top edge (overlap).
            Rect tabsBaseRect = new Rect(
                0f,
                0f,
                inRect.width,
                inRect.height - BottomBarHeight);
            Verse.TabDrawer.DrawTabsOverflow(tabsBaseRect, tabs, MinTabWidth, MaxTabWidth);

            // Inner content (padding) — scroll views start here, under the tabs
            Rect inner = contentRect.ContractedBy(12f);
            GUI.BeginGroup(inner);
            Rect drawRect = inner.AtZero();
            DrawSelectedTabContent(drawRect);
            GUI.EndGroup();

            DrawBottomBar(inRect);
        }

        private void DrawSelectedTabContent(Rect region)
        {
            switch (selectedTab)
            {
                case SettingsTab.Global:
                    TabDrawer_Global.Draw(region);
                    break;
                case SettingsTab.Twitch:
                    TabDrawer_Twitch.Draw(region);
                    break;
                case SettingsTab.YouTube:
                    TabDrawer_YouTube.Draw(region);
                    break;
                case SettingsTab.Kick:
                    TabDrawer_Kick.Draw(region);
                    break;
                case SettingsTab.Economy:
                    TabDrawer_Economy.Draw(region);
                    break;
                case SettingsTab.GameEvents:
                    TabDrawer_GameEvents.Draw(region);
                    break;
                case SettingsTab.Rewards:
                    TabDrawer_Rewards.Draw(region);
                    break;
                case SettingsTab.AI:
                    TabDrawer_AI.Draw(region);
                    break;
                default:
                    TabDrawer_Global.Draw(region);
                    break;
            }
        }

        private void DrawBottomBar(Rect inRect)
        {
            // Save Backup | Load Backup | … | Close — same pattern as other RICS editors
            float btnH = 38f;
            float btnW = 130f;
            float gap = 6f;
            float padding = 10f;
            float currentY = inRect.yMax - BottomBarHeight + (BottomBarHeight - btnH) / 2f;

            Rect saveRect = new Rect(padding, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveRect, "RICS.Editor.SaveBackup".Translate()))
            {
                var modSettings = CAPChatInteractiveMod.Instance?.Settings;
                if (modSettings != null)
                {
                    JsonFileManager.SaveSettingsBackup(modSettings);
                    Messages.Message("RICS settings backup saved.", MessageTypeDefOf.NeutralEvent);
                }
            }

            float loadX = padding + btnW + gap;
            Rect loadRect = new Rect(loadX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadRect, "RICS.Editor.LoadBackup".Translate()))
            {
                var backup = JsonFileManager.LoadLatestSettingsBackup();
                if (backup != null)
                {
                    JsonFileManager.ApplyBackupToCurrentSettings(backup);
                    Messages.Message(
                        "RICS settings restored from backup. Close and reopen this window to fully refresh.",
                        MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message("No settings backup found.", MessageTypeDefOf.RejectInput);
                }
            }

            float closeX = inRect.xMax - btnW;
            Rect closeRect = new Rect(closeX, currentY, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "RICS.Editor.Close".Translate()))
            {
                Close();
            }
        }

        /// <summary>
        /// NEEDS WORK TO BE FULLY FUNCTIONAL — currently just saves a quick timestamped backup and shows a message.
        /// </summary>
        private void ShowSaveAsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Quick Timestamped Backup", () =>
                {
                    var modSettings = CAPChatInteractiveMod.Instance?.Settings;
                    if (modSettings != null)
                    {
                        JsonFileManager.SaveSettingsBackup(modSettings);
                        Messages.Message("Quick timestamped backup saved.", MessageTypeDefOf.NeutralEvent);
                    }
                }),
                new FloatMenuOption("Save as Named Theme (custom name)", () =>
                {
                    var modSettings = CAPChatInteractiveMod.Instance?.Settings;
                    if (modSettings != null)
                    {
                        Find.WindowStack.Add(new Dialog_TextInput(
                            "Enter backup name (e.g. GrimwarSettings)",
                            name =>
                            {
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    JsonFileManager.SaveSettingsBackup(modSettings);
                                    Messages.Message(
                                        $"Named backup saved as {name} (timestamped).",
                                        MessageTypeDefOf.NeutralEvent);
                                }
                            }));
                    }
                })
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLoadFileMenu()
        {
            var backup = JsonFileManager.LoadLatestSettingsBackup();
            if (backup != null)
            {
                JsonFileManager.ApplyBackupToCurrentSettings(backup);
                Messages.Message("Settings loaded from latest backup.", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("No backup found.", MessageTypeDefOf.RejectInput);
            }
        }

        private void ShowDeleteFileMenu()
        {
            Messages.Message(
                "Backup management for main settings is currently handled via the Backups folder.",
                MessageTypeDefOf.NeutralEvent);
        }
    }
}
