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
// Dialog window for Quality and Research settings 
//
using CAP_ChatInteractive;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;
using ColorLibrary = CAP_ChatInteractive.ColorLibrary;

public class Dialog_QualityResearchSettings : Window
{
    private Vector2 scrollPosition = Vector2.zero;
    private CAPChatInteractiveSettings settings;
    private Dictionary<string, string> qualityMultiplierBuffers = new Dictionary<string, string>();
    public override Vector2 InitialSize => new Vector2(452f, 600f); // Increased from 600f to 650f

    public Dialog_QualityResearchSettings(CAPChatInteractiveSettings settings)
    {
        this.settings = settings;
        doCloseButton = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    // Remove all the static field declarations and use settings.GlobalSettings instead

    public override void DoWindowContents(Rect inRect)
    {
        // Header
        Text.Font = GameFont.Medium;
        Rect headerRect = new Rect(0f, 0f, inRect.width, 40f);
        Widgets.Label(headerRect, "Quality & Research Settings");
        Text.Font = GameFont.Small;

        // Main content area
        Rect contentRect = new Rect(0f, 45f, inRect.width, inRect.height - 45f - CloseButSize.y);
        DrawContent(contentRect);
    }

    private void DrawContent(Rect rect)
    {
        Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 420f); // Increased height for the button

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        {
            float y = 0f;

            // Quality Settings Section
            y = DrawQualitySection(new Rect(0f, y, viewRect.width, 260f));

            // Research Settings Section  
            y = DrawResearchSection(new Rect(0f, y + 10f, viewRect.width, 120f));

            // Info text
            Rect infoRect = new Rect(0f, y + 210f, viewRect.width, 90f);
            DrawInfoText(infoRect);

            // Reset to defaults button
            Rect buttonRect = new Rect(0f, y + 300f, 200f, 30f);
            DrawResetButton(buttonRect);
        }
        Widgets.EndScrollView();
    }

