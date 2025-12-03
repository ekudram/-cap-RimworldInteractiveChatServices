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
                        commandSettings[commandKey] = new CommandSettings();
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
            GUI.color = ColorLibrary.Orange;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            Widgets.Label(titleRect, "Command Management");

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
                if (Widgets.ButtonText(new Rect(rect.width - 80f, 10f, 75f, 24f), "Settings"))
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
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Name"))
            {
                if (sortMethod == CommandSortMethod.Name)
                    sortAscending = !sortAscending;
                else
                    sortMethod = CommandSortMethod.Name;
                SortCommands();
            }
            x += buttonWidth + spacing;

            // Sort by Category
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Category"))
            {
                if (sortMethod == CommandSortMethod.Category)
                    sortAscending = !sortAscending;
                else
                    sortMethod = CommandSortMethod.Category;
                SortCommands();
            }
            x += buttonWidth + spacing;

            // Sort by Status
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "Status"))
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
            Widgets.Label(headerRect, "Commands");
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
                        displayName += " [DISABLED]";

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
                Widgets.Label(messageRect, "Select a command to see details");
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
                headerText += " 🚫 DISABLED";

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
                Widgets.Label(basicLabelRect, "Basic Information:");
                y += sectionHeight;

                // Command text
                Rect commandTextRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                Widgets.Label(commandTextRect, $"Trigger: !{selectedCommand.commandText}");
                y += sectionHeight;

                // Description
                Rect descRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight * 2);
                string desc = string.IsNullOrEmpty(selectedCommand.commandDescription) ?
                    "No description available" : selectedCommand.commandDescription;
                Widgets.Label(descRect, $"Description: {desc}");
                y += sectionHeight * 2;

                // Permission level
                Rect permRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, sectionHeight);
                Widgets.Label(permRect, $"Permission Level: {selectedCommand.permissionLevel}");
                y += sectionHeight;

                y += 10f;

                // Basic Settings section
                Rect settingsLabelRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(settingsLabelRect, "Basic Settings:");
                y += sectionHeight;

                // Enabled toggle
                Rect enabledRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(enabledRect, "Enabled:");
                Rect enabledToggleRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                if (Widgets.ButtonText(enabledToggleRect, settings.Enabled ? "ON" : "OFF"))
                {
                    settings.Enabled = !settings.Enabled;
                }
                y += sectionHeight;

                // Cooldown setting - FIXED and simplified
                Rect cooldownLabelRect = new Rect(leftPadding + 10f, y, 250f, sectionHeight);
                Widgets.Label(cooldownLabelRect, "Command Cooldown (seconds):");

                Rect cooldownInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                // Create a unique key for this setting
                string cooldownKey = $"cooldown_{settings.GetHashCode()}";

                // Initialize buffer if needed
                if (!numericBuffers.ContainsKey(cooldownKey))
                {
                    numericBuffers[cooldownKey] = settings.CooldownSeconds.ToString();
                }

                string cooldownBuffer = numericBuffers[cooldownKey];
                Widgets.TextFieldNumeric(cooldownInputRect, ref settings.CooldownSeconds, ref cooldownBuffer, 1, 60);
                numericBuffers[cooldownKey] = cooldownBuffer;

                // Description below
                Rect cooldownDescRect = new Rect(leftPadding + 10f, y + sectionHeight - 8f, viewRect.width - leftPadding, 14f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f); // Softer gray
                Widgets.Label(cooldownDescRect, "Prevents command spamming - reminds users to be respectful");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                y += sectionHeight + 10f; // Extra space for description

                y += sectionHeight + 8f; // Extra space for description

                // Cost setting (if applicable)
                if (settings.SupportsCost)
                {
                    Rect costRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(costRect, $"Cost ({settingsGlobalChat.CurrencyName}):");
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
                Widgets.Label(advancedLabelRect, "Advanced Settings:");
                y += sectionHeight;

                // Command alias - NEW: Allow custom command aliases
                Rect aliasLabelRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(aliasLabelRect, "Command Alias:");
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
                Widgets.Label(aliasDescRect, $"Example: 'bp' for !backpack. Don't include '{globalSettings.Prefix}' or '{globalSettings.BuyPrefix}' prefix.");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 20f;

                // Event Command Settings - Only show for commands like raid, militaryaid
                Rect eventHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                Widgets.Label(eventHeaderRect, "Command Cooldown Settings:");
                y += sectionHeight;

                // Use event cooldown toggle
                Rect eventToggleRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.CheckboxLabeled(eventToggleRect, "Turn on for non event Commands", ref settings.useCommandCooldown);
                y += sectionHeight;

                // Max uses per cooldown period
                Rect eventUsesRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.Label(eventUsesRect, "Max uses per cooldown period 0 = infinite use of command:");
                Rect eventUsesInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                string eventUsesBuffer = settings.MaxUsesPerCooldownPeriod.ToString();
                UIUtilities.TextFieldNumericFlexible(eventUsesInputRect, ref settings.MaxUsesPerCooldownPeriod, ref eventUsesBuffer, 0, 10000);
                y += sectionHeight;


                // RAID-SPECIFIC SETTINGS - Only show for raid command
                if (selectedCommand.commandText.ToLower() == "raid")
                {
                    y += 10f; // Extra spacing

                    Rect raidHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(raidHeaderRect, "Raid Command Settings:");
                    y += sectionHeight;

                    // Default wager
                    Rect wagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(wagerRect, "Default wager:");
                    Rect wagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string wagerBuffer = settings.DefaultRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(wagerInputRect, ref settings.DefaultRaidWager, ref wagerBuffer,
                        settings.MinRaidWager, settings.MaxRaidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "Min wager:");
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(minWagerInputRect, ref settings.MinRaidWager, ref minWagerBuffer, 100, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "Max wager:");
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerBuffer = settings.MaxRaidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(maxWagerInputRect, ref settings.MaxRaidWager, ref maxWagerBuffer,
                        settings.MinRaidWager, 50000);
                    y += sectionHeight;

                    // Allowed raid types button
                    Rect raidTypesRect = new Rect(leftPadding + 10f, y, 200f, sectionHeight);
                    if (Widgets.ButtonText(raidTypesRect, "Configure Raid Types →"))
                    {
                        Find.WindowStack.Add(new Dialog_RaidTypesEditor(settings));
                    }
                    y += sectionHeight;

                    // Allowed strategies button  
                    Rect strategiesRect = new Rect(leftPadding + 10f, y, 200f, sectionHeight);
                    if (Widgets.ButtonText(strategiesRect, "Configure Strategies →"))
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
                    Widgets.Label(militaryHeaderRect, "Military Aid Command Settings:");
                    y += sectionHeight;

                    // Default wager
                    Rect wagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(wagerRect, "Default wager:");
                    Rect wagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string wagerBuffer = settings.DefaultMilitaryAidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(wagerInputRect, ref settings.DefaultMilitaryAidWager, ref wagerBuffer,
                        settings.MinMilitaryAidWager, settings.MaxMilitaryAidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "Min wager:");
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinMilitaryAidWager.ToString();
                    UIUtilities.TextFieldNumericFlexible(minWagerInputRect, ref settings.MinMilitaryAidWager, ref minWagerBuffer, 500, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "Max wager:");
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
                    Widgets.Label(lootboxHeaderRect, "Loot Box Global Settings:");
                    y += sectionHeight;

                    // Coin range
                    Rect coinRangeRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(coinRangeRect, "Coin Range (1-10000):");
                    y += sectionHeight;

                    // Min coin input
                    Rect coinMinRect = new Rect(leftPadding + 20f, y, 80f, sectionHeight);
                    Widgets.Label(coinMinRect, "Min:");
                    Rect coinMinInputRect = new Rect(leftPadding + 60f, y, 60f, sectionHeight);

                    // Use buffer pattern for min coin
                    string coinMinKey = "lootbox_mincoin";
                    if (!numericBuffers.ContainsKey(coinMinKey))
                    {
                        numericBuffers[coinMinKey] = settingsGlobalChat.LootBoxRandomCoinRange.min.ToString();
                    }
                    string coinMinBuffer = numericBuffers[coinMinKey];
                    Widgets.TextFieldNumeric(coinMinInputRect, ref settingsGlobalChat.LootBoxRandomCoinRange.min, ref coinMinBuffer, 1, 10000);
                    numericBuffers[coinMinKey] = coinMinBuffer;

                    // Max coin input
                    Rect coinMaxRect = new Rect(leftPadding + 140f, y, 80f, sectionHeight);
                    Widgets.Label(coinMaxRect, "Max:");
                    Rect coinMaxInputRect = new Rect(leftPadding + 180f, y, 60f, sectionHeight);

                    // Use buffer pattern for max coin
                    string coinMaxKey = "lootbox_maxcoin";
                    if (!numericBuffers.ContainsKey(coinMaxKey))
                    {
                        numericBuffers[coinMaxKey] = settingsGlobalChat.LootBoxRandomCoinRange.max.ToString();
                    }
                    string coinMaxBuffer = numericBuffers[coinMaxKey];
                    Widgets.TextFieldNumeric(coinMaxInputRect, ref settingsGlobalChat.LootBoxRandomCoinRange.max, ref coinMaxBuffer, 1, 10000);
                    numericBuffers[coinMaxKey] = coinMaxBuffer;

                    y += sectionHeight;

                    // Lootboxes per day
                    Rect perDayRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(perDayRect, "Lootboxes Per Day:");
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
                    Widgets.CheckboxLabeled(welcomeRect, "Show Welcome Message", ref settingsGlobalChat.LootBoxShowWelcomeMessage);
                    y += sectionHeight;

                    // Force open all at once
                    Rect forceOpenRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.CheckboxLabeled(forceOpenRect, "Force Open All At Once", ref settingsGlobalChat.LootBoxForceOpenAllAtOnce);
                    y += sectionHeight;
                }

                // PASSION-SPECIFIC SETTINGS - Only show for passion command
                if (selectedCommand != null && selectedCommand.commandText.ToLower() == "passion")
                {
                    y += 10f; // Extra spacing

                    Rect passionHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(passionHeaderRect, "Passion Command Settings:");
                    y += sectionHeight;

                    // Min wager
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "Minimum wager:");
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    string minWagerKey = "passion_minwager";
                    if (!numericBuffers.ContainsKey(minWagerKey))
                    {
                        numericBuffers[minWagerKey] = settingsGlobalChat.MinPassionWager.ToString();
                    }
                    string minWagerBuffer = numericBuffers[minWagerKey];
                    Widgets.TextFieldNumeric(minWagerInputRect, ref settingsGlobalChat.MinPassionWager, ref minWagerBuffer, 1, 10000);
                    numericBuffers[minWagerKey] = minWagerBuffer;

                    y += sectionHeight;

                    // Max wager
                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "Maximum wager:");
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    string maxWagerKey = "passion_maxwager";
                    if (!numericBuffers.ContainsKey(maxWagerKey))
                    {
                        numericBuffers[maxWagerKey] = settingsGlobalChat.MaxPassionWager.ToString();
                    }
                    string maxWagerBuffer = numericBuffers[maxWagerKey];
                    Widgets.TextFieldNumeric(maxWagerInputRect, ref settingsGlobalChat.MaxPassionWager, ref maxWagerBuffer,
                        settingsGlobalChat.MinPassionWager, 100000);
                    numericBuffers[maxWagerKey] = maxWagerBuffer;

                    y += sectionHeight;

                    // Base success chance
                    Rect baseChanceRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(baseChanceRect, "Base success chance (%):");
                    Rect baseChanceInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    string baseChanceKey = "passion_basechance";
                    if (!numericBuffers.ContainsKey(baseChanceKey))
                    {
                        numericBuffers[baseChanceKey] = settingsGlobalChat.BasePassionSuccessChance.ToString();
                    }
                    string baseChanceBuffer = numericBuffers[baseChanceKey];
                    Widgets.TextFieldNumeric(baseChanceInputRect, ref settingsGlobalChat.BasePassionSuccessChance, ref baseChanceBuffer, 1.0f, 100.0f);
                    numericBuffers[baseChanceKey] = baseChanceBuffer;

                    y += sectionHeight;

                    // Max success chance
                    Rect maxChanceRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxChanceRect, "Max success chance (%):");
                    Rect maxChanceInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);

                    string maxChanceKey = "passion_maxchance";
                    if (!numericBuffers.ContainsKey(maxChanceKey))
                    {
                        numericBuffers[maxChanceKey] = settingsGlobalChat.MaxPassionSuccessChance.ToString();
                    }
                    string maxChanceBuffer = numericBuffers[maxChanceKey];
                    Widgets.TextFieldNumeric(maxChanceInputRect, ref settingsGlobalChat.MaxPassionSuccessChance, ref maxChanceBuffer,
                        settingsGlobalChat.BasePassionSuccessChance, 100.0f);
                    numericBuffers[maxChanceKey] = maxChanceBuffer;

                    y += sectionHeight;

                    // Description
                    Rect passionDescRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding, 28f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(passionDescRect, "Higher wagers = better success chances. Base chance scales with wager amount.");
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += 20f;
                }

            }
            Widgets.EndScrollView();
        }

        private void ShowCommandSettingsMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>
    {
        new FloatMenuOption("Reset All Commands to Defaults", () => ShowResetConfirmationDialog())
    };

            Find.WindowStack.Add(new FloatMenu(options));
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
                height += 28f; // Extra spacing
            }

            // MILITARY AID-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "militaryaid")
            {
                height += 10f; // Extra spacing
                height += 28f; // Military aid header
                height += 28f; // Default wager
                height += 28f; // Min wager
                height += 28f; // Max wager
                height += 28f; // Extra spacing
            }

            // LOOTBOX-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "openlootbox")
            {
                height += 10f; // Extra spacing
                height += 28f; // Lootbox header
                height += 28f; // Coin range label
                height += 28f; // Coin range inputs
                height += 28f; // Lootboxes per day
                height += 28f; // Show welcome message
                height += 28f; // Force open all
                height += 28f; // Extra spacing
            }

            // PASSION-SPECIFIC SETTINGS HEIGHT
            if (selectedCommand != null && selectedCommand.commandText.ToLower() == "passion")
            {
                height += 10f; // Extra spacing
                height += 28f; // Passion header
                height += 28f; // Min wager
                height += 28f; // Max wager
                height += 28f; // Base success chance
                height += 28f; // Max success chance
                height += 20f; // Description
                height += 28f; // Extra spacing
            }

            return height + 20f; // Extra padding
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
            TaggedString warningText = "You are about to reset all commands to defaults.\n\nExcept the Lootbox Commands coin amounts (because they are global).\n\nThis cannot be undone.".Colorize(Verse.ColorLibrary.RedReadable);

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                warningText,
                () => ResetAllCommandsToDefaults(),
                true,
                "Reset Commands"
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
                Messages.Message("All commands have been reset to defaults", MessageTypeDefOf.TaskCompletion);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error resetting commands: {ex.Message}");
                Messages.Message($"Error resetting commands: {ex.Message}", MessageTypeDefOf.NegativeEvent);
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