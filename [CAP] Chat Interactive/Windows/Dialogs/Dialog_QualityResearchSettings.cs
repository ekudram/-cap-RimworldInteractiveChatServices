using CAP_ChatInteractive;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

public class Dialog_QualityResearchSettings : Window
{
    private Vector2 scrollPosition = Vector2.zero;
    private CAPChatInteractiveSettings settings;

    public override Vector2 InitialSize => new Vector2(500f, 600f);

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
        Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 400f);

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        {
            float y = 0f;

            // Quality Settings Section
            y = DrawQualitySection(new Rect(0f, y, viewRect.width, 260f));

            // Research Settings Section  
            y = DrawResearchSection(new Rect(0f, y + 10f, viewRect.width, 120f));

            // Info text
            Rect infoRect = new Rect(0f, y + 210f, viewRect.width, 60f);
            DrawInfoText(infoRect);
        }
        Widgets.EndScrollView();
    }

    private float DrawQualitySection(Rect rect)
    {
        Widgets.BeginGroup(rect);

        // Section header
        Text.Font = GameFont.Medium;
        Rect headerRect = new Rect(0f, 0f, rect.width, 30f);
        Widgets.Label(headerRect, "Allowed Quality Levels");
        Text.Font = GameFont.Small;

        float y = 35f;
        float checkboxHeight = 30f;

        // Quality checkboxes with MMO colors - now using settings
        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Awful", ref settings.GlobalSettings.AllowAwfulQuality, Color.gray);
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Poor", ref settings.GlobalSettings.AllowPoorQuality, new Color(0.8f, 0.8f, 0.8f));
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Normal", ref settings.GlobalSettings.AllowNormalQuality, Color.white);
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Good", ref settings.GlobalSettings.AllowGoodQuality, Color.green);
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Excellent", ref settings.GlobalSettings.AllowExcellentQuality, Color.blue);
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Masterwork", ref settings.GlobalSettings.AllowMasterworkQuality, new Color(0.5f, 0f, 0.5f));
        y += checkboxHeight;

        DrawQualityCheckbox(new Rect(0f, y, rect.width, checkboxHeight), "Legendary", ref settings.GlobalSettings.AllowLegendaryQuality, new Color(1f, 0.5f, 0f));

        Widgets.EndGroup();
        return rect.height;
    }

    private void DrawQualityCheckbox(Rect rect, string label, ref bool value, Color color)
    {
        // Color swatch
        Rect colorRect = new Rect(rect.x, rect.y + 5f, 20f, 20f);
        Widgets.DrawBoxSolid(colorRect, color);
        Widgets.DrawBox(colorRect);

        // Checkbox
        Rect checkboxRect = new Rect(colorRect.xMax + 10f, rect.y, rect.width - 30f, rect.height);
        bool previousValue = value;
        Widgets.CheckboxLabeled(checkboxRect, label, ref value);

        if (value != previousValue)
        {
            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
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

        // Only show this if research requirements are enabled
        if (settings.GlobalSettings.RequireResearch)
        {
            Rect allowUnresearchedRect = new Rect(20f, y, rect.width - 20f, checkboxHeight);
            bool previousAllow = settings.GlobalSettings.AllowUnresearchedItems;
            Widgets.CheckboxLabeled(allowUnresearchedRect, "Allow purchase of unresearched items", ref settings.GlobalSettings.AllowUnresearchedItems);
            if (settings.GlobalSettings.AllowUnresearchedItems != previousAllow)
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
            }
        }

        Widgets.EndGroup();
        return rect.height;
    }

    private void DrawInfoText(Rect rect)
    {
        Text.Font = GameFont.Tiny;
        GUI.color = Color.gray;

        string infoText = "These settings affect the !buy command:\n" +
                         "• Quality levels determine what qualities viewers can request\n" +
                         "• Research settings control whether items require research\n" +
                         "Changes take effect immediately for new purchases.";

        Widgets.Label(rect, infoText);

        Text.Font = GameFont.Small;
        GUI.color = Color.white;
    }
}