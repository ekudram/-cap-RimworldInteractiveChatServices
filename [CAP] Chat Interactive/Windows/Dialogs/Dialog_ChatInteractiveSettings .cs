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

// Sets up the Tabs and window for Settings, but the actual content of each tab is drawn in separate classes for better organization and maintainability.

using _CAP__Chat_Interactive;
using Newtonsoft.Json;
using RimWorld;
using System.Collections.Generic;
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
            doCloseButton = false;
            forcePause = false; // Changed from true to false since this is now the main settings
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;
            optionalTitle = "RICS.CIS.OptionalTitle".Translate();
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();

            // Register all tab drawers

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.Global".Translate(),
                Tooltip = "RICS.CIS.Global.Tooltip".Translate(),
                ContentDrawer = TabDrawer_Global.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.Twitch".Translate(),
                Tooltip = "RICS.CIS.Global.Tooltip".Translate(),
                ContentDrawer = TabDrawer_Twitch.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.YouTube".Translate(),
                Tooltip = "RICS.CIS.Twitch.Tooltip".Translate(),
                ContentDrawer = TabDrawer_YouTube.Draw
            });
            // /*
            // Kick tab — added for full multi-platform support (Discord/Steam-ready pattern)
            //_tabWorker.AddTab(new TabItem   // ← uncomment when you're ready to test
            //{
            //    Label = "RICS.CIS.Kick".Translate(),
            //    Tooltip = "RICS.CIS.Kick.Tooltip".Translate(),
            //    ContentDrawer = TabDrawer_Kick.Draw
            //});
            // */
            //_tabWorker.AddTab(new TabItem
            //{
            //    Label = "OAuth",
            //    Tooltip = "Configure YouTube OAuth settings",
            //    ContentDrawer = TabDrawer_OAuth.Draw
            //});

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.Economy".Translate(),
                Tooltip = "RICS.CIS.Economy.Tooltip".Translate(),
                ContentDrawer = TabDrawer_Economy.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.GameEvents".Translate(),
                Tooltip = "RICS.CIS.GameEvents.Tooltip".Translate(),
                ContentDrawer = TabDrawer_GameEvents.Draw
            });

            _tabWorker.AddTab(new TabItem
            {
                Label = "RICS.CIS.Rewards".Translate(),
                Tooltip = "RICS.CIS.Rewards.Tooltip".Translate(),
                ContentDrawer = TabDrawer_Rewards.Draw
            });
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Calculate areas — reserves space for our custom bottom button bar
            var tabBarRect = new Rect(0f, 0f, inRect.width, Text.LineHeight * 1.5f);
            float bottomBarHeight = 50f;
            var tabContentRect = new Rect(0f, tabBarRect.height, inRect.width, inRect.height - tabBarRect.height - bottomBarHeight);

            // Draw tab bar
            GUI.BeginGroup(tabBarRect);
            _tabWorker.Draw(tabBarRect.AtZero(), paneled: true);
            GUI.EndGroup();

            // Draw tab content
            GUI.BeginGroup(tabContentRect);
            _tabWorker.SelectedTab?.Draw(tabContentRect.AtZero());
            GUI.EndGroup();

            // ========== BOTTOM BUTTON BAR (Save Backup | Load Backup | Save As... | Load file | Delete file | Close) ==========
            // WHY: Now fully consistent with Command Manager, Events, Store, Traits, Weather, and Pawn Race Settings.
            float btnH = 38f;
            float btnW = 130f;
            float gap = 6f;
            float padding = 10f;
            float currentY = inRect.yMax - bottomBarHeight + (bottomBarHeight - btnH) / 2f;

            // Save Backup (quick timestamped)
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

            // Load Backup (latest timestamped)
            float loadX = padding + btnW + gap;
            Rect loadRect = new Rect(loadX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadRect, "RICS.Editor.LoadBackup".Translate()))
            {
                var backup = JsonFileManager.LoadLatestSettingsBackup();
                if (backup != null)
                {
                    JsonFileManager.ApplyBackupToCurrentSettings(backup);
                    Messages.Message("RICS settings restored from backup. Close and reopen this window to fully refresh.", MessageTypeDefOf.NeutralEvent);
                }
                else
                {
                    Messages.Message("No settings backup found.", MessageTypeDefOf.RejectInput);
                }
            }

            //// Save As... (custom named theme)
            float saveAsX = loadX + btnW + gap;
            //Rect saveAsRect = new Rect(saveAsX, currentY, btnW, btnH);
            //if (Widgets.ButtonText(saveAsRect, "Save As..."))
            //{
            //    ShowSaveAsMenu();
            //}

            //// Load file
            float loadFileX = saveAsX + btnW + gap;
            //Rect loadFileRect = new Rect(loadFileX, currentY, btnW, btnH);
            //if (Widgets.ButtonText(loadFileRect, "Load file"))
            //{
            //    ShowLoadFileMenu();
            //}

            // Delete file
            //float deleteX = loadFileX + btnW + gap;
            float deleteX = loadX + btnW + gap;
            Rect deleteRect = new Rect(deleteX, currentY, btnW, btnH);
            if (Widgets.ButtonText(deleteRect, "RICS.Editor.DeleteFile".Translate()))
            {
                ShowDeleteFileMenu();
            }

            // Close (right-aligned)
            float closeX = inRect.xMax - btnW - padding;
            Rect closeRect = new Rect(closeX, currentY, btnW, btnH);
            if (Widgets.ButtonText(closeRect, "RICS.Editor.Close".Translate()))
            {
                this.Close();
            }
        }

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
                        string json = JsonConvert.SerializeObject(modSettings, Formatting.Indented); // Safe because we control serialization in JsonFileManager
                        Find.WindowStack.Add(new Dialog_TextInput(
                            "Enter backup name (e.g. GrimwarSettings)",
                            name =>
                            {
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    // We can extend JsonFileManager later with a named version if needed.
                                    // For now we use quick save + message.
                                    JsonFileManager.SaveSettingsBackup(modSettings);
                                    Messages.Message($"Named backup saved as {name} (timestamped).", MessageTypeDefOf.NeutralEvent);
                                }
                            }));
                    }
                })
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLoadFileMenu()
        {
            // For main settings we keep it simple — Load Latest is the primary path.
            // Advanced named loading can be added later if needed.
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
            // Placeholder for future — main settings backups are managed in the Backups folder.
            Messages.Message("Backup management for main settings is currently handled via the Backups folder.", MessageTypeDefOf.NeutralEvent);
        }
    }
}