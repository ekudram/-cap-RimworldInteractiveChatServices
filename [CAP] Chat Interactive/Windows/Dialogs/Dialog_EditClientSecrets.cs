using RimWorld;
using System.IO;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_EditClientSecrets : Window
    {
        private string _currentContent;
        private Vector2 _scrollPosition;
        private Vector2 _mainScrollPosition;
        private bool _fileExists;

        public override Vector2 InitialSize => new Vector2(800f, 700f);

        public Dialog_EditClientSecrets()
        {
            _fileExists = JsonFileManager.FileExists("client_secrets.json");
            _currentContent = _fileExists ?
                JsonFileManager.LoadFile("client_secrets.json") :
                JsonFileManager.GetClientSecretsTemplate();

            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            optionalTitle = "YouTube OAuth 2.0 Setup";
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Calculate total content height
            float instructionsHeight = Text.CalcHeight(GetInstructions(), inRect.width - 30f);
            float jsonLabelHeight = 25f;
            float jsonEditorHeight = 300f;
            float buttonHeight = 30f;
            float spacing = 10f;

            float totalContentHeight = instructionsHeight + jsonLabelHeight + jsonEditorHeight + buttonHeight + (spacing * 4);

            // Main scroll view for entire content
            Rect mainViewRect = new Rect(0f, 0f, inRect.width - 20f, totalContentHeight);
            _mainScrollPosition = GUI.BeginScrollView(inRect, _mainScrollPosition, mainViewRect);
            {
                float y = 0f;

                // Header
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, y, mainViewRect.width, 30f), "YouTube OAuth 2.0 Client Setup");
                Text.Font = GameFont.Small;
                y += 35f;

                // Warning
                Rect warningRect = new Rect(0f, y, mainViewRect.width, 40f);
                GUI.color = Color.red;
                Widgets.Label(warningRect, "⚠ WARNING: YouTube OAuth may require Google verification (days/weeks)");
                GUI.color = Color.white;
                y += 45f;

                // Instructions
                string instructions = GetInstructions();
                Rect instructionsRect = new Rect(0f, y, mainViewRect.width, instructionsHeight);
                Widgets.Label(instructionsRect, instructions);
                y += instructionsHeight + spacing;

                // JSON editor label
                Widgets.Label(new Rect(0f, y, mainViewRect.width, jsonLabelHeight), "OAuth Client Secrets JSON:");
                y += jsonLabelHeight;

                // JSON editor with its own scroll view
                Rect jsonRect = new Rect(0f, y, mainViewRect.width, jsonEditorHeight);
                float jsonContentHeight = Text.CalcHeight(_currentContent, jsonRect.width - 30f) + 20f;
                Rect jsonViewRect = new Rect(0f, 0f, jsonRect.width - 20f, Mathf.Max(jsonContentHeight, jsonRect.height));

                _scrollPosition = GUI.BeginScrollView(jsonRect, _scrollPosition, jsonViewRect);
                {
                    _currentContent = Widgets.TextArea(new Rect(0f, 0f, jsonViewRect.width, jsonViewRect.height), _currentContent);
                }
                GUI.EndScrollView();
                y += jsonEditorHeight + spacing;

                // Buttons at bottom of content
                Rect buttonRect = new Rect(0f, y, mainViewRect.width, buttonHeight);

                if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, 120f, 30f), "Use Template"))
                {
                    _currentContent = JsonFileManager.GetClientSecretsTemplate();
                }

                if (Widgets.ButtonText(new Rect(buttonRect.x + 130f, buttonRect.y, 120f, 30f), "Validate JSON"))
                {
                    if (_currentContent.Contains("YOUR_CLIENT_ID_HERE") || _currentContent.Contains("YOUR_CLIENT_SECRET_HERE"))
                    {
                        Messages.Message("Replace placeholder values with your actual Client ID and Secret!", MessageTypeDefOf.NegativeEvent);
                    }
                    else if (_currentContent.Contains("client_id") && _currentContent.Contains("client_secret"))
                    {
                        Messages.Message("JSON format looks good!", MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message("JSON appears incomplete", MessageTypeDefOf.NegativeEvent);
                    }
                }

                if (Widgets.ButtonText(new Rect(buttonRect.x + 260f, buttonRect.y, 120f, 30f), "Save File"))
                {
                    if (JsonFileManager.SaveFile("client_secrets.json", _currentContent))
                    {
                        Messages.Message("OAuth configuration saved! You can now send YouTube messages.", MessageTypeDefOf.PositiveEvent);
                        Close();
                    }
                }
            }
            GUI.EndScrollView();
        }

        private string GetInstructions()
        {
            return @"<b>⚠ IMPORTANT: YouTube OAuth Verification</b>
• You can skip this.  It may never work.  Go yell at Google Cloud about it.
• New apps often require <b>Google verification</b>
• Process can take <b>days or weeks</b>  
• May require <b>paid Google Cloud account</b>
• <color=#4A90E2>Consider using chat reading only</color> (no OAuth needed)

<b>STEP 1: Get Client ID & Secret from Google</b>
1. Go to <color=#4A90E2>console.cloud.google.com</color>
2. Create project or select existing
3. Enable 'YouTube Data API v3'
4. Go to <b>Credentials</b> → <b>Create Credentials</b> → <b>OAuth 2.0 Client ID</b>
5. Application type: <b>Desktop Application</b>
6. Copy the <b>Client ID</b> and <b>Client Secret</b>

<b>STEP 2: Fill in the JSON below</b>
• Replace <color=#FF6B6B>YOUR_CLIENT_ID_HERE</color> with your actual Client ID
• Replace <color=#FF6B6B>YOUR_CLIENT_SECRET_HERE</color> with your actual Client Secret
• Keep the project_id as 'cap-chat-interactive'

<b>STEP 3: Save and Authenticate</b>
• First connection will open browser for OAuth
• Grant permissions to your YouTube account
• This enables <b>sending messages</b> to chat";
        }
    }
}