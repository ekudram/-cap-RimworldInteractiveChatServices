// Dialog_RaidTypesEditor.cs
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
// A dialog window for editing allowed raid types in chat commands
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class Dialog_RaidTypesEditor : Window
    {
        private CommandSettings settings;
        private Vector2 scrollPosition = Vector2.zero;

        // All possible raid types
        private static readonly List<string> AllRaidTypes = new List<string>
        {
            "standard", "drop", "dropcenter", "dropedge", "dropchaos", "dropgroups",
            "mech", "mechcluster", "water", "wateredge"
        };

        public override Vector2 InitialSize => new Vector2(400f, 500f);

        public Dialog_RaidTypesEditor(CommandSettings settings)
        {
            this.settings = settings;
            doCloseButton = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            optionalTitle = "RICS.RaidTypesEditor.Title".Translate();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "RICS.RaidTypesEditor.AllowedHeader".Translate());
            Text.Font = GameFont.Small;

            // Description
            Widgets.Label(new Rect(0f, 35f, inRect.width, 40f),
                "RICS.RaidTypesEditor.Description".Translate());

            // List of raid types
            Rect listRect = new Rect(0f, 80f, inRect.width, inRect.height - 80f - CloseButSize.y);
            float itemHeight = 30f;
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, AllRaidTypes.Count * itemHeight);

            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            {
                float y = 0f;
                foreach (var raidType in AllRaidTypes)
                {
                    Rect itemRect = new Rect(10f, y, viewRect.width - 20f, itemHeight - 2f);

                    bool isAllowed = settings.AllowedRaidTypes.Contains(raidType);
                    bool newValue = isAllowed;
                    Widgets.CheckboxLabeled(itemRect, GetRaidTypeDisplayName(raidType), ref newValue);

                    if (newValue != isAllowed)
                    {
                        if (newValue)
                            settings.AllowedRaidTypes.Add(raidType);
                        else
                            settings.AllowedRaidTypes.Remove(raidType);
                    }

                    y += itemHeight;
                }
            }
            Widgets.EndScrollView();
        }

        private string GetRaidTypeDisplayName(string raidType)
        {
            return raidType.ToLower() switch
            {
                "standard" => "Standard (Edge Walk-in)",
                "drop" => "Random Drop Pod",
                "dropcenter" => "Center Drop (Deadly)",
                "dropedge" => "Edge Drop",
                "dropchaos" => "Random Chaos Drop",
                "dropgroups" => "Edge Drop Groups",
                "mech" => "Mechanoid Raid",
                "mechcluster" => "Mech Cluster (Royalty)",
                "water" => "Water Edge (Biotech)",
                "wateredge" => "Water Edge (Biotech)",
                _ => raidType
            };
        }
    }
}