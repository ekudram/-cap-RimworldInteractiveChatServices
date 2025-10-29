using CAP_ChatInteractive;
using RimWorld;
using UnityEngine;
using Verse;

namespace _CAP__Chat_Interactive
{
    public static class TabDrawer_OAuth
    {
        private static Vector2 _scrollPosition = Vector2.zero;

        public static void Draw(Rect region)
        {
            var view = new Rect(0f, 0f, region.width - 16f, 600f);

            Widgets.BeginScrollView(region, ref _scrollPosition, view);
            var listing = new Listing_Standard();
            listing.Begin(view);

            // OAuth Client Secrets section
            bool clientSecretsExists = JsonFileManager.FileExists("client_secrets.json");

            Rect oauthRect = listing.GetRect(50f);
            Rect oauthLabelRect = new Rect(oauthRect.x, oauthRect.y, 180f, 30f);
            Rect oauthButtonRect = new Rect(oauthLabelRect.xMax + 10f, oauthRect.y, 120f, 30f);
            Rect oauthStatusRect = new Rect(oauthButtonRect.xMax + 10f, oauthRect.y, 120f, 30f);
            Rect oauthWarningRect = new Rect(oauthRect.x, oauthRect.y + 25f, oauthRect.width, 20f);

            Widgets.Label(oauthLabelRect, "OAuth Config:");
            if (Widgets.ButtonText(oauthButtonRect, clientSecretsExists ? "Edit" : "Create"))
            {
                Find.WindowStack.Add(new Dialog_EditClientSecrets());
            }

            if (clientSecretsExists)
            {
                GUI.color = Color.green;
                Widgets.Label(oauthStatusRect, "✓ Ready");
                GUI.color = Color.white;

                GUI.color = Color.yellow;
                Widgets.Label(oauthWarningRect, "⚠ May require Google verification");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.yellow;
                Widgets.Label(oauthStatusRect, "✗ No OAuth");
                GUI.color = Color.white;

                Widgets.Label(oauthWarningRect, "Chat reading still works without OAuth");
            }
            TooltipHandler.TipRegion(oauthRect, "OAuth 2.0 for sending messages (may require Google verification)");

            // Quota Usage Display for YouTube
            var youtubeService = CAPChatInteractiveMod.Instance?.YouTubeService;
            var youtubeSettings = CAPChatInteractiveMod.Instance.Settings.YouTubeSettings;
            if (youtubeService != null && youtubeSettings.IsConnected)
            {
                listing.Gap(24f);
                Text.Font = GameFont.Medium;
                listing.Label("YouTube API Quota");
                Text.Font = GameFont.Small;
                listing.GapLine(6f);

                listing.Label($"Status: {youtubeService.QuotaStatus}");

                Rect quotaRect = listing.GetRect(22f);
                Widgets.FillableBar(quotaRect, youtubeService.QuotaPercentage / 100f,
                    SolidColorMaterials.NewSolidColorTexture(youtubeService.QuotaColor));

                if (youtubeService.QuotaPercentage >= 80)
                {
                    listing.Gap(4f);
                    GUI.color = Color.yellow;
                    listing.Label("High usage - reduce polling");
                    GUI.color = Color.white;
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}