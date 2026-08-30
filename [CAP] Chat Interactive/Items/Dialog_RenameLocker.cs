// File: Dialog_RenameLocker.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// This file defines a dialog window that allows the player to rename a Rimazon locker.

using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RenameLocker : Window
    {
        private string curName;
        private Building_RimazonLocker locker;

        public Dialog_RenameLocker(Building_RimazonLocker locker)
        {
            this.locker = locker;
            curName = locker.customName ?? "";
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(400f, 175f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "Rename locker (leave blank to reset):");

            curName = Widgets.TextField(new Rect(0f, 40f, inRect.width, 35f), curName);

            if (Widgets.ButtonText(new Rect(15f, inRect.height - 35f - 15f, inRect.width / 2 - 20f, 35f), "OK"))
            {
                locker.RenameLocker(curName);
                Close();
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, inRect.height - 35f - 15f, inRect.width / 2 - 20f, 35f), "Cancel"))
            {
                Close();
            }
        }
    }
}
