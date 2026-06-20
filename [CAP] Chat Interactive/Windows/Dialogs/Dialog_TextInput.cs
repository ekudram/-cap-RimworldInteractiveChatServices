// Dialog_TextInput.cs
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
// Reusable simple text input dialog for naming backups, themes, etc.
// Can be used by any editor (CommandManager, Store, Traits, etc.).
// Example usage:
// Find.WindowStack.Add(new Dialog_TextInput("Enter backup name (e.g. Grimwar)", name =>
// {
//     if (!string.IsNullOrWhiteSpace(name))
//         BackupUtility.SaveNamedBackup("CommandManager", name, jsonContent);
// }));

using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_TextInput : Window
    {
        private string inputText = "";
        private readonly string titleText;
        private readonly System.Action<string> onAccept;

        public override Vector2 InitialSize => new Vector2(400f, 150f);

        public Dialog_TextInput(string title, System.Action<string> onAcceptCallback)
        {
            titleText = title;
            onAccept = onAcceptCallback;
            doCloseButton = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), titleText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // Text input field
            Rect textRect = new Rect(10f, 40f, inRect.width - 20f, 30f);
            inputText = Widgets.TextField(textRect, inputText);

            // Buttons row
            float buttonY = inRect.height - 40f;
            float buttonWidth = 120f;
            float buttonGap = 20f;

            Rect okRect = new Rect(inRect.width / 2 - buttonWidth - buttonGap / 2, buttonY, buttonWidth, 30f);
            if (Widgets.ButtonText(okRect, "OK"))
            {
                onAccept?.Invoke(inputText?.Trim() ?? "");
                this.Close();
            }

            Rect cancelRect = new Rect(inRect.width / 2 + buttonGap / 2, buttonY, buttonWidth, 30f);
            if (Widgets.ButtonText(cancelRect, "Cancel"))
            {
                this.Close();
            }
        }
    }
}