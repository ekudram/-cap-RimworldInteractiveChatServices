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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

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
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            // optionalTitle = "Command Management"; Created in DrawHeader instead
            settingsGlobalChat = CAPChatInteractiveMod.Instance.Settings.GlobalSettings; // <-- ADD THIS LINE

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

            // Header - increased height for two rows
            Rect headerRect = new Rect(0f, 0f, inRect.width, 70f); // Changed from 40f to 70f
            DrawHeader(headerRect);

            // Main content area - adjusted to start after the taller header
            Rect contentRect = new Rect(0f, 75f, inRect.width, inRect.height - 75f - CloseButSize.y); // Changed from 90f to 75f
            DrawContent(contentRect);
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
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
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
            Widgets.Label(headerRect, "CAP.CommandManager.Commands".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // Command list
            Rect listRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            float rowHeight = 35f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, filteredCommands.Count * rowHeight);

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

            Logger.Debug($"DrawCommandDetailsContent - rect.height: {rect.height}, viewHeight: {viewHeight}, viewRect.height: {viewRect.height}");

            Widgets.BeginScrollView(rect, ref detailsScrollPosition, viewRect);
            {
                float y = 0f;
                float sectionHeight = 28f;
                float leftPadding = 15f;

                float checkBoxWidth = 28f;
                float inputWidth = 80f;
                float gapAfterCheck = 8f;
                float gapBeforeInput = 12f;
                float labelWidth = viewRect.width - leftPadding - 10f - checkBoxWidth - gapAfterCheck - inputWidth - gapBeforeInput - 10f;

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

                // Enabled toggle
                Rect enabledRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(enabledRect, "CAP.CommandManager.Enabled".Translate());
                Rect enabledToggleRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                string toggleText = settings.Enabled ? "CAP.CommandManager.On".Translate() : "CAP.CommandManager.Off".Translate();
                if (Widgets.ButtonText(enabledToggleRect, toggleText))
                {
                    settings.Enabled = !settings.Enabled;
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

                // Description below
                Rect cooldownDescRect = new Rect(leftPadding + 10f, y + sectionHeight - 8f, viewRect.width - leftPadding, 14f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f); // Softer gray
                Widgets.Label(cooldownDescRect, "CAP.CommandManager.CooldownDesc".Translate());
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                y += sectionHeight + 10f; // Extra space for description

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

                // Description for alias
                Rect aliasDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, 14f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                string aliasDesc = "CAP.CommandManager.AliasDesc".Translate(globalSettings.Prefix, globalSettings.BuyPrefix);
                Widgets.Label(aliasDescRect, aliasDesc);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 20f;

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


                // RAID-SPECIFIC SETTINGS - Only show for raid command
                if (selectedCommand.commandText.ToLower() == "raid")
                {
                    y += 10f; // Extra spacing

                    Rect raidHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(raidHeaderRect, "CAP.CommandManager.RaidSettings".Translate());
                    y += sectionHeight;

                    // Default wager
                    Rect wagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(wagerRect, "CAP.CommandManager.DefaultWager".Translate());
                    Rect wagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string wagerBuffer = settings.DefaultRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(wagerInputRect, ref settings.DefaultRaidWager, ref wagerBuffer,
                        settings.MinRaidWager, settings.MaxRaidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "CAP.CommandManager.MinWager".Translate());
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(minWagerInputRect, ref settings.MinRaidWager, ref minWagerBuffer, 100, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "CAP.CommandManager.MaxWager".Translate());
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerBuffer = settings.MaxRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(maxWagerInputRect, ref settings.MaxRaidWager, ref maxWagerBuffer,
                        settings.MinRaidWager, 50000);
                    y += sectionHeight;

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

                // MILITARY AID-SPECIFIC SETTINGS - Only show for militaryaid command
                if (selectedCommand.commandText.ToLower() == "militaryaid")
                {
                    y += 10f; // Extra spacing

                    Rect militaryHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(militaryHeaderRect, "CAP.CommandManager.MilitaryAidSettings".Translate().Colorize(ColorLibrary.SubHeader));
                    y += sectionHeight;

                    // Default wager
                    Rect wagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(wagerRect, "CAP.CommandManager.MilitaryAidDefaultwager".Translate());
                    Rect wagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string wagerBuffer = settings.DefaultMilitaryAidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(wagerInputRect, ref settings.DefaultMilitaryAidWager, ref wagerBuffer,
                        settings.MinMilitaryAidWager, settings.MaxMilitaryAidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "CAP.CommandManager.MilitaryAidMinWager".Translate());
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinMilitaryAidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(minWagerInputRect, ref settings.MinMilitaryAidWager, ref minWagerBuffer, 500, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "CAP.CommandManager.MilitaryAidMaxWager".Translate());
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerBuffer = settings.MaxMilitaryAidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(maxWagerInputRect, ref settings.MaxMilitaryAidWager, ref maxWagerBuffer,
                        settings.MinMilitaryAidWager, 20000);
                    y += sectionHeight;
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

                // PASSION-SPECIFIC SETTINGS - Only show for passion command
                if (selectedCommand != null && selectedCommand.commandText.ToLower() == "passion")
                {
                    y += 10f; // Extra spacing

                    Rect passionHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(passionHeaderRect, "CAP.CommandManager.PassionSettings".Translate().Colorize(ColorLibrary.SubHeader));
                    y += sectionHeight;

                    // ── Wager Settings ───────────────────────────────────────────────────────
                    Rect wagerSubHeader = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(wagerSubHeader, "CAP.CommandManager.PassionWagerSettings".Translate());
                    y += sectionHeight;

                    // Min wager
                    Rect minWagerLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerLabelRect, "CAP.CommandManager.PassionMinWager".Translate());
                    TooltipHandler.TipRegion(minWagerLabelRect, "CAP.CommandManager.PassionMinWagerTip".Translate());
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerKey = "passion_minwager";
                    if (!numericBuffers.ContainsKey(minWagerKey))
                        numericBuffers[minWagerKey] = settingsGlobalChat.MinPassionWager.ToString();
                    string minWagerBuffer = numericBuffers[minWagerKey];
                    Widgets.TextFieldNumeric(minWagerInputRect, ref settingsGlobalChat.MinPassionWager, ref minWagerBuffer, 1, 10000);
                    numericBuffers[minWagerKey] = minWagerBuffer;
                    y += sectionHeight;

                    // Max wager
                    Rect maxWagerLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerLabelRect, "CAP.CommandManager.PassionMaxWager".Translate());
                    TooltipHandler.TipRegion(maxWagerLabelRect, "CAP.CommandManager.PassionMaxWagerTip".Translate());
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerKey = "passion_maxwager";
                    if (!numericBuffers.ContainsKey(maxWagerKey))
                        numericBuffers[maxWagerKey] = settingsGlobalChat.MaxPassionWager.ToString();
                    string maxWagerBuffer = numericBuffers[maxWagerKey];
                    Widgets.TextFieldNumeric(maxWagerInputRect, ref settingsGlobalChat.MaxPassionWager, ref maxWagerBuffer,
                        settingsGlobalChat.MinPassionWager, 100000);
                    numericBuffers[maxWagerKey] = maxWagerBuffer;
                    y += sectionHeight;

                    // Bonus per 100 coins
                    Rect bonusPer100LabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(bonusPer100LabelRect, "CAP.CommandManager.PassionWagerBonusPer100".Translate());
                    TooltipHandler.TipRegion(bonusPer100LabelRect, "CAP.CommandManager.PassionWagerBonusPer100Tip".Translate());
                    Rect bonusPer100InputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string bonusPer100Key = "passion_wagerbonusper100";
                    if (!numericBuffers.ContainsKey(bonusPer100Key))
                        numericBuffers[bonusPer100Key] = settingsGlobalChat.PassionWagerBonusPer100.ToString("F1");
                    string bonusPer100Buffer = numericBuffers[bonusPer100Key];
                    Widgets.TextFieldNumeric(bonusPer100InputRect, ref settingsGlobalChat.PassionWagerBonusPer100, ref bonusPer100Buffer, 0f, 10f);
                    numericBuffers[bonusPer100Key] = bonusPer100Buffer;
                    y += sectionHeight;

                    // Max wager bonus
                    Rect maxBonusLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxBonusLabelRect, "CAP.CommandManager.PassionMaxWagerBonus".Translate());
                    TooltipHandler.TipRegion(maxBonusLabelRect, "CAP.CommandManager.PassionMaxWagerBonusTip".Translate());
                    Rect maxBonusInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxBonusKey = "passion_maxwagerbonus";
                    if (!numericBuffers.ContainsKey(maxBonusKey))
                        numericBuffers[maxBonusKey] = settingsGlobalChat.MaxPassionWagerBonus.ToString("F1");
                    string maxBonusBuffer = numericBuffers[maxBonusKey];
                    Widgets.TextFieldNumeric(maxBonusInputRect, ref settingsGlobalChat.MaxPassionWagerBonus, ref maxBonusBuffer, 0f, 100f);
                    numericBuffers[maxBonusKey] = maxBonusBuffer;
                    y += sectionHeight;

                    // ── Success Chance Scaling ───────────────────────────────────────────────
                    Rect successSubHeader = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(successSubHeader, "CAP.CommandManager.PassionSuccessScaling".Translate());
                    y += sectionHeight;

                    // Base success chance
                    Rect baseChanceLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(baseChanceLabelRect, "CAP.CommandManager.PassionBaseSuccess".Translate());
                    TooltipHandler.TipRegion(baseChanceLabelRect, "CAP.CommandManager.PassionBaseSuccessTip".Translate());
                    Rect baseChanceInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string baseChanceKey = "passion_basechance";
                    if (!numericBuffers.ContainsKey(baseChanceKey))
                        numericBuffers[baseChanceKey] = settingsGlobalChat.BasePassionSuccessChance.ToString("F1");
                    string baseChanceBuffer = numericBuffers[baseChanceKey];
                    Widgets.TextFieldNumeric(baseChanceInputRect, ref settingsGlobalChat.BasePassionSuccessChance, ref baseChanceBuffer, 1f, 100f);
                    numericBuffers[baseChanceKey] = baseChanceBuffer;
                    y += sectionHeight;

                    // Max success chance
                    Rect maxChanceLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxChanceLabelRect, "CAP.CommandManager.PassionMaxSuccess".Translate());
                    TooltipHandler.TipRegion(maxChanceLabelRect, "CAP.CommandManager.PassionMaxSuccessTip".Translate());
                    Rect maxChanceInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxChanceKey = "passion_maxchance";
                    if (!numericBuffers.ContainsKey(maxChanceKey))
                        numericBuffers[maxChanceKey] = settingsGlobalChat.MaxPassionSuccessChance.ToString("F1");
                    string maxChanceBuffer = numericBuffers[maxChanceKey];
                    Widgets.TextFieldNumeric(maxChanceInputRect, ref settingsGlobalChat.MaxPassionSuccessChance, ref maxChanceBuffer,
                        settingsGlobalChat.BasePassionSuccessChance, 100f);
                    numericBuffers[maxChanceKey] = maxChanceBuffer;
                    y += sectionHeight;

                    // ── Critical Success ─────────────────────────────────────────────────────
                    Rect critSuccessSubHeader = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(critSuccessSubHeader, "CAP.CommandManager.PassionCritSuccess".Translate());
                    y += sectionHeight;

                    // Critical success ratio
                    Rect critRatioLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(critRatioLabelRect, "CAP.CommandManager.PassionCritSuccessRatio".Translate());
                    TooltipHandler.TipRegion(critRatioLabelRect, "CAP.CommandManager.PassionCritSuccessRatioTip".Translate());
                    Rect critRatioInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string critRatioKey = "passion_critsuccessratio";
                    if (!numericBuffers.ContainsKey(critRatioKey))
                        numericBuffers[critRatioKey] = settingsGlobalChat.CriticalSuccessRatio.ToString("F2");
                    string critRatioBuffer = numericBuffers[critRatioKey];
                    Widgets.TextFieldNumeric(critRatioInputRect, ref settingsGlobalChat.CriticalSuccessRatio, ref critRatioBuffer, 0f, 2f);
                    numericBuffers[critRatioKey] = critRatioBuffer;
                    y += sectionHeight;

                    // Max critical success chance
                    Rect maxCritLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxCritLabelRect, "CAP.CommandManager.PassionMaxCritSuccess".Translate());
                    TooltipHandler.TipRegion(maxCritLabelRect, "CAP.CommandManager.PassionMaxCritSuccessTip".Translate());
                    Rect maxCritInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxCritKey = "passion_maxcritsuccess";
                    if (!numericBuffers.ContainsKey(maxCritKey))
                        numericBuffers[maxCritKey] = settingsGlobalChat.MaxCriticalSuccessChance.ToString("F1");
                    string maxCritBuffer = numericBuffers[maxCritKey];
                    Widgets.TextFieldNumeric(maxCritInputRect, ref settingsGlobalChat.MaxCriticalSuccessChance, ref maxCritBuffer, 0f, 20f);
                    numericBuffers[maxCritKey] = maxCritBuffer;
                    y += sectionHeight;

                    // ── Critical Failure ─────────────────────────────────────────────────────
                    Rect critFailSubHeader = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(critFailSubHeader, "CAP.CommandManager.PassionCritFailure".Translate());
                    y += sectionHeight;

                    // Base crit fail chance
                    Rect baseFailLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(baseFailLabelRect, "CAP.CommandManager.PassionCritFailBase".Translate());
                    TooltipHandler.TipRegion(baseFailLabelRect, "CAP.CommandManager.PassionCritFailBaseTip".Translate());
                    Rect baseFailInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string baseFailKey = "passion_critfailbase";
                    if (!numericBuffers.ContainsKey(baseFailKey))
                        numericBuffers[baseFailKey] = settingsGlobalChat.CriticalFailBaseChance.ToString("F1");
                    string baseFailBuffer = numericBuffers[baseFailKey];
                    Widgets.TextFieldNumeric(baseFailInputRect, ref settingsGlobalChat.CriticalFailBaseChance, ref baseFailBuffer, 0f, 50f);
                    numericBuffers[baseFailKey] = baseFailBuffer;
                    y += sectionHeight;

                    // Reduction factor
                    Rect reductionLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(reductionLabelRect, "CAP.CommandManager.PassionCritFailReduction".Translate());
                    TooltipHandler.TipRegion(reductionLabelRect, "CAP.CommandManager.PassionCritFailReductionTip".Translate());
                    Rect reductionInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string reductionKey = "passion_critfailreduction";
                    if (!numericBuffers.ContainsKey(reductionKey))
                        numericBuffers[reductionKey] = settingsGlobalChat.CriticalFailReductionFactor.ToString("F2");
                    string reductionBuffer = numericBuffers[reductionKey];
                    Widgets.TextFieldNumeric(reductionInputRect, ref settingsGlobalChat.CriticalFailReductionFactor, ref reductionBuffer, 0f, 1f);
                    numericBuffers[reductionKey] = reductionBuffer;
                    y += sectionHeight;

                    // Min crit fail chance
                    Rect minFailLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minFailLabelRect, "CAP.CommandManager.PassionCritFailMin".Translate());
                    TooltipHandler.TipRegion(minFailLabelRect, "CAP.CommandManager.PassionCritFailMinTip".Translate());
                    Rect minFailInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minFailKey = "passion_critfailmin";
                    if (!numericBuffers.ContainsKey(minFailKey))
                        numericBuffers[minFailKey] = settingsGlobalChat.MinCriticalFailChance.ToString("F1");
                    string minFailBuffer = numericBuffers[minFailKey];
                    Widgets.TextFieldNumeric(minFailInputRect, ref settingsGlobalChat.MinCriticalFailChance, ref minFailBuffer, 0f, 20f);
                    numericBuffers[minFailKey] = minFailBuffer;
                    y += sectionHeight;

                    // ── Outcome Biases (Advanced) ────────────────────────────────────────────
                    Rect biasSubHeader = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                    Widgets.Label(biasSubHeader, "CAP.CommandManager.PassionOutcomeBiases".Translate());
                    y += sectionHeight;

                    // Crit success upgrade vs new
                    Rect upgradeLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(upgradeLabelRect, "CAP.CommandManager.PassionCritSuccessUpgradeChance".Translate());
                    TooltipHandler.TipRegion(upgradeLabelRect, "CAP.CommandManager.PassionCritSuccessUpgradeChanceTip".Translate());
                    Rect upgradeInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string upgradeKey = "passion_critupgrade";
                    if (!numericBuffers.ContainsKey(upgradeKey))
                        numericBuffers[upgradeKey] = settingsGlobalChat.CritSuccessUpgradeVsNewChance.ToString("F2");
                    string upgradeBuffer = numericBuffers[upgradeKey];
                    Widgets.TextFieldNumeric(upgradeInputRect, ref settingsGlobalChat.CritSuccessUpgradeVsNewChance, ref upgradeBuffer, 0f, 1f);
                    numericBuffers[upgradeKey] = upgradeBuffer;
                    y += sectionHeight;

                    // Crit fail lose vs wrong
                    Rect loseLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(loseLabelRect, "CAP.CommandManager.PassionCritFailLoseChance".Translate());
                    TooltipHandler.TipRegion(loseLabelRect, "CAP.CommandManager.PassionCritFailLoseChanceTip".Translate());
                    Rect loseInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string loseKey = "passion_critlose";
                    if (!numericBuffers.ContainsKey(loseKey))
                        numericBuffers[loseKey] = settingsGlobalChat.CritFailLoseVsWrongChance.ToString("F2");
                    string loseBuffer = numericBuffers[loseKey];
                    Widgets.TextFieldNumeric(loseInputRect, ref settingsGlobalChat.CritFailLoseVsWrongChance, ref loseBuffer, 0f, 1f);
                    numericBuffers[loseKey] = loseBuffer;
                    y += sectionHeight;

                    // Targeted crit fail affect target
                    Rect targetedLabelRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(targetedLabelRect, "CAP.CommandManager.PassionTargetedCritFailChance".Translate());
                    TooltipHandler.TipRegion(targetedLabelRect, "CAP.CommandManager.PassionTargetedCritFailChanceTip".Translate());
                    Rect targetedInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string targetedKey = "passion_targetedcritfail";
                    if (!numericBuffers.ContainsKey(targetedKey))
                        numericBuffers[targetedKey] = settingsGlobalChat.TargetedCritFailAffectTargetChance.ToString("F2");
                    string targetedBuffer = numericBuffers[targetedKey];
                    Widgets.TextFieldNumeric(targetedInputRect, ref settingsGlobalChat.TargetedCritFailAffectTargetChance, ref targetedBuffer, 0f, 1f);
                    numericBuffers[targetedKey] = targetedBuffer;
                    y += sectionHeight;

                    // Overall description (kept from original)
                    Rect passionDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, 28f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(passionDescRect, "CAP.CommandManager.PassionDescription".Translate());
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += 28f;
                }

                // SURGERY-SPECIFIC SETTINGS - Only show for surgery command
                if (selectedCommand != null && selectedCommand.commandText.ToLower() == "surgery")
                {
                    y += 18f; // Extra spacing

                    Rect surgeryHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Text.Font = GameFont.Medium;
                    GUI.color = ColorLibrary.HeaderAccent;
                    Widgets.Label(surgeryHeaderRect, "CAP.CommandManager.SurgerySettings".Translate().Colorize(ColorLibrary.SubHeader));
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += sectionHeight;

                    // ── Gender Swap ────────────────────────────────────────────────────────────────
                    float rowY = y;
                    // Checkbox (left)
                    Rect checkRect = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRect.x, checkRect.y, ref globalSettings.SurgeryAllowGenderSwap);  // bare checkbox
                    TooltipHandler.TipRegion(checkRect, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect genderCostLabelRect = new Rect(checkRect.xMax + gapAfterCheck, y, labelWidth, sectionHeight);
                    Widgets.Label(genderCostLabelRect, "CAP.CommandManager.SurgeryGenderSwapCost".Translate());
                    Rect genderCostInputRect = new Rect(viewRect.width - 90f, rowY, 80f, sectionHeight);

                    // Numeric input (right)
                    Rect genderInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string genderCostKey = "surgery_gender_swap_cost";
                    if (!numericBuffers.ContainsKey(genderCostKey))
                        numericBuffers[genderCostKey] = globalSettings.SurgeryGenderSwapCost.ToString();
                    string genderCostBuffer = numericBuffers[genderCostKey];
                    Widgets.TextFieldNumeric(genderInputRect, ref globalSettings.SurgeryGenderSwapCost, ref genderCostBuffer, -100000f, 100000f);
                    numericBuffers[genderCostKey] = genderCostBuffer;

                    y += sectionHeight;

                    // ── Body Change ────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectBody = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectBody.x, checkRectBody.y, ref globalSettings.SurgeryAllowBodyChange);
                    TooltipHandler.TipRegion(checkRectBody, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect bodyCostLabelRect = new Rect(checkRectBody.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(bodyCostLabelRect, "CAP.CommandManager.SurgeryBodyChangeCost".Translate());

                    Rect bodyCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string bodyCostKey = "surgery_body_change_cost";
                    if (!numericBuffers.ContainsKey(bodyCostKey))
                        numericBuffers[bodyCostKey] = globalSettings.SurgeryBodyChangeCost.ToString();
                    string bodyCostBuffer = numericBuffers[bodyCostKey];
                    Widgets.TextFieldNumeric(bodyCostInputRect, ref globalSettings.SurgeryBodyChangeCost, ref bodyCostBuffer, -100000f, 100000f);
                    numericBuffers[bodyCostKey] = bodyCostBuffer;

                    y += sectionHeight;

                    // ── Sterilize ──────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectSterilize = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectSterilize.x, checkRectSterilize.y, ref globalSettings.SurgeryAllowSterilize);
                    TooltipHandler.TipRegion(checkRectSterilize, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect sterilizeCostLabelRect = new Rect(checkRectSterilize.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(sterilizeCostLabelRect, "CAP.CommandManager.SurgerySterilizeCost".Translate());

                    Rect sterilizeCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string sterilizeCostKey = "surgery_sterilize_cost";
                    if (!numericBuffers.ContainsKey(sterilizeCostKey))
                        numericBuffers[sterilizeCostKey] = globalSettings.SurgerySterilizeCost.ToString();
                    string sterilizeCostBuffer = numericBuffers[sterilizeCostKey];
                    Widgets.TextFieldNumeric(sterilizeCostInputRect, ref globalSettings.SurgerySterilizeCost, ref sterilizeCostBuffer, -100000f, 100000f);
                    numericBuffers[sterilizeCostKey] = sterilizeCostBuffer;

                    y += sectionHeight;

                    // ── IUD ────────────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectIUD = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectIUD.x, checkRectIUD.y, ref globalSettings.SurgeryAllowIUD);
                    TooltipHandler.TipRegion(checkRectIUD, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect iudCostLabelRect = new Rect(checkRectIUD.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(iudCostLabelRect, "CAP.CommandManager.SurgeryIUDCost".Translate());

                    Rect iudCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string iudCostKey = "surgery_iud_cost";
                    if (!numericBuffers.ContainsKey(iudCostKey))
                        numericBuffers[iudCostKey] = globalSettings.SurgeryIUDCost.ToString();
                    string iudCostBuffer = numericBuffers[iudCostKey];
                    Widgets.TextFieldNumeric(iudCostInputRect, ref globalSettings.SurgeryIUDCost, ref iudCostBuffer, -100000f, 100000f);
                    numericBuffers[iudCostKey] = iudCostBuffer;

                    y += sectionHeight;

                    // ── Vas Reverse ────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectVas = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectVas.x, checkRectVas.y, ref globalSettings.SurgeryAllowVasReverse);
                    TooltipHandler.TipRegion(checkRectVas, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect vasReverseCostLabelRect = new Rect(checkRectVas.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(vasReverseCostLabelRect, "CAP.CommandManager.SurgeryVasReverseCost".Translate());

                    Rect vasReverseCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string vasReverseCostKey = "surgery_vas_reverse_cost";
                    if (!numericBuffers.ContainsKey(vasReverseCostKey))
                        numericBuffers[vasReverseCostKey] = globalSettings.SurgeryVasReverseCost.ToString();
                    string vasReverseCostBuffer = numericBuffers[vasReverseCostKey];
                    Widgets.TextFieldNumeric(vasReverseCostInputRect, ref globalSettings.SurgeryVasReverseCost, ref vasReverseCostBuffer, -100000f, 100000f);
                    numericBuffers[vasReverseCostKey] = vasReverseCostBuffer;

                    y += sectionHeight;

                    // ── Terminate ──────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectTerminate = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectTerminate.x, checkRectTerminate.y, ref globalSettings.SurgeryAllowTerminate);
                    TooltipHandler.TipRegion(checkRectTerminate, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect terminateCostLabelRect = new Rect(checkRectTerminate.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(terminateCostLabelRect, "CAP.CommandManager.SurgeryTerminateCost".Translate());

                    Rect terminateCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string terminateCostKey = "surgery_terminate_cost";
                    if (!numericBuffers.ContainsKey(terminateCostKey))
                        numericBuffers[terminateCostKey] = globalSettings.SurgeryTerminateCost.ToString();
                    string terminateCostBuffer = numericBuffers[terminateCostKey];
                    Widgets.TextFieldNumeric(terminateCostInputRect, ref globalSettings.SurgeryTerminateCost, ref terminateCostBuffer, -100000f, 100000f);
                    numericBuffers[terminateCostKey] = terminateCostBuffer;

                    y += sectionHeight;

                    // ── Hemogen ────────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectHemogen = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectHemogen.x, checkRectHemogen.y, ref globalSettings.SurgeryAllowHemogen);
                    TooltipHandler.TipRegion(checkRectHemogen, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect hemogenCostLabelRect = new Rect(checkRectHemogen.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(hemogenCostLabelRect, "CAP.CommandManager.SurgeryHemogenCost".Translate());

                    Rect hemogenCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string hemogenCostKey = "surgery_hemogen_cost";
                    if (!numericBuffers.ContainsKey(hemogenCostKey))
                        numericBuffers[hemogenCostKey] = globalSettings.SurgeryHemogenCost.ToString();
                    string hemogenCostBuffer = numericBuffers[hemogenCostKey];
                    Widgets.TextFieldNumeric(hemogenCostInputRect, ref globalSettings.SurgeryHemogenCost, ref hemogenCostBuffer, -100000f, 100000f);
                    numericBuffers[hemogenCostKey] = hemogenCostBuffer;

                    y += sectionHeight;

                    // ── Transfusion ────────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectTransfusion = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectTransfusion.x, checkRectTransfusion.y, ref globalSettings.SurgeryAllowTransfusion);
                    TooltipHandler.TipRegion(checkRectTransfusion, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect transfusionCostLabelRect = new Rect(checkRectTransfusion.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(transfusionCostLabelRect, "CAP.CommandManager.SurgeryTransfusionCost".Translate());

                    Rect transfusionCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string transfusionCostKey = "surgery_transfusion_cost";
                    if (!numericBuffers.ContainsKey(transfusionCostKey))
                        numericBuffers[transfusionCostKey] = globalSettings.SurgeryTransfusionCost.ToString();
                    string transfusionCostBuffer = numericBuffers[transfusionCostKey];
                    Widgets.TextFieldNumeric(transfusionCostInputRect, ref globalSettings.SurgeryTransfusionCost, ref transfusionCostBuffer, -100000f, 100000f);
                    numericBuffers[transfusionCostKey] = transfusionCostBuffer;

                    y += sectionHeight;

                    // ── Misc Biotech ───────────────────────────────────────────────────────────────
                    rowY = y;

                    Rect checkRectMisc = new Rect(leftPadding + 10f, rowY, checkBoxWidth, sectionHeight);
                    Widgets.Checkbox(checkRectMisc.x, checkRectMisc.y, ref globalSettings.SurgeryAllowMiscBiotech);
                    TooltipHandler.TipRegion(checkRectMisc, "CAP.CommandManager.SurgeryTooltip".Translate());

                    Rect miscCostLabelRect = new Rect(checkRectMisc.xMax + gapAfterCheck, rowY, labelWidth, sectionHeight);
                    Widgets.Label(miscCostLabelRect, "CAP.CommandManager.SurgeryMiscBiotechCost".Translate());

                    Rect miscCostInputRect = new Rect(viewRect.width - inputWidth - 10f, rowY, inputWidth, sectionHeight);
                    string miscCostKey = "surgery_misc_biotech_cost";
                    if (!numericBuffers.ContainsKey(miscCostKey))
                        numericBuffers[miscCostKey] = globalSettings.SurgeryMiscBiotechCost.ToString();
                    string miscCostBuffer = numericBuffers[miscCostKey];
                    Widgets.TextFieldNumeric(miscCostInputRect, ref globalSettings.SurgeryMiscBiotechCost, ref miscCostBuffer, -100000f, 100000f);
                    numericBuffers[miscCostKey] = miscCostBuffer;

                    y += sectionHeight + 4f;

                    // Optional description
                    Rect surgeryDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, 28f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(surgeryDescRect, "CAP.CommandManager.SurgeryDescription".Translate());
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += 20f;
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
            height += 14f; // Alias description

            // Command Cooldown Settings (replaces the old game days cooldown)
            height += 28f; // Command Cooldown Settings header
            height += 28f; // Turn on for non event Commands toggle
            height += 28f; // Max uses per cooldown period

            // RAID-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "raid")
            {
                height += 10f; // Extra spacing
                height += 28f; // Raid header
                height += 28f; // Default wager
                height += 28f; // Min wager
                height += 28f; // Max wager
                height += 28f; // Raid types button
                height += 28f; // Strategies button
            }

            // MILITARY AID-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "militaryaid")
            {
                height += 10f; // Extra spacing
                height += 28f; // Military aid header
                height += 28f; // Default wager
                height += 28f; // Min wager
                height += 28f; // Max wager
            }

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

            // PASSION-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "passion")
            {
                height += 10f;  // Extra spacing
                height += 28f;  // Main Passion header
                height += 28f;  // Wager sub-header
                height += 28f * 4; // 4 wager fields
                height += 28f;  // Success sub-header
                height += 28f * 2; // 2 success fields
                height += 28f;  // Critical Success sub-header
                height += 28f * 2; // 2 crit success fields
                height += 28f;  // Critical Failure sub-header
                height += 28f * 3; // 3 crit failure fields
                height += 28f;  // Outcome Biases sub-header
                height += 28f * 3; // 3 bias fields
                height += 28f;  // Overall description
                height += 28f;  // A little extra to prevent cut off.
            }

            // SURGERY-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "surgery")
            {
                height += 10f; // Extra spacing
                height += 28f; // Surgery header
                height += 28f; // Gender Swap Cost
                height += 28f; // Body Change Cost
                height += 28f; // Sterilize Cost
                height += 28f; // IUD Cost
                height += 28f; // Vas Reverse Cost
                height += 28f; // Terminate Cost
                height += 28f; // Hemogen Cost
                height += 28f; // Transfusion Cost
                height += 28f; // Misc Biotech Cost
                height += 28f; // Description (taller than normal)
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