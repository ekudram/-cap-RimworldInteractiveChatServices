// Dialog_RaidStrategiesEditor.cs
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
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RaidStrategiesEditor : Window
    {
        private CommandSettings settings;
        private Vector2 scrollPosition = Vector2.zero;

        // Mirror the StrategyAliases from your command handler
        private static readonly Dictionary<string, RaidStrategyDef> StrategyAliases = new Dictionary<string, RaidStrategyDef>(StringComparer.OrdinalIgnoreCase)
        {
            { "default", null }, // Special case for storyteller choice
            { "immediate", RaidStrategyDefOf.ImmediateAttack },
            { "smart", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSmart") ?? RaidStrategyDefOf.ImmediateAttack },
            { "sappers", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackSappers") ?? RaidStrategyDefOf.ImmediateAttack },
            { "breach", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackBreaching") ?? RaidStrategyDefOf.ImmediateAttack },
            { "breachsmart", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("ImmediateAttackBreachingSmart") ?? RaidStrategyDefOf.ImmediateAttack },
            { "stage", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("StageThenAttack") ?? RaidStrategyDefOf.ImmediateAttack },
            { "siege", DefDatabase<RaidStrategyDef>.GetNamedSilentFail("Siege") ?? RaidStrategyDefOf.ImmediateAttack }
        };

        // Build display names from the actual Defs
        private static Dictionary<string, string> strategyDisplayNames;

        static Dialog_RaidStrategiesEditor()
        {
            strategyDisplayNames = new Dictionary<string, string>();

            foreach (var kvp in StrategyAliases)
            {
                string displayName;

                if (kvp.Key == "default")
                {
                    displayName = "RICS.RSE.Strategy.Default".Translate();
                }
                else if (kvp.Value != null)
                {
                    // Use the def's label and add friendly description
                    displayName = kvp.Value.LabelCap.Resolve();

                    // Add contextual hints based on the key
                    switch (kvp.Key)
                    {
                        case "smart":
                            displayName += " (" + "RICS.RSE.Strategy.SmartHint".Translate() + ")";
                            break;
                        case "sappers":
                            displayName += " (" + "RICS.RSE.Strategy.SappersHint".Translate() + ")";
                            break;
                        case "breach":
                            displayName += " (" + "RICS.RSE.Strategy.BreachHint".Translate() + ")";
                            break;
                        case "breachsmart":
                            displayName += " (" + "RICS.RSE.Strategy.BreachSmartHint".Translate() + ")";
                            break;
                        case "stage":
                            displayName += " (" + "RICS.RSE.Strategy.StageHint".Translate() + ")";
                            break;
                        case "siege":
                            displayName += " (" + "RICS.RSE.Strategy.SiegeHint".Translate() + ")";
                            break;
                    }
                }
                else
                {
                    displayName = kvp.Key.CapitalizeFirst();
                }

                strategyDisplayNames[kvp.Key] = displayName;
            }
        }

        // Property to maintain backward compatibility
        private static Dictionary<string, string> AllStrategies => strategyDisplayNames;

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public Dialog_RaidStrategiesEditor(CommandSettings settings)
        {
            this.settings = settings;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Header
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "RICS.RSE.Header.RaidStrategies".Translate());
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            // Description
            Widgets.Label(new Rect(0f, 35f, inRect.width, 40f), "RICS.RSE.Description".Translate());

            // Quick action buttons
            Rect quickActionsRect = new Rect(0f, 80f, inRect.width, 30f);
            DrawQuickActions(quickActionsRect);

            // List of strategies
            Rect listRect = new Rect(0f, 115f, inRect.width, inRect.height - 115f - CloseButSize.y);
            float itemHeight = 30f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, AllStrategies.Count * itemHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var strategy in AllStrategies)
                {
                    Rect itemRect = new Rect(10f, y, viewRect.width - 20f, itemHeight - 2f);

                    bool isAllowed = settings.AllowedRaidStrategies.Contains(strategy.Key);
                    bool newValue = isAllowed;
                    Widgets.CheckboxLabeled(itemRect, strategy.Value, ref newValue);

                    if (newValue != isAllowed)
                    {
                        if (newValue)
                            settings.AllowedRaidStrategies.Add(strategy.Key);
                        else
                            settings.AllowedRaidStrategies.Remove(strategy.Key);
                    }

                    y += itemHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawQuickActions(Rect rect)
        {
            Widgets.BeginGroup(rect);

            float buttonWidth = 120f;
            float spacing = 5f;
            float x = 0f;

            // Select All button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.RSE.SelectAll".Translate()))
            {
                settings.AllowedRaidStrategies.Clear();
                foreach (var strategyKey in AllStrategies.Keys)
                {
                    settings.AllowedRaidStrategies.Add(strategyKey);
                }
            }
            x += buttonWidth + spacing;

            // Select None button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.RSE.SelectNone".Translate()))
            {
                settings.AllowedRaidStrategies.Clear();
            }
            x += buttonWidth + spacing;

            // Select Basic button
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 30f), "RICS.RSE.SelectBasic".Translate()))
            {
                settings.AllowedRaidStrategies.Clear();

                // Match your command handler's basic strategies
                var basicStrategies = new[] { "default", "immediate", "smart" };
                foreach (var key in basicStrategies)
                {
                    if (AllStrategies.ContainsKey(key))
                    {
                        settings.AllowedRaidStrategies.Add(key);
                    }
                }
            }

            Widgets.EndGroup();
        }
    }
}