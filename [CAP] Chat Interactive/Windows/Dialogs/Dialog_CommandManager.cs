// Dialog_CommandManager.cs
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
// A dialog window for managing chat commands and their settings
using LudeonTK;
using Newtonsoft.Json;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CAP_ChatInteractive
{
    public class Dialog_CommandManager : Window
    {
        private Vector2 commandScrollPosition = Vector2.zero;
        private Vector2 detailsScrollPosition = Vector2.zero;
        private string searchQuery = "";
        private string lastSearch = "";
        public CommandSortMethod sortMethod = CommandSortMethod.Name;
        private bool sortAscending = true;
        public ChatCommandDef selectedCommand = null;
        public List<ChatCommandDef> filteredCommands = new List<ChatCommandDef>();
        public Dictionary<string, CommandSettings> commandSettings = new Dictionary<string, CommandSettings>();
        private Dictionary<string, string> numericBuffers = new Dictionary<string, string>();
        private Dictionary<CommandSettings, Dictionary<string, string>> commandSpecificBuffers = new Dictionary<CommandSettings, Dictionary<string, string>>();
        private CAPGlobalChatSettings settingsGlobalChat;

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public Dialog_CommandManager()
        {
            doCloseButton = false;   // WHY: We draw our own 5-button bar at bottom (Save Backup | Load Backup | Save As... | Load file | Close)
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Command Management"; Created in DrawHeader instead
            settingsGlobalChat = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;

            LoadCommandSettings();
            FilterCommands();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Update search if query changed
            if (searchQuery != lastSearch || filteredCommands.Count == 0)
            {
                FilterCommands();
            }

            float bottomBarHeight = 50f; // Space for the 5-button bar

            // Header - increased height for two rows
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f);
            DrawHeader(headerRect);

            // Main content area — leave room at bottom for button bar
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - bottomBarHeight);
            DrawContent(contentRect);

            // ========== BOTTOM BUTTON BAR (Save Backup | Load Backup | Save As... | Load file | Close) ==========
            // WHY: Matches the Global Settings pattern the user requested. Uses the new reusable BackupUtility
            // so the exact same buttons/logic can be dropped into Store, Traits, Incidents, Weather, RaceSettings, etc.
            float btnH = 38f;
            float btnW = 140f;
            float gap = 8f;
            float padding = 12f;
            float currentY = inRect.yMax - bottomBarHeight + (bottomBarHeight - btnH) / 2f;

            // Save Backup (quick timestamped)
            Rect saveRect = new Rect(padding, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveRect, "RICS.Editor.SaveBackup".Translate()))
            {
                string json = JsonConvert.SerializeObject(commandSettings, Formatting.Indented);
                BackupUtility.SaveQuickBackup("CommandManager", json);
                Messages.Message("RICS.Editor.BackupSaved".Translate(), MessageTypeDefOf.NeutralEvent);
            }

            // Load Backup (latest timestamped)
            float loadX = padding + btnW + gap;
            Rect loadRect = new Rect(loadX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadRect, "RICS.Editor.LoadBackup".Translate()))
            {
                string json = BackupUtility.LoadLatestTimestampedBackup("CommandManager");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        commandSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json)
                                         ?? new Dictionary<string, CommandSettings>();
                        FilterCommands();
                        Messages.Message("RICS.Editor.Loaded".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to apply loaded backup: {ex.Message}");
                        Messages.Message("RICS.Editor.FailedLoad".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }
                else
                {
                    Messages.Message("RICS.Editor.NoBackups".Translate(), MessageTypeDefOf.RejectInput);
                }
            }

            // Save As... (opens FloatMenu of options; currently saves with sensible named default)
            float saveAsX = loadX + btnW + gap;
            Rect saveAsRect = new Rect(saveAsX, currentY, btnW, btnH);
            if (Widgets.ButtonText(saveAsRect, "RICS.Editor.SaveAs".Translate()))
            {
                ShowSaveAsMenu();
            }

            // Load file (shows all backups in FloatMenu — timestamped + any named/theme files)
            float loadFileX = saveAsX + btnW + gap;
            Rect loadFileRect = new Rect(loadFileX, currentY, btnW, btnH);
            if (Widgets.ButtonText(loadFileRect, "RICS.Editor.LoadFile".Translate()))
            {
                ShowLoadFileMenu();
            }

            // Delete file (right next to Load file, with confirmation)
            float deleteX = loadFileX + btnW + gap;
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

        public override void PostClose()
        {
            base.PostClose();
            SaveCommandSettings();

            // Force save the mod settings
            if (CAPChatInteractiveMod.Instance?.Settings != null)
            {
                CAPChatInteractiveMod.Instance.Settings.Write();
                Logger.Debug("Forced mod settings save from Command Manager");
            }
        }

        private void LoadCommandSettings()
        {
            // Load from JSON file
            string json = JsonFileManager.LoadFile("CommandSettings.json");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    // Load with commandText keys (what command processor uses)
                    commandSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json)
                                     ?? new Dictionary<string, CommandSettings>();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading command settings: {ex}");
                    commandSettings = new Dictionary<string, CommandSettings>();
                }
            }

            // Initialize settings for all commands using commandText as key
            foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefs)
            {
                if (!string.IsNullOrEmpty(commandDef.commandText))
                {
                    string commandKey = commandDef.commandText.ToLowerInvariant();

                    if (!commandSettings.ContainsKey(commandKey))
                    {
                        // Create new settings with XML defaults
                        var settings = new CommandSettings();
                        settings.PermissionLevel = commandDef.permissionLevel; // ← IMPORTANT!
                        settings.CooldownSeconds = commandDef.cooldownSeconds;
                        // Set other defaults from XML as needed

                        commandSettings[commandKey] = settings;
                    }
                    else
                    {
                        // Update existing settings if they're still at defaults
                        var existing = commandSettings[commandKey];
                        if (existing.PermissionLevel == "everyone") // Still default
                        {
                            existing.PermissionLevel = commandDef.permissionLevel;
                        }
                        if (existing.CooldownSeconds == 0) // Still default
                        {
                            existing.CooldownSeconds = commandDef.cooldownSeconds;
                        }
                    }

                    // Ensure custom settings declared in the Def have entries (with defaults) in CustomData
                    var s = commandSettings[commandKey];
                    if (commandDef.CustomData != null && commandDef.CustomData.Count > 0)
                    {
                        s.EnsureCustomDefaults(commandDef.CustomData);
                    }
                }
            }
        }

        public void SaveCommandSettings()
        {
            try
            {
                // Save using the same commandText keys we loaded with
                string json = JsonConvert.SerializeObject(commandSettings, Formatting.Indented);
                JsonFileManager.SaveFile("CommandSettings.json", json);
                Logger.Debug("Saved command settings to JSON");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving command settings: {ex}");
            }
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title row - Orange with underline
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Rect titleRect = new Rect(0f, 0f, 240f, 30f);
            Widgets.Label(titleRect, "CAP.CommandManager.Title".Translate());

            // Draw underline
            Rect underlineRect = new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f);
            Widgets.DrawLineHorizontal(underlineRect.x, underlineRect.y, underlineRect.width);

            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Second row for controls - positioned lower to avoid cutoff
            float controlsY = 40f; // Increased from 35f to 40f for better spacing

            // Search bar
            Rect searchRect = new Rect(0f, controlsY, 170f, 30f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(180f, controlsY, 300f, 30f);
            DrawSortButtons(sortRect);

            // Settings gear icon - adjusted position for taller header
            Rect settingsRect = new Rect(rect.width - 30f, 10f, 24f, 24f); // Moved down from 5f to 10f
            Texture2D gearIcon = ContentFinder<Texture2D>.Get("UI/Icons/Options/OptionsGeneral", false);
            if (gearIcon != null)
            {
                if (Widgets.ButtonImage(settingsRect, gearIcon))
                {
                    ShowCommandSettingsMenu();
                }
            }
            else
            {
                // Fallback text button
                if (Widgets.ButtonText(new Rect(rect.width - 80f, 10f, 75f, 24f), "CAP.CommandManager.Settings".Translate()))
                {
                    ShowCommandSettingsMenu();
                }
            }

            Widgets.EndGroup();
        }

        private void DrawSortButtons(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 90f;
            float spacing = 5f;
            float x = 0f;

            // Sort by Name
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "CAP.CommandManager.Sort.Name".Translate()))
            {
                if (sortMethod == CommandSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = CommandSortMethod.Name;
                SortCommands();
            }
            x += buttonWidth + spacing;

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "CAP.CommandManager.Sort.Category".Translate()))
            {
                if (sortMethod == CommandSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = CommandSortMethod.Category;
                SortCommands();
            }
            x += buttonWidth + spacing;

            // Sort by Status
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "CAP.CommandManager.Sort.Status".Translate()))
            {
                if (sortMethod == CommandSortMethod.Status)
                    sortAscending = !sortAscending;
                else
                    sortMethod = CommandSortMethod.Status;
                SortCommands();
            }

            Widgets.EndGroup();
        }

        private void DrawContent(Rect rect)
        {
            float listWidth = 250f;
            float detailsWidth = rect.width - listWidth - 10f;

            Rect listRect = new Rect(rect.x, rect.y, listWidth, rect.height);
            Rect detailsRect = new Rect(rect.x + listWidth + 10f, rect.y, detailsWidth, rect.height);

            DrawCommandList(listRect);
            DrawCommandDetails(detailsRect);
        }

        private void DrawCommandList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = ColorLibrary.SubHeader;
            Widgets.Label(headerRect, "CAP.CommandManager.Commands".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Command list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f - 4);
            float rowHeight = 35f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, (filteredCommands.Count * rowHeight - 4f) ); // -4f to pull the bottom up se we are not covering the box.

            Widgets.BeginScrollView(listRect, ref commandScrollPosition, viewRect);
            {
                float y = 0f;
                for (int i = 0; i < filteredCommands.Count; i++)
                {
                    var command = filteredCommands[i];
                    string commandKey = command.commandText?.ToLowerInvariant() ?? command.defName.ToLowerInvariant();
                    var settings = commandSettings.ContainsKey(commandKey) ? commandSettings[commandKey] : new CommandSettings();

                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Command name with status indicator
                    string displayName = $"!{command.commandText}";
                    if (!settings.Enabled)
                        displayName += " " + "CAP.CommandManager.Disabled".Translate().Colorize(Color.red);

                    // Color coding based on permission level
                    Color buttonColor = GetCommandColor(command);
                    bool isSelected = selectedCommand == command;

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
                        selectedCommand = command;
                    }
                    GUI.color = Color.white;

                    y += rowHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private Color GetCommandColor(ChatCommandDef command)
        {
            // Use commandText as key, not defName
            string commandKey = command.commandText?.ToLowerInvariant() ?? command.defName.ToLowerInvariant();

            if (!commandSettings.ContainsKey(commandKey))
                return Color.gray;

            var settings = commandSettings[commandKey];
            if (!settings.Enabled) return Color.gray;

            return command.permissionLevel switch
            {
                "broadcaster" => new Color(0.9f, 0.3f, 0.3f),
                "moderator" => new Color(0.2f, 0.8f, 0.2f),
                "vip" => new Color(0.8f, 0.6f, 0.2f),
                "subscriber" => new Color(0.4f, 0.6f, 1f),
                _ => Color.white
            };
        }

        private void DrawCommandDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            if (selectedCommand == null)
            {
                Rect messageRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(messageRect, "CAP.CommandManager.SelectCommand".Translate());

                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            string commandKey = selectedCommand.commandText?.ToLowerInvariant() ?? selectedCommand.defName.ToLowerInvariant();
            if (!commandSettings.TryGetValue(commandKey, out var settings))
            {
                settings = new CommandSettings();
            }
            // var settings = commandSettings.ContainsKey(commandKey) ? commandSettings[commandKey] : new CommandSettings();

            // Header with command name
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            string headerText = $"!{selectedCommand.commandText}";
            if (!settings.Enabled)
                headerText += " " + "CAP.CommandManager.Disabled".Translate().Colorize(Color.red);

            Widgets.Label(headerRect, headerText);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Details content with scrolling
            Rect contentRect = new Rect(rect.x, rect.y + 50f, rect.width, rect.height - 60f);
            DrawCommandDetailsContent(contentRect, settings);
        }

        private void DrawCommandDetailsContent(Rect rect, CommandSettings settings)
        {
            float contentWidth = rect.width - 30f;
            float viewHeight = CalculateDetailsHeight(settings);
            Rect viewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(viewHeight, rect.height));

            Widgets.BeginScrollView(rect, ref detailsScrollPosition, viewRect);
            {
                float y = 0f;
                float sectionHeight = 28f;
                float leftPadding = 15f;

                // Basic Info section
                Rect basicLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(basicLabelRect, "CAP.CommandManager.BasicInfo".Translate());
                y += sectionHeight;

                // Command text
                Rect commandTextRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                Widgets.Label(commandTextRect, "CAP.CommandManager.Trigger".Translate() + $" !{selectedCommand.commandText}");
                y += sectionHeight;

                // Description
                Rect descRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight * 2);
                string desc = string.IsNullOrEmpty(selectedCommand.commandDescription) ?
                    "CAP.CommandManager.NoDescription".Translate() : selectedCommand.commandDescription;
                Widgets.Label(descRect, "CAP.CommandManager.Description".Translate() + $" {desc}");
                y += sectionHeight * 2;

                // Permission level
                Rect permRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                string key = $"CAP.CommandManager.PermissionLevel.{selectedCommand.permissionLevel.ToString().ToLowerInvariant()}";
                string permLevelTranslated = key.Translate();

                // fallback in case someone removes the key (rare but nice)
                if (permLevelTranslated == key)
                {
                    permLevelTranslated = selectedCommand.permissionLevel.ToString().CapitalizeFirst();
                }

                Widgets.Label(permRect, "CAP.CommandManager.PermissionLevel".Translate() + ": " + permLevelTranslated);
                y += sectionHeight;

                y += 10f;

                // Basic Settings section
                Rect settingsLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(settingsLabelRect, "CAP.CommandManager.BasicSettings".Translate());
                y += sectionHeight;

                // Enabled toggle — changed to CheckboxLabeled (standard RimWorld pattern) ver 1.37
                Rect enabledRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                bool wasEnabled = settings.Enabled;
                Widgets.CheckboxLabeled(enabledRect, "CAP.CommandManager.Enabled".Translate(), ref settings.Enabled);

                // Play click sound when toggled (vanilla behavior)
                if (settings.Enabled != wasEnabled)
                {
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                y += sectionHeight;


                // Cooldown setting 
                Rect cooldownLabelRect = new Rect(leftPadding + 10f, y, 250f, sectionHeight);
                Widgets.Label(cooldownLabelRect, "CAP.CommandManager.Cooldown".Translate());

                Rect cooldownInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                // Create a unique key for this setting
                string cooldownKey = $"cooldown_{settings.GetHashCode()}";

                // Initialize buffer if needed
                if (!numericBuffers.ContainsKey(cooldownKey))
                {
                    numericBuffers[cooldownKey] = settings.CooldownSeconds.ToString();
                }

                string cooldownBuffer = numericBuffers[cooldownKey];
                Widgets.TextFieldNumeric(cooldownInputRect, ref settings.CooldownSeconds, ref cooldownBuffer, 0f, 300f);
                numericBuffers[cooldownKey] = cooldownBuffer;

                // Description for cooldown — wider + height for wrapping (fixes cutoff) ver 1.37
                Rect cooldownDescRect = new Rect(leftPadding + 10f, y + sectionHeight - 4f, viewRect.width - leftPadding - 20f, 40f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(cooldownDescRect, "CAP.CommandManager.CooldownDesc".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                y += sectionHeight + 38f; // Extra vertical space so next section doesn't overlap

                // Cost setting (if applicable)
                if (settings.SupportsCost)
                {
                    Rect costRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(costRect, string.Format("CAP.CommandManager.Cost".Translate(), settingsGlobalChat.CurrencyName));
                    Rect costInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    string costKey = $"cost_{settings.GetHashCode()}";
                    if (!numericBuffers.ContainsKey(costKey))
                    {
                        numericBuffers[costKey] = settings.Cost.ToString();
                    }

                    string costBuffer = numericBuffers[costKey];
                    Widgets.TextFieldNumeric(costInputRect, ref settings.Cost, ref costBuffer, 0, 10000);
                    numericBuffers[costKey] = costBuffer; // Update the buffer

                    y += sectionHeight;
                }

                y += 10f;

                // Advanced Settings section
                Rect advancedLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(advancedLabelRect, "CAP.CommandManager.AdvancedSettings".Translate());
                y += sectionHeight;

                // Command alias - NEW: Allow custom command aliases
                Rect aliasLabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(aliasLabelRect, "CAP.CommandManager.CommandAlias".Translate());
                Rect aliasInputRect = new Rect(viewRect.width - 200f, y, 180f, sectionHeight); // Wider input

                // Get global settings for prefixes
                var globalSettings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                string aliasText = settings.CommandAlias ?? ""; // Reusing this field for alias

                // Strip prefixes if they were entered
                if (!string.IsNullOrEmpty(aliasText))
                {
                    if (aliasText.StartsWith(globalSettings.Prefix))
                        aliasText = aliasText.Substring(globalSettings.Prefix.Length);
                    if (aliasText.StartsWith(globalSettings.BuyPrefix))
                        aliasText = aliasText.Substring(globalSettings.BuyPrefix.Length);
                }

                // Input field with placeholder text
                aliasText = Widgets.TextField(aliasInputRect, aliasText);
                settings.CommandAlias = aliasText; // Store without prefixes

                y += sectionHeight;

                // Description for alias — wider rect + more height for long translations ver 1.37
                Rect aliasDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 20f, 38f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                string aliasDesc = "CAP.CommandManager.AliasDesc".Translate(globalSettings.Prefix, globalSettings.BuyPrefix);
                Widgets.Label(aliasDescRect, aliasDesc);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 40f; // Increased spacing after the potentially multi-line description

                // Event Command Settings - Only show for commands like raid, militaryaid
                Rect eventHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(eventHeaderRect, "CAP.CommandManager.CooldownSettings".Translate());
                y += sectionHeight;

                // Use per-command cooldown toggle
                Rect toggleRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.CheckboxLabeled(toggleRect, "CAP.CommandManager.UseCommandCooldown".Translate(), ref settings.useCommandCooldown);
                TooltipHandler.TipRegion(toggleRect, "CAP.CommandManager.UseCommandCooldownDesc".Translate());
                y += sectionHeight + 4f;  // Small extra spacing after checkbox looks nicer

                // Max uses per cooldown period
                Rect labelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 110f, sectionHeight);
                Widgets.Label(labelRect, "CAP.CommandManager.MaxUsesPerCooldown".Translate());
                TooltipHandler.TipRegion(labelRect, "CAP.CommandManager.MaxUsesPerCooldownDesc".Translate());

                Rect inputRect = new Rect(viewRect.width - 100f, y, 90f, sectionHeight);  // Slightly wider input for comfort
                string buffer = settings.MaxUsesPerCooldownPeriod.ToStringCached();       // Use cached ToString if available, minor perf
                Widgets.TextFieldNumeric(inputRect, ref settings.MaxUsesPerCooldownPeriod, ref buffer, 0, 10000);

                // Optional: Gray helper text when 0 (only if space allows)
                if (settings.MaxUsesPerCooldownPeriod == 0)
                {
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Rect noteRect = new Rect(leftPadding + 25f, y + sectionHeight + 2f, viewRect.width - leftPadding - 30f, 20f);
                    Widgets.Label(noteRect, "CAP.CommandManager.UnlimitedUses".Translate());
                    GUI.color = Color.white;
                }
                y += sectionHeight + 8f;  // Extra spacing before next section


                // === COMMAND-SPECIFIC / CUSTOM SETTINGS (from ChatCommandDef.CustomData definition) ===
                // Generic support for per-command extras declared in XML (e.g. addon toggles).
                // Values stored in settings.CustomData as JSON. Uses existing buffers for numeric fields.
                if (selectedCommand != null && selectedCommand.CustomData != null && selectedCommand.CustomData.Count > 0)
                {
                    y += 6f;
                    Rect customHeader = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(customHeader, "CAP.CommandManager.CommandSpecificSettings".Translate());
                    y += sectionHeight;

                    string cmdKey = selectedCommand.commandText?.ToLowerInvariant() ?? selectedCommand.defName.ToLowerInvariant();
                    // Ensure defaults for this command's schema (idempotent)
                    settings.EnsureCustomDefaults(selectedCommand.CustomData);

                    foreach (var cset in selectedCommand.CustomData)
                    {
                        string displayLabel = !string.IsNullOrEmpty(cset.label) ? cset.label : cset.name;
                        string tip = !string.IsNullOrEmpty(cset.description) ? cset.description : "";

                        string t = (cset.type ?? "").ToLowerInvariant();

                        if (t == "label")
                        {
                            // Label: just show as (bold) text / header. Can be used for "Raid Advanceded Settings" etc.
                            Rect lRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 20f, sectionHeight);
                            Text.Font = GameFont.Medium;
                            Widgets.Label(lRect, displayLabel);
                            Text.Font = GameFont.Small;
                            if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(lRect, tip);
                            y += sectionHeight;
                        }
                        else if (t == "checkbox" || t == "bool")
                        {
                            bool val = settings.GetCustom<bool>(cset.name, bool.TryParse(cset.defaultValue, out var b) ? b : false);
                            Rect cbRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 20f, sectionHeight);
                            bool was = val;
                            Widgets.CheckboxLabeled(cbRect, displayLabel, ref val);
                            if (val != was)
                            {
                                settings.SetCustom(cset.name, val);
                                SoundDefOf.Click.PlayOneShotOnCamera();
                            }
                            if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(cbRect, tip);
                            y += sectionHeight;
                        }
                        else if (t == "numerictextbox" || t == "int" || t == "float")
                        {
                            Rect lRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                            Widgets.Label(lRect, displayLabel);
                            if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(lRect, tip);

                            Rect iRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                            string bufKey = $"custom_{cmdKey}_{cset.name}_{settings.GetHashCode()}";
                            if (!numericBuffers.ContainsKey(bufKey))
                            {
                                string cur = settings.GetCustom<string>(cset.name, cset.defaultValue ?? (t == "int" ? "0" : "0.0"));
                                numericBuffers[bufKey] = cur;
                            }
                            string buf = numericBuffers[bufKey];

                            if (t == "int")
                            {
                                int iv = settings.GetCustom<int>(cset.name, int.TryParse(cset.defaultValue, out var di) ? di : 0);
                                Widgets.TextFieldNumeric(iRect, ref iv, ref buf, (int)Mathf.Max(cset.min, int.MinValue), (int)Mathf.Min(cset.max, int.MaxValue));
                                if (iv != settings.GetCustom<int>(cset.name)) settings.SetCustom(cset.name, iv);
                            }
                            else
                            {
                                float fv = settings.GetCustom<float>(cset.name, float.TryParse(cset.defaultValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var df) ? df : 0f);
                                UIUtilities.TextFieldNumericFlexible(iRect, ref fv, ref buf, cset.min, cset.max);
                                if (!Mathf.Approximately(fv, settings.GetCustom<float>(cset.name))) settings.SetCustom(cset.name, fv);
                            }
                            numericBuffers[bufKey] = buf;
                            y += sectionHeight;
                        }
                        else // LabelTextBox or string / default
                        {
                            Rect lRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 200f, sectionHeight);
                            Widgets.Label(lRect, displayLabel);
                            if (!string.IsNullOrEmpty(tip)) TooltipHandler.TipRegion(lRect, tip);

                            Rect iRect = new Rect(viewRect.width - 190f, y, 170f, sectionHeight);
                            string cur = settings.GetCustom<string>(cset.name, cset.defaultValue ?? "");
                            string newVal = Widgets.TextField(iRect, cur);
                            if (newVal != cur) settings.SetCustom(cset.name, newVal);
                            y += sectionHeight;
                        }
                    }
                    y += 6f;
                }


                // RAID-SPECIFIC SETTINGS - Only show for raid command
                if (selectedCommand.commandText.ToLower() == "raid")
                {
                    DrawRaidSpecificSettings(viewRect, settings, ref y);
                }

                // MILITARY AID-SPECIFIC SETTINGS - Only show for militaryaid command
                if (selectedCommand.commandText.ToLower() == "militaryaid")
                {
                    DrawMilitaryAidSpecificSettings(viewRect, settings, ref y);
                }

                // LOOTBOX-SPECIFIC SETTINGS - Only show for openlootboxes command
                if (selectedCommand.commandText.ToLower() == "openlootbox")
                {
                    y += 10f; // Extra spacing

                    Rect lootboxHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(lootboxHeaderRect, "CAP.CommandManager.LootboxSettings".Translate().Colorize(ColorLibrary.SubHeader));
                    y += sectionHeight;

                    // Coin range
                    Rect coinRangeRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(coinRangeRect, "CAP.CommandManager.LootboxCoinRange".Translate());
                    y += sectionHeight;

                    // Min coin input
                    Rect coinMinRect = new Rect(leftPadding + 20f, y, 80f, sectionHeight);
                    Widgets.Label(coinMinRect, "CAP.CommandManager.Min".Translate());
                    Rect coinMinInputRect = new Rect(leftPadding + 60f, y, 60f, sectionHeight);

                    // Use buffer pattern for min coin
                    string coinMinKey = "lootbox_mincoin";
                    if (!numericBuffers.ContainsKey(coinMinKey))
                    {
                        numericBuffers[coinMinKey] = settingsGlobalChat.LootBoxRandomCoinRange.min.ToString();
                    }
                    string coinMinBuffer = numericBuffers[coinMinKey];
                    Widgets.TextFieldNumeric(coinMinInputRect, ref settingsGlobalChat.LootBoxRandomCoinRange.min, ref coinMinBuffer, 1f, 10000f);
                    numericBuffers[coinMinKey] = coinMinBuffer;

                    // Max coin input
                    Rect coinMaxRect = new Rect(leftPadding + 140f, y, 80f, sectionHeight);
                    Widgets.Label(coinMaxRect, "CAP.CommandManager.Max".Translate());
                    Rect coinMaxInputRect = new Rect(leftPadding + 180f, y, 60f, sectionHeight);

                    // Use buffer pattern for max coin
                    string coinMaxKey = "lootbox_maxcoin";
                    if (!numericBuffers.ContainsKey(coinMaxKey))
                    {
                        numericBuffers[coinMaxKey] = settingsGlobalChat.LootBoxRandomCoinRange.max.ToString();
                    }
                    string coinMaxBuffer = numericBuffers[coinMaxKey];
                    Widgets.TextFieldNumeric(coinMaxInputRect, ref settingsGlobalChat.LootBoxRandomCoinRange.max, ref coinMaxBuffer, 1f, 10000f);
                    numericBuffers[coinMaxKey] = coinMaxBuffer;

                    y += sectionHeight;

                    // Lootboxes per day
                    Rect perDayRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(perDayRect, "CAP.CommandManager.LootboxPerDay".Translate());
                    Rect perDayInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    // Use buffer pattern for per day
                    string perDayKey = "lootbox_perday";
                    if (!numericBuffers.ContainsKey(perDayKey))
                    {
                        numericBuffers[perDayKey] = settingsGlobalChat.LootBoxesPerDay.ToString();
                    }
                    string perDayBuffer = numericBuffers[perDayKey];
                    Widgets.TextFieldNumeric(perDayInputRect, ref settingsGlobalChat.LootBoxesPerDay, ref perDayBuffer, 1, 20);
                    numericBuffers[perDayKey] = perDayBuffer;

                    y += sectionHeight;

                    // Show welcome message
                    Rect welcomeRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.CheckboxLabeled(welcomeRect, "CAP.CommandManager.ShowWelcomeMessage".Translate(), ref settingsGlobalChat.LootBoxShowWelcomeMessage);
                    y += sectionHeight;

                    // Force open all at once
                    Rect forceOpenRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.CheckboxLabeled(forceOpenRect, "CAP.CommandManager.ForceOpen".Translate(), ref settingsGlobalChat.LootBoxForceOpenAllAtOnce);
                    y += sectionHeight;
                }

                // PASSION-SPECIFIC SETTINGS have been migrated to per-command CustomData (see <CustomData> in the Passion def).
                // They are now rendered by the generic dynamic custom block (Labels for sections + NumericTextBox fields).
                // The old hardcoded block was removed to avoid drawing the old global + new system.
                // If a thin "Passion Command Settings" header + reset is desired later, it can go here (after the custom items).

                // SURGERY-SPECIFIC SETTINGS have been migrated to per-command CustomData.
                // Rendered by the generic dynamic section using CheckBox + NumericTextBox pairs.
                // Old paired checkbox+cost drawing removed to avoid duplicating the old+new system.

                // SHUFFLE CHILDHOOD-SPECIFIC SETTINGS
                if (selectedCommand != null && selectedCommand.commandText.ToLower() == "shufflechildhood")
                {
                    y += 10f; // Extra spacing

                    Rect headerRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(headerRect, "CAP.CommandManager.ShuffleChildhoodSettings".Translate().Colorize(ColorLibrary.SubHeader));
                    y += sectionHeight;

                    Rect ChildhoodlabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(ChildhoodlabelRect, "CAP.CommandManager.ChildhoodWager".Translate());

                    Rect ChildhoodinputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string bufferKey = "shuffle_childhood_wager";
                    if (!numericBuffers.ContainsKey(bufferKey))
                        numericBuffers[bufferKey] = settingsGlobalChat.ChildhoodWager.ToString();

                    string Childhoodbuffer = numericBuffers[bufferKey];
                    Widgets.TextFieldNumeric(ChildhoodinputRect, ref settingsGlobalChat.ChildhoodWager, ref Childhoodbuffer, 1, 100000);
                    numericBuffers[bufferKey] = Childhoodbuffer;

                    y += sectionHeight + 8f; // small padding
                }

                // SHUFFLE ADULTHOOD-SPECIFIC SETTINGS
                if (selectedCommand != null && selectedCommand.commandText.ToLower() == "shuffleadulthood")
                {
                    y += 10f; // Extra spacing

                    Rect headerRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(headerRect, "CAP.CommandManager.ShuffleAdulthoodSettings".Translate().Colorize(ColorLibrary.SubHeader));
                    y += sectionHeight;

                    Rect AdulthoodlabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(AdulthoodlabelRect, "CAP.CommandManager.AdulthoodWager".Translate());

                    Rect AdulthoodinputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string bufferKey = "shuffle_adulthood_wager";
                    if (!numericBuffers.ContainsKey(bufferKey))
                        numericBuffers[bufferKey] = settingsGlobalChat.AdulthoodWager.ToString();

                    string Adulthoodbuffer = numericBuffers[bufferKey];
                    Widgets.TextFieldNumeric(AdulthoodinputRect, ref settingsGlobalChat.AdulthoodWager, ref Adulthoodbuffer, 1, 100000);
                    numericBuffers[bufferKey] = Adulthoodbuffer;

                    y += sectionHeight + 8f; // small padding
                }


            }
            Widgets.EndScrollView();
        }

        private float CalculateDetailsHeight(CommandSettings settings)
        {
            float height = 70f; // Header space (increased for new header design)
            height += 28f * 5; // Basic info (command, desc, perm)
            height += 38f; // Basic settings label + spacing
            height += 28f; // Enabled
            height += 28f * 1.5f; // Cooldown (taller for description)
            if (settings.SupportsCost) height += 28f; // Cost if applicable
            height += 38f; // Advanced settings label + spacing
            height += 28f; // Command alias
            height += 38f; // ← Changed from 14f to 38f for alias description

            // Command Cooldown Settings (replaces the old game days cooldown)
            height += 28f; // Command Cooldown Settings header
            height += 28f; // Turn on for non event Commands toggle
            height += 28f; // Max uses per cooldown period

            // LOOTBOX-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "openlootbox")
            {
                height += 10f; // Extra spacing
                height += 28f; // Lootbox header
                height += 28f; // Coin range label
                height += 28f; // Coin range inputs (min/max on same line)
                height += 28f; // Lootboxes per day
                height += 28f; // Show welcome message
                height += 28f; // Force open all at once
                height += 14f; // Extra spacing for description
            }

            // PASSION height is now provided dynamically by the CustomData items (Labels + Numerics) in the generic section.
            // (The old per-passion hardcoded block was removed during migration to the new system.)

            // SURGERY height is now provided by the dynamic CustomData (CheckBoxes + Numerics + Labels).
            // (Old hardcoded block removed.)

            // SHUFFLE CHILDHOOD-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "shufflechildhood")
            {
                height += 10f;  // Extra spacing
                height += 28f;  // Header
                height += 28f;  // ChildhoodWager row
            }

            // SHUFFLE ADULTHOOD-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "shuffleadulthood")
            {
                height += 10f;  // Extra spacing
                height += 28f;  // Header
                height += 28f;  // AdulthoodWager row
            }

            // === DYNAMIC CUSTOM DATA HEIGHT (from selectedCommand.CustomData) ===
            // This must be calculated from the CustomData definition because:
            // - We are moving to dynamic per-command settings.
            // - Modders can add commands with arbitrary numbers/types of settings via <CustomData> in their XML.
            // - We only know the required space at runtime by inspecting the CustomData list.
            // This method will evolve as more commands (core + modder) add custom settings.
            if (selectedCommand != null && selectedCommand.CustomData != null && selectedCommand.CustomData.Count > 0)
            {
                height += 6f;   // spacing before custom section header
                height += 28f;  // "Command Specific Settings" header

                foreach (var cset in selectedCommand.CustomData)
                {
                    string t = (cset.type ?? "").ToLowerInvariant();

                    if (t == "label")
                    {
                        height += 28f;  // label row
                    }
                    else
                    {
                        height += 28f;  // input row (checkbox, textbox, numeric)
                        if (!string.IsNullOrEmpty(cset.description))
                            height += 14f; // extra for description tooltip area
                    }
                }

                height += 6f; // spacing after custom items
            }

            // RAID-SPECIFIC extra height for the list buttons drawn after custom wagers (types/strategies).
            // These are not part of CustomData so must be added here. Matches DrawRaidSpecificSettings + our added padding.
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "raid")
            {
                height += 10f;  // initial spacing in DrawRaidSpecificSettings
                height += 28f;  // "Raid Command Settings" header
                height += 8f;   // extra padding for the two buttons
                height += 28f;  // Configure Raid Types button
                height += 28f;  // Configure Strategies button
            }

            // MILITARY AID extra: now only the header line for reset button (wager fields come from dynamic CustomData).
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "militaryaid")
            {
                height += 10f;  // spacing
                height += 28f;  // Military header + reset line
            }

            return height + 40f; // Extra padding for safety
        }

        private void ShowCommandSettingsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
    {
        new FloatMenuOption("CAP.CommandManager.ResetAll".Translate(), () => ShowResetConfirmationDialog())  // // Do Tranlslate
    };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void FilterCommands()
        {
            lastSearch = searchQuery;
            filteredCommands.Clear();

            var allCommands = DefDatabase<ChatCommandDef>.AllDefs.AsEnumerable();

            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string searchLower = searchQuery.ToLower();
                allCommands = allCommands.Where(cmd =>
                    cmd.commandText.ToLower().Contains(searchLower) ||
                    cmd.defName.ToLower().Contains(searchLower) ||
                    cmd.commandDescription.ToLower().Contains(searchLower)
                );
            }

            filteredCommands = allCommands.ToList();
            SortCommands();
        }

        private void SortCommands()
        {
            switch (sortMethod)
            {
                case CommandSortMethod.Name:
                    filteredCommands = sortAscending ?
                        filteredCommands.OrderBy(c => c.commandText).ToList() :
                        filteredCommands.OrderByDescending(c => c.commandText).ToList();
                    break;
                case CommandSortMethod.Category:
                    filteredCommands = sortAscending ?
                        filteredCommands.OrderBy(c => GetCommandCategory(c)).ToList() :
                        filteredCommands.OrderByDescending(c => GetCommandCategory(c)).ToList();
                    break;
                case CommandSortMethod.Status:
                    filteredCommands = sortAscending ?
                        filteredCommands.OrderBy(c => commandSettings[c.defName].Enabled).ToList() :
                        filteredCommands.OrderByDescending(c => commandSettings[c.defName].Enabled).ToList();
                    break;
            }
        }

        private string GetCommandCategory(ChatCommandDef command)
        {
            // Categorize based on namespace or other criteria
            if (command.commandClass.FullName.Contains("ModCommands")) return "Moderator";
            if (command.commandClass.FullName.Contains("ViewerCommands")) return "Viewer";
            if (command.commandClass.FullName.Contains("TestCommands")) return "Test";
            return "Other";
        }



        public CommandSettings GetCommandSettings(string commandName)
        {
            if (commandSettings.ContainsKey(commandName))
            {
                return commandSettings[commandName];
            }
            return new CommandSettings();
        }

        private void ShowResetConfirmationDialog()
        {
            TaggedString warningText = "CAP.CommandManager.ResetWarning".Translate().Colorize(Verse.ColorLibrary.RedReadable);

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                warningText,
                () => ResetAllCommandsToDefaults(),
                true,
                "CAP.CommandManager.ResetCommands".Translate()
            ));
        }

        private void ResetAllCommandsToDefaults()
        {
            try
            {
                // Delete the command settings JSON file
                string filePath = JsonFileManager.GetFilePath("CommandSettings.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted CommandSettings.json file");
                }
                else
                {
                    Logger.Message("No CommandSettings.json file found to delete");
                }
                // Rebuild command settings from scratch
                commandSettings.Clear();
                LoadCommandSettings(); // This will recreate defaults
                // Refresh the UI
                FilterCommands();
                Logger.Message("Command settings reset to defaults");
                Messages.Message("CAP.CommandManager.ResetMessage".Translate(), MessageTypeDefOf.TaskCompletion); // Do Tranlslate
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resetting commands: {ex.Message}");
                Messages.Message(string.Format("CAP.CommandManager.ResetError".Translate(), ex.Message),
                 MessageTypeDefOf.NegativeEvent);
            }
        }


        private void ShowSaveAsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Quick Timestamped Backup", () =>
                {
                    string json = JsonConvert.SerializeObject(commandSettings, Formatting.Indented);
                    BackupUtility.SaveQuickBackup("CommandManager", json);
                    Messages.Message("Quick timestamped backup saved.", MessageTypeDefOf.NeutralEvent);
                }),
                new FloatMenuOption("Save as Named Theme (custom name)", () =>
                {
                    // Uses the new reusable Dialog_TextInput for true custom naming
                    string json = JsonConvert.SerializeObject(commandSettings, Formatting.Indented);

                    Find.WindowStack.Add(new Dialog_TextInput(
                        "Enter backup name (e.g. Grimwar, RimMagic)",
                        name =>
                        {
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                BackupUtility.SaveNamedBackup("CommandManager", name, json);
                                Messages.Message($"Named backup saved as {name}.json", MessageTypeDefOf.NeutralEvent);
                            }
                            else
                            {
                                // Fallback if user enters nothing
                                string fallback = "CustomTheme_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
                                BackupUtility.SaveNamedBackup("CommandManager", fallback, json);
                                Messages.Message($"Saved with fallback name: {fallback}.json", MessageTypeDefOf.NeutralEvent);
                            }
                        }));
                })
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLoadFileMenu()
        {
            var files = BackupUtility.GetAllBackupFiles("CommandManager");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Command Manager.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var file in files)
            {
                options.Add(new FloatMenuOption(file, () =>
                {
                    string json = BackupUtility.LoadBackupFile("CommandManager", file);
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            commandSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json)
                                             ?? new Dictionary<string, CommandSettings>();
                            FilterCommands();
                            Messages.Message($"Loaded backup: {file}", MessageTypeDefOf.NeutralEvent);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to load {file}: {ex.Message}");
                            Messages.Message("Failed to load selected backup.", MessageTypeDefOf.RejectInput);
                        }
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowDeleteFileMenu()
        {
            var files = BackupUtility.GetAllBackupFiles("CommandManager");
            if (files.Count == 0)
            {
                Messages.Message("No backup files found for Command Manager.", MessageTypeDefOf.RejectInput);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (var file in files)
            {
                options.Add(new FloatMenuOption(file, () =>
                {
                    // Confirmation before delete (good UX)
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Delete backup file?\n{file}\n\nThis cannot be undone.",
                        () =>
                        {
                            bool deleted = BackupUtility.DeleteBackupFile("CommandManager", file);
                            if (deleted)
                            {
                                Messages.Message($"Deleted: {file}", MessageTypeDefOf.NeutralEvent);
                            }
                        },
                        true,
                        "Delete Backup"
                    ));
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ======================================================================
        // NEW: Dedicated method for Raid command settings (easier to maintain)
        // ======================================================================
        private void DrawRaidSpecificSettings(Rect viewRect, CommandSettings settings, ref float y)
        {
            float sectionHeight = 28f;
            float leftPadding = 15f;

            y += 10f; // Extra spacing

            Rect raidHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
            Widgets.Label(raidHeaderRect, "CAP.CommandManager.RaidSettings".Translate());
            y += sectionHeight;

            // === RESET BUTTON ===
            Rect resetRect = new Rect(viewRect.width - 180f, raidHeaderRect.y, 160f, sectionHeight - 2f);
            if (Widgets.ButtonText(resetRect, "Reset to Recommended"))
            {
                // Wager values are now only in CustomData (driven by <CustomData> in Commands.xml for the Raid command)
                settings.SetCustom("defaultRaidWager", 500);
                settings.SetCustom("minRaidWager", 100);
                settings.SetCustom("maxRaidWager", 2500);

                // Sync the display buffers used by the dynamic custom numeric fields so the reset is reflected in UI immediately.
                int hash = settings.GetHashCode();
                numericBuffers[$"custom_raid_defaultRaidWager_{hash}"] = "500";
                numericBuffers[$"custom_raid_minRaidWager_{hash}"] = "100";
                numericBuffers[$"custom_raid_maxRaidWager_{hash}"] = "2500";

                Messages.Message("Raid wagers reset to recommended values (500 / 100 / 2500).", MessageTypeDefOf.TaskCompletion);
            }
            TooltipHandler.TipRegion(resetRect, "Reset to new recommended pricing based on current viewer economy.");

            // Note: Wager fields are now driven by the <CustomData> definition above via the generic custom settings drawer.
            // The lists (types/strategies) remain here for their dedicated sub-editors.

            // Extra padding before the two list buttons (types/strategies) as requested.
            y += 8f;

            // Allowed raid types button
            Rect raidTypesRect = new Rect(leftPadding + 10f, y, 200f, sectionHeight);
            if (Widgets.ButtonText(raidTypesRect, "CAP.CommandManager.ConfigureRaidTypes".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RaidTypesEditor(settings));
            }
            y += sectionHeight;

            // Allowed strategies button
            Rect strategiesRect = new Rect(leftPadding + 10f, y, 200f, sectionHeight);
            if (Widgets.ButtonText(strategiesRect, "CAP.CommandManager.ConfigureStrategies".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RaidStrategiesEditor(settings));
            }
            y += sectionHeight;
        }

        // ======================================================================
        // NEW: Dedicated method for Military Aid command settings
        // ======================================================================
        private void DrawMilitaryAidSpecificSettings(Rect viewRect, CommandSettings settings, ref float y)
        {
            float sectionHeight = 28f;
            float leftPadding = 15f;

            y += 10f; // Extra spacing

            Rect militaryHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
            Widgets.Label(militaryHeaderRect, "CAP.CommandManager.MilitaryAidSettings".Translate().Colorize(ColorLibrary.SubHeader));
            y += sectionHeight;

            // === RESET BUTTON ===
            Rect resetRect = new Rect(viewRect.width - 180f, militaryHeaderRect.y, 160f, sectionHeight - 2f);
            if (Widgets.ButtonText(resetRect, "Reset to Recommended"))
            {
                // Values now stored via CustomData (per <CustomData> in XML)
                settings.SetCustom("defaultMilitaryAidWager", 300);
                settings.SetCustom("minMilitaryAidWager", 50);
                settings.SetCustom("maxMilitaryAidWager", 1500);

                // Sync display buffers (used by the generic CustomData numeric drawer) so reset reflects in UI.
                int hash = settings.GetHashCode();
                numericBuffers[$"custom_militaryaid_defaultMilitaryAidWager_{hash}"] = "300";
                numericBuffers[$"custom_militaryaid_minMilitaryAidWager_{hash}"] = "50";
                numericBuffers[$"custom_militaryaid_maxMilitaryAidWager_{hash}"] = "1500";

                Messages.Message("Military Aid wagers reset to recommended values (300 / 50 / 1500).", MessageTypeDefOf.TaskCompletion);
            }
            TooltipHandler.TipRegion(resetRect, "Reset to new recommended pricing based on current viewer economy.");

            // Wager value fields are rendered exclusively by the dynamic <CustomData> system (see XML definition).
            // Previously this method duplicated the inputs (old + new system). The fields above (under Command Specific Settings)
            // are the single source of truth now. Only header + reset remain here for consistency with Raid layout.
        }

        [DebugAction("CAP", "Delete JSON & Rebuild Commands", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRebuildCommands()
        {
            try
            {
                // Delete the command settings JSON file
                string filePath = JsonFileManager.GetFilePath("CommandSettings.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Message("Deleted CommandSettings.json file");
                }
                else
                {
                    Logger.Message("No CommandSettings.json file found to delete");
                }

                // Force reinitialization through the game component
                var comp = Current.Game.GetComponent<GameComponent_CommandsInitializer>();
                if (comp != null)
                {
                    // Use reflection to reset the initialization flag if needed
                    var field = typeof(GameComponent_CommandsInitializer).GetField("commandsInitialized",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(comp, false);
                    }
                }

                Logger.Message("Command settings will be rebuilt on next tick");
                Messages.Message("Command settings will be rebuilt on next tick", MessageTypeDefOf.TaskCompletion);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rebuilding commands: {ex.Message}");
                Messages.Message($"Error rebuilding commands: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

    }

    public enum CommandSortMethod
    {
        Name,
        Category,
        Status
    }


}