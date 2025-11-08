// Dialog_CommandManager.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// A dialog window for managing chat commands and their settings
using Newtonsoft.Json;
using RimWorld;
using System;
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

        private bool IsEventCommand(ChatCommandDef command)
        {
            return command.isEventCommand;
        }

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

            // Header
            Rect headerRect = new Rect(0f, 0f, inRect.width, 40f);
            DrawHeader(headerRect);

            // Main content area
            Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 45f - CloseButSize.y);
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
                    commandSettings = JsonConvert.DeserializeObject<Dictionary<string, CommandSettings>>(json)
                                     ?? new Dictionary<string, CommandSettings>();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error loading command settings: {ex}");
                    commandSettings = new Dictionary<string, CommandSettings>();
                }
            }

            // Initialize settings for all commands
            foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefs)
            {
                if (!commandSettings.ContainsKey(commandDef.defName))
                {
                    commandSettings[commandDef.defName] = new CommandSettings();
                }
            }
        }

        public void SaveCommandSettings()
        {
            // Delegate to the mod class for saving
            var mod = CAPChatInteractiveMod.Instance;
            if (mod != null)
            {
                // You might need to make SaveCommandSettingsToJson public or use an event
                // For now, we'll keep the save logic in the dialog but ensure it's consistent
                try
                {
                    string json = JsonConvert.SerializeObject(commandSettings, Formatting.Indented);
                    JsonFileManager.SaveFile("CommandSettings.json", json);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error saving command settings: {ex}");
                }
            }
        }

        private void DrawHeader(Rect rect)
        {
            Widgets.BeginGroup(rect);

            // Title with counts - left aligned
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, 200f, 30f);
            string titleText = $"Commands ({DefDatabase<ChatCommandDef>.AllDefs.Count()})";
            if (filteredCommands.Count != DefDatabase<ChatCommandDef>.AllDefs.Count())
                titleText += $" - Filtered: {filteredCommands.Count}";
            Widgets.Label(titleRect, titleText);
            Text.Font = GameFont.Small;

            // Search bar
            Rect searchRect = new Rect(210f, 5f, 250f, 30f);
            searchQuery = Widgets.TextField(searchRect, searchQuery);

            // Sort buttons
            Rect sortRect = new Rect(470f, 5f, 300f, 30f);
            DrawSortButtons(sortRect);

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
                    var settings = commandSettings[command.defName];

                    Rect buttonRect = new Rect(5f, y, viewRect.width - 10f, rowHeight - 2f);

                    // Command name with status indicator
                    string displayName = $"!{command.commandText}";
                    if (!command.enabled || !settings.Enabled)
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
            if (!command.enabled) return Color.gray;

            var settings = commandSettings[command.defName];
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

            var settings = commandSettings[selectedCommand.defName];

            // Header with command name
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;

            string headerText = $"!{selectedCommand.commandText}";
            if (!selectedCommand.enabled || !settings.Enabled)
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

                // Cooldown setting
                Rect cooldownLabelRect = new Rect(leftPadding + 10f, y, 250f, sectionHeight);
                Widgets.Label(cooldownLabelRect, "Command Cooldown (seconds):");

                Rect cooldownSliderRect = new Rect(leftPadding + 260f, y, 150f, sectionHeight);
                settings.CooldownSeconds = (int)Widgets.HorizontalSlider(cooldownSliderRect, settings.CooldownSeconds, 1f, 60f);

                Rect cooldownInputRect = new Rect(leftPadding + 420f, y, 60f, sectionHeight);
                string cooldownBuffer = settings.CooldownSeconds.ToString();
                Widgets.TextFieldNumeric(cooldownInputRect, ref settings.CooldownSeconds, ref cooldownBuffer, 1, 60);

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
                    Widgets.Label(costRect, "Cost (coins):");
                    Rect costInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string costBuffer = settings.Cost.ToString();
                    Widgets.TextFieldNumeric(costInputRect, ref settings.Cost, ref costBuffer, 0, 10000);
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
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(aliasDescRect, $"Example: 'bp' for !backpack. Don't include '{globalSettings.Prefix}' or '{globalSettings.BuyPrefix}' prefix.");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                y += 20f;

                // Max uses per stream - WITH TOGGLE
                Rect maxUsesToggleRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.CheckboxLabeled(maxUsesToggleRect, "Limit uses per game session.  Resets each load.", ref settings.UseMaxUsesPerStream);
                y += sectionHeight;

                if (settings.UseMaxUsesPerStream)
                {
                    Rect maxUsesRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxUsesRect, "Max uses (0 = unlimited):");
                    Rect maxUsesInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxUsesBuffer = settings.MaxUsesPerStream.ToString();
                    Widgets.TextFieldNumeric(maxUsesInputRect, ref settings.MaxUsesPerStream, ref maxUsesBuffer, 0, 10000000);
                    y += sectionHeight;
                }

                // Game days cooldown - WITH TOGGLE
                Rect gameDaysToggleRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                Widgets.CheckboxLabeled(gameDaysToggleRect, "Use game days cooldown", ref settings.UseGameDaysCooldown);
                y += sectionHeight;

                if (settings.UseGameDaysCooldown)
                {
                    // Game days cooldown - IMPROVED: Combined slider and numeric input
                    Rect daysLabelRect = new Rect(leftPadding + 20f, y, 250f, sectionHeight);
                    Widgets.Label(daysLabelRect, "Cooldown (game days):");

                    Rect daysSliderRect = new Rect(leftPadding + 260f, y, 150f, sectionHeight);
                    settings.GameDaysCooldown = (int)Widgets.HorizontalSlider(daysSliderRect, settings.GameDaysCooldown, 0f, 30f);

                    Rect daysInputRect = new Rect(leftPadding + 420f, y, 60f, sectionHeight);
                    string daysBuffer = settings.GameDaysCooldown.ToString();
                    Widgets.TextFieldNumeric(daysInputRect, ref settings.GameDaysCooldown, ref daysBuffer, 0, 30);
                    y += sectionHeight;

                    // Description for game days cooldown
                    Rect gameDaysDescRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding, 14f);
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(gameDaysDescRect, "Recommended: Off. Prevents command use for X game days. Use events settings for better control.");
                    Text.Font = GameFont.Small;
                    GUI.color = Color.white;
                    y += 14f;
                }

                // Event Command Settings - Only show for commands like raid, militaryaid
                if (IsEventCommand(selectedCommand))
                {
                    Rect eventHeaderRect = new Rect(leftPadding, y, viewRect.width, sectionHeight);
                    Widgets.Label(eventHeaderRect, "Event Command Settings:");
                    y += sectionHeight;

                    // Use event cooldown toggle
                    Rect eventToggleRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.CheckboxLabeled(eventToggleRect, "Use event cooldown system", ref settings.UseEventCooldown);
                    y += sectionHeight;

                    if (selectedCommand.commandText.ToLower() == "raid" || selectedCommand.commandText.ToLower() == "militaryaid")
                    {
                        // Max uses per cooldown period
                        Rect eventUsesRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                        Widgets.Label(eventUsesRect, "Max uses per cooldown period:");
                        Rect eventUsesInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                        string eventUsesBuffer = settings.MaxUsesPerCooldownPeriod.ToString();
                        Widgets.TextFieldNumeric(eventUsesInputRect, ref settings.MaxUsesPerCooldownPeriod, ref eventUsesBuffer, 0, 100);
                        y += sectionHeight;

                        // Respect global limits toggle
                        Rect globalToggleRect = new Rect(leftPadding + 20f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                        Widgets.CheckboxLabeled(globalToggleRect, "Count toward global event limit", ref settings.RespectsGlobalEventCooldown);
                        y += sectionHeight;
                    }
                }

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
                    Widgets.TextFieldNumeric(wagerInputRect, ref settings.DefaultRaidWager, ref wagerBuffer,
                        settings.MinRaidWager, settings.MaxRaidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "Min wager:");
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinRaidWager.ToString();
                    Widgets.TextFieldNumeric(minWagerInputRect, ref settings.MinRaidWager, ref minWagerBuffer, 100, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "Max wager:");
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerBuffer = settings.MaxRaidWager.ToString();
                    Widgets.TextFieldNumeric(maxWagerInputRect, ref settings.MaxRaidWager, ref maxWagerBuffer,
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
                    Widgets.TextFieldNumeric(wagerInputRect, ref settings.DefaultMilitaryAidWager, ref wagerBuffer,
                        settings.MinMilitaryAidWager, settings.MaxMilitaryAidWager);
                    y += sectionHeight;

                    // Wager range
                    Rect minWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(minWagerRect, "Min wager:");
                    Rect minWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string minWagerBuffer = settings.MinMilitaryAidWager.ToString();
                    Widgets.TextFieldNumeric(minWagerInputRect, ref settings.MinMilitaryAidWager, ref minWagerBuffer, 500, 5000);
                    y += sectionHeight;

                    Rect maxWagerRect = new Rect(leftPadding + 10f, y, viewRect.width - leftPadding - 100f, sectionHeight);
                    Widgets.Label(maxWagerRect, "Max wager:");
                    Rect maxWagerInputRect = new Rect(viewRect.width - 90f, y, 80f, sectionHeight);
                    string maxWagerBuffer = settings.MaxMilitaryAidWager.ToString();
                    Widgets.TextFieldNumeric(maxWagerInputRect, ref settings.MaxMilitaryAidWager, ref maxWagerBuffer,
                        settings.MinMilitaryAidWager, 20000);
                    y += sectionHeight;
                }

            }
            Widgets.EndScrollView();
        }

        private float CalculateDetailsHeight(CommandSettings settings)
        {
            float height = 50f; // Header space
            height += 28f * 5; // Basic info (command, desc, perm)
            height += 38f; // Basic settings label + spacing
            height += 28f; // Enabled
            height += 28f * 1.5f; // Cooldown (taller for description)
            if (settings.SupportsCost) height += 28f; // Cost if applicable
            height += 38f; // Advanced settings label + spacing
            height += 28f; // Command alias
            height += 14f; // Alias description
            height += 28f; // Game days toggle
            if (settings.UseGameDaysCooldown)
            {
                height += 28f; // Game days input
                height += 14f; // Game days description
            }
            height += 28f; // Max uses toggle
            if (settings.UseMaxUsesPerStream) height += 28f; // Max uses input

            // EVENT COMMAND SECTION HEIGHT
            if (selectedCommand != null && IsEventCommand(selectedCommand))
            {
                height += 28f; // Event header
                height += 28f; // Event cooldown toggle
                if (settings.UseEventCooldown)
                {
                    height += 28f; // Max uses per period
                    height += 28f; // Respect global limits
                }
            }

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
    }

    public enum CommandSortMethod
    {
        Name,
        Category,
        Status
    }
}