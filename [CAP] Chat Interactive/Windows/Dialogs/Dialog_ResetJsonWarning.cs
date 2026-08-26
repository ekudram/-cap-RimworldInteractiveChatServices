// Dialog_ResetJsonWarning.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Warning dialog before wiping an editor's main JSON and rebuilding from Defs.
// Backup Now | Confirm Reset | Cancel
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_ResetJsonWarning : Window
    {
        private readonly string editorKey;
        private readonly string mainFileName;
        private readonly Func<string> getCurrentJson;
        private readonly Action onConfirmReset;

        public override Vector2 InitialSize => new Vector2(520f, 280f);

        /// <param name="editorKey">BackupUtility folder key (e.g. CommandManager).</param>
        /// <param name="mainFileName">Display name of main JSON (e.g. CommandSettings.json).</param>
        /// <param name="getCurrentJson">Serialize current in-memory data for Backup Now.</param>
        /// <param name="onConfirmReset">Destructive wipe + rebuild (caller closes/reopens editor).</param>
        public Dialog_ResetJsonWarning(
            string editorKey,
            string mainFileName,
            Func<string> getCurrentJson,
            Action onConfirmReset)
        {
            this.editorKey = editorKey ?? "General";
            this.mainFileName = string.IsNullOrEmpty(mainFileName) ? "JSON" : mainFileName;
            this.getCurrentJson = getCurrentJson;
            this.onConfirmReset = onConfirmReset;
            doCloseButton = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium; 
            GUI.color = Verse.ColorLibrary.RedReadable; ;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "RICS.Editor.ResetJsonWarningTitle".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            string body = "RICS.Editor.ResetJsonWarningBody".Translate(mainFileName);
            Rect bodyRect = new Rect(0f, 40f, inRect.width, inRect.height - 100f);
            Widgets.Label(bodyRect, body);

            float btnH = 36f;
            float btnW = 150f;
            float gap = 10f;
            float y = inRect.height - btnH - 8f;
            float totalW = btnW * 3f + gap * 2f;
            float x0 = (inRect.width - totalW) / 2f;

            Rect backupRect = new Rect(x0, y, btnW, btnH);
            if (Widgets.ButtonText(backupRect, "RICS.Editor.ResetJsonBackupNow".Translate()))
            {
                try
                {
                    string json = getCurrentJson?.Invoke();
                    if (!string.IsNullOrEmpty(json))
                    {
                        BackupUtility.SaveQuickBackup(editorKey, json);
                        Messages.Message("RICS.Editor.QuickBackupSaved".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                    else
                    {
                        Messages.Message("RICS.Editor.ResetJsonBackupEmpty".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ResetJson] Backup failed for {editorKey}: {ex.Message}");
                    Messages.Message("RICS.Editor.ResetJsonFailed".Translate(ex.Message), MessageTypeDefOf.NegativeEvent);
                }
            }

            Rect confirmRect = new Rect(x0 + btnW + gap, y, btnW, btnH);
            if (Widgets.ButtonText(confirmRect, "RICS.Editor.ResetJsonConfirm".Translate()))
            {
                try
                {
                    onConfirmReset?.Invoke();
                    this.Close(doCloseSound: false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ResetJson] Confirm reset failed for {editorKey}: {ex.Message}");
                    Messages.Message("RICS.Editor.ResetJsonFailed".Translate(ex.Message), MessageTypeDefOf.NegativeEvent);
                }
            }

            Rect cancelRect = new Rect(x0 + (btnW + gap) * 2f, y, btnW, btnH);
            if (Widgets.ButtonText(cancelRect, "RICS.Dialog.Cancel".Translate()))
                this.Close();
        }
    }
}