    private float DrawQualitySection(Rect rect)
    {
        Widgets.BeginGroup(rect);

        // Section header
        Text.Font = GameFont.Medium;
        Rect headerRect = new Rect(0f, 0f, rect.width, 30f);
        Widgets.Label(headerRect, "Allowed Quality Levels & Multipliers");
        Text.Font = GameFont.Small;

        float y = 35f;
        float checkboxHeight = 30f;

        // Quality checkboxes with MMO colors and multiplier inputs
        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Awful", ref settings.GlobalSettings.AllowAwfulQuality, Color.red, "Awful");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Poor", ref settings.GlobalSettings.AllowPoorQuality, new Color(0.65f, 0.50f, 0.39f), "Poor");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Normal", ref settings.GlobalSettings.AllowNormalQuality, Color.white, "Normal");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Good", ref settings.GlobalSettings.AllowGoodQuality, Color.green, "Good");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Excellent", ref settings.GlobalSettings.AllowExcellentQuality, Color.blue, "Excellent");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Masterwork", ref settings.GlobalSettings.AllowMasterworkQuality, new Color(0.5f, 0f, 0.5f), "Masterwork");
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Legendary", ref settings.GlobalSettings.AllowLegendaryQuality, new Color(1f, 0.5f, 0f), "Legendary");

        Widgets.EndGroup();
        return rect.height;
    }


    private void DrawQualityCheckbox(Rect rect, string label, ref bool value, Color color, string qualityType)
    {
        float y = rect.y + 5f;
        float spacing = 10f;

        // Color swatch
        Rect colorRect = new Rect(rect.x, y, 20f, 20f);
        Widgets.DrawBoxSolid(colorRect, color);
        Widgets.DrawBox(colorRect);

        // Checkbox
        Rect checkboxRect = new Rect(colorRect.xMax + spacing, rect.y, 150f, rect.height);
        bool previousValue = value;
        Widgets.CheckboxLabeled(checkboxRect, label, ref value);

        if (value != previousValue)
        {
            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
        }

        // Get the multiplier value for this quality type
        float multiplier = 1.0f;
        switch (qualityType)
        {
            case "Awful": multiplier = settings.GlobalSettings.AwfulQuality; break;
            case "Poor": multiplier = settings.GlobalSettings.PoorQuality; break;
            case "Normal": multiplier = settings.GlobalSettings.NormalQuality; break;
            case "Good": multiplier = settings.GlobalSettings.GoodQuality; break;
            case "Excellent": multiplier = settings.GlobalSettings.ExcellentQuality; break;
            case "Masterwork": multiplier = settings.GlobalSettings.MasterworkQuality; break;
            case "Legendary": multiplier = settings.GlobalSettings.LegendaryQuality; break;
        }

        // Label for multiplier
        Rect multiplierLabelRect = new Rect(checkboxRect.xMax + spacing, rect.y, 140f, rect.height);
        string multiplierText = $"Multiplier: {multiplier * 100:F0}%";
        Widgets.Label(multiplierLabelRect, multiplierText);

        // Input field for multiplier - CHANGED: We'll work with percentage in the UI
        Rect inputRect = new Rect(multiplierLabelRect.xMax + 5f, rect.y, 60f, rect.height);

        // Initialize buffer for this quality type if it doesn't exist
        if (!qualityMultiplierBuffers.ContainsKey(qualityType))
        {
            // Store as percentage (0.5 multiplier = 50%)
            qualityMultiplierBuffers[qualityType] = (multiplier * 100).ToString("F0");
        }

        // Get the buffer for this quality type
        string buffer = qualityMultiplierBuffers[qualityType];

        // Create a temporary variable to hold the percentage value from the UI
        float percentageValue = multiplier * 100;

        // Draw input field - user enters percentage (500 = 500%)
        Widgets.TextFieldNumeric(inputRect, ref percentageValue, ref buffer, 0f, 99999f);

        // Store the updated buffer back
        qualityMultiplierBuffers[qualityType] = buffer;

        // Convert percentage back to multiplier (500% = 5.0)
        float newMultiplier = percentageValue / 100f;

        // Save back to the correct setting
        switch (qualityType)
        {
            case "Awful": settings.GlobalSettings.AwfulQuality = newMultiplier; break;
            case "Poor": settings.GlobalSettings.PoorQuality = newMultiplier; break;
            case "Normal": settings.GlobalSettings.NormalQuality = newMultiplier; break;
            case "Good": settings.GlobalSettings.GoodQuality = newMultiplier; break;
            case "Excellent": settings.GlobalSettings.ExcellentQuality = newMultiplier; break;
            case "Masterwork": settings.GlobalSettings.MasterworkQuality = newMultiplier; break;
            case "Legendary": settings.GlobalSettings.LegendaryQuality = newMultiplier; break;
        }
    }

    private float DrawResearchSection(Rect rect)
    {
        Widgets.BeginGroup(rect);

        // Section header
        Text.Font = GameFont.Medium;
        Rect headerRect = new Rect(0f, 0f, rect.width, 30f);
        Widgets.Label(headerRect, "Research Requirements");
        Text.Font = GameFont.Small;

        float y = 35f;
        float checkboxHeight = 30f;

        // Research checkboxes - now using settings
        Rect researchRect = new Rect(0f, y, rect.width, checkboxHeight);
        bool previousResearch = settings.GlobalSettings.RequireResearch;
        Widgets.CheckboxLabeled(researchRect, "Enable research requirements", ref settings.GlobalSettings.RequireResearch);
        if (settings.GlobalSettings.RequireResearch != previousResearch)
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
        }

        y += checkboxHeight;

        Widgets.EndGroup();
        return rect.height;
    }

    private void DrawInfoText(Rect rect)
    {
        Text.Font = GameFont.Tiny;
        GUI.color = ColorLibrary.LightGray;

        string infoText = "These settings affect the purchase commands:\n" +
                         "• Quality levels determine what qualities viewers can request\n" +
                         "• Multipliers affect the price of items (100% = normal price)\n" +
                         "• Research setting controls whether items can by bought before\n"+
                         "    items are researched. ✔️ means research required.\n" +
                         "• Use 'Reset Multipliers' button to restore defaults\n" +
                         "Changes take effect immediately for new purchases.";

        Widgets.Label(rect, infoText);

        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }

    private void DrawResetButton(Rect rect)
    {
        if (Widgets.ButtonText(rect, "Reset Multipliers to Defaults"))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();

            // Reset all multipliers to default values
            settings.GlobalSettings.AwfulQuality = 0.5f;
            settings.GlobalSettings.PoorQuality = 0.75f;
            settings.GlobalSettings.NormalQuality = 1.0f;
            settings.GlobalSettings.GoodQuality = 1.5f;
            settings.GlobalSettings.ExcellentQuality = 2.0f;
            settings.GlobalSettings.MasterworkQuality = 3.0f;
            settings.GlobalSettings.LegendaryQuality = 5.0f;

            // Clear the buffers so they'll be re-initialized with the new values
            qualityMultiplierBuffers.Clear();

            // Show a confirmation message
            Messages.Message("Quality multipliers reset to default values", MessageTypeDefOf.PositiveEvent);

            // Force a UI refresh
            Find.WindowStack.TryRemove(this, true);
            Find.WindowStack.Add(new Dialog_QualityResearchSettings(settings));
        }

        // Add a tooltip
        TooltipHandler.TipRegion(rect, "Reset all quality price multipliers to their default values:\n" +
                                      "• Awful: 50%\n" +
                                      "• Poor: 75%\n" +
                                      "• Normal: 100%\n" +
                                      "• Good: 150%\n" +
                                      "• Excellent: 200%\n" +
                                      "• Masterwork: 300%\n" +
                                      "• Legendary: 500%");
    }

    public override void PostClose()
    {
        base.PostClose();

        // Force save the settings when the dialog closes
        if (settings != null)
        {
            try
            {
                // This will trigger the WriteSettings method in your mod class
                CAPChatInteractiveMod.Instance.Settings.Write();

                // Alternative: directly call the mod's WriteSettings
                // CAPChatInteractiveMod.Instance.WriteSettings();

                CAP_ChatInteractive.Logger.Debug("Quality research settings saved on dialog close");
            }
            catch (Exception ex)
            {
                CAP_ChatInteractive.Logger.Error($"Failed to save settings on dialog close: {ex}");
            }
        }
    }
}