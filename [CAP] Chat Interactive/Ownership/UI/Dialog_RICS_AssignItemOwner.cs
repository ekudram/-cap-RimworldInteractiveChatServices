// Dialog_RICS_AssignItemOwner.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Searchable pawn picker for item/chest ownership (50+ viewer colonies).
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class Dialog_RICS_AssignItemOwner : Window
    {
        private readonly string title;
        private readonly string contextLine;
        private readonly Pawn currentOwner;
        private readonly Action<Pawn> onPicked;
        private readonly bool allowClear;
        private readonly bool includeAllPawnsOption;
        private readonly Action onAllPawns;
        private readonly string pickButtonLabel;
        private readonly string currentPawnLine;

        private Vector2 scrollPos;
        private string searchText = "";
        private List<Pawn> cached = new List<Pawn>();

        public override Vector2 InitialSize => new Vector2(520f, 620f);

        public Dialog_RICS_AssignItemOwner(Comp_RICS_OwnedByPawn comp)
            : this(
                "RICS.Ownership.Dialog.Title".Translate(),
                comp?.parent != null ? "RICS.Ownership.Dialog.Item".Translate(comp.parent.LabelCap).ToString() : "",
                comp?.Owner,
                pawn =>
                {
                    if (comp == null || comp.parent == null || comp.parent.Destroyed)
                        return;
                    if (pawn == null)
                    {
                        comp.ClearOwner("RICS UI");
                        Messages.Message("RICS.Ownership.Cleared".Translate(comp.parent.LabelNoCount),
                            MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    else
                    {
                        comp.SetOwner(pawn, "RICS UI");
                        Messages.Message(
                            "RICS.Ownership.Assigned".Translate(comp.parent.LabelNoCount, pawn.LabelShortCap),
                            MessageTypeDefOf.TaskCompletion,
                            historical: false);
                    }
                },
                allowClear: true)
        {
        }

        public Dialog_RICS_AssignItemOwner(
            string title,
            string contextLine,
            Pawn currentOwner,
            Action<Pawn> onPicked,
            bool allowClear,
            bool includeAllPawnsOption = false,
            Action onAllPawns = null,
            string pickButtonLabel = null,
            string currentPawnLine = null)
        {
            this.title = title ?? "RICS.Ownership.Dialog.Title".Translate();
            this.contextLine = contextLine ?? "";
            this.currentOwner = currentOwner;
            this.onPicked = onPicked;
            this.allowClear = allowClear;
            this.includeAllPawnsOption = includeAllPawnsOption;
            this.onAllPawns = onAllPawns;
            this.pickButtonLabel = string.IsNullOrEmpty(pickButtonLabel)
                ? "RICS.Ownership.Dialog.MakeOwner".Translate()
                : pickButtonLabel;
            this.currentPawnLine = currentPawnLine;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            doCloseButton = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            Rebuild();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = ColorLibrary.HeaderAccent;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            float y = 36f;
            if (!string.IsNullOrEmpty(contextLine))
            {
                Widgets.Label(new Rect(0f, y, inRect.width, 22f), contextLine);
                y += 24f;
            }

            if (currentOwner != null)
            {
                string currentLine = string.IsNullOrEmpty(currentPawnLine)
                    ? "RICS.Ownership.Dialog.CurrentOwner".Translate(currentOwner.LabelShortCap).ToString()
                    : currentPawnLine;
                Widgets.Label(new Rect(0f, y, inRect.width - 160f, 28f), currentLine);
                if (allowClear && Widgets.ButtonText(new Rect(inRect.width - 150f, y, 140f, 28f),
                    "RICS.Ownership.Dialog.Clear".Translate()))
                {
                    onPicked?.Invoke(null);
                    Close();
                    return;
                }
                y += 32f;
            }

            if (includeAllPawnsOption)
            {
                if (Widgets.ButtonText(new Rect(0f, y, 180f, 28f), "RICS.Ownership.Browser.AllPawns".Translate()))
                {
                    onAllPawns?.Invoke();
                    Close();
                    return;
                }
                y += 32f;
            }

            Widgets.Label(new Rect(0f, y, 80f, 28f), "RICS.Ownership.Dialog.Search".Translate());
            string next = Widgets.TextField(new Rect(88f, y, inRect.width - 88f, 28f), searchText);
            if (next != searchText)
            {
                searchText = next;
                Rebuild();
            }
            y += 34f;

            Widgets.Label(new Rect(0f, y, inRect.width, 22f), "RICS.Ownership.Dialog.Choose".Translate());
            y += 24f;

            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            float viewH = Mathf.Max(cached.Count * 36f + 8f, outRect.height);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, viewH);
            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            if (cached.Count == 0)
            {
                Widgets.Label(new Rect(8f, 8f, viewRect.width - 16f, 28f),
                    "RICS.Ownership.Dialog.NoMatches".Translate());
            }
            else
            {
                float rowY = 0f;
                for (int i = 0; i < cached.Count; i++)
                {
                    Pawn pawn = cached[i];
                    if (pawn == null || pawn.Destroyed)
                        continue;

                    Rect row = new Rect(0f, rowY, viewRect.width, 32f);
                    if (i % 2 == 0)
                        Widgets.DrawLightHighlight(row);
                    Widgets.DrawHighlightIfMouseover(row);

                    string viewer = FormatViewerSuffix(pawn);
                    string label = string.IsNullOrEmpty(viewer)
                        ? pawn.LabelShortCap
                        : $"{pawn.LabelShortCap}  ({viewer})";

                    Widgets.Label(new Rect(8f, rowY + 6f, viewRect.width - 176f, 24f), label);
                    if (Widgets.ButtonText(new Rect(viewRect.width - 164f, rowY + 2f, 156f, 28f),
                        pickButtonLabel)
                        || Widgets.ButtonInvisible(row))
                    {
                        onPicked?.Invoke(pawn);
                        Close();
                        break;
                    }
                    rowY += 36f;
                }
            }

            Widgets.EndScrollView();
        }

        private void Rebuild()
        {
            cached.Clear();
            List<Pawn> colonists;
            try
            {
                colonists = PawnsFinder.AllMaps_FreeColonists?.ToList() ?? new List<Pawn>();
            }
            catch
            {
                colonists = new List<Pawn>();
            }

            string q = searchText?.Trim() ?? "";
            foreach (var p in colonists.OrderBy(p => p?.LabelShortCap ?? ""))
            {
                if (p == null || p.Destroyed)
                    continue;
                if (p == currentOwner)
                    continue;
                if (!Matches(p, q))
                    continue;
                cached.Add(p);
            }
        }

        private static bool Matches(Pawn pawn, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;
            if (Contains(pawn.LabelShortCap, query))
                return true;
            try
            {
                if (pawn.Name != null && Contains(pawn.Name.ToStringFull, query))
                    return true;
            }
            catch { }
            TryGetViewerInfo(pawn, out string service, out string username);
            if (!string.IsNullOrEmpty(service) && Contains(service, query))
                return true;
            return !string.IsNullOrEmpty(username) && Contains(username, query);
        }

        /// <summary>Platform only next to the pawn name, e.g. "Twitch" — never "Twitch: captolamia".</summary>
        private static string FormatViewerSuffix(Pawn pawn)
        {
            TryGetViewerInfo(pawn, out string service, out string username);
            if (!string.IsNullOrEmpty(service))
                return service;
            if (string.IsNullOrEmpty(username) || LooksLikeRawId(username))
                return null;
            return username;
        }

        private static void TryGetViewerInfo(Pawn pawn, out string service, out string username)
        {
            service = null;
            username = null;
            try
            {
                var mgr = CAPChatInteractiveMod.GetPawnAssignmentManager();
                string id = mgr?.GetUsernameForPawn(pawn);
                if (string.IsNullOrEmpty(id))
                    return;

                string platKey = null;
                int colon = id.IndexOf(':');
                if (colon > 0)
                    platKey = id.Substring(0, colon);

                service = PrettyService(platKey);

                var viewer = Viewers.GetViewerByPlatformIdentifier(id)
                             ?? (!string.IsNullOrEmpty(id) ? Viewers.GetViewerNoAdd(id) : null);
                if (viewer != null)
                {
                    username = !string.IsNullOrWhiteSpace(viewer.DisplayName)
                        ? viewer.DisplayName
                        : viewer.Username;
                    if (string.IsNullOrEmpty(service) && viewer.PlatformUserIds != null)
                    {
                        foreach (var plat in viewer.PlatformUserIds.Keys)
                        {
                            service = PrettyService(plat);
                            if (!string.IsNullOrEmpty(service))
                                break;
                        }
                    }
                }
            }
            catch { }
        }

        private static string PrettyService(string platform)
        {
            if (string.IsNullOrEmpty(platform))
                return null;
            switch (platform.Trim().ToLowerInvariant())
            {
                case "twitch": return "Twitch";
                case "youtube": return "YouTube";
                case "kick": return "Kick";
                case "username":
                case "name":
                case "unknown":
                    return null;
                default:
                    return char.ToUpperInvariant(platform[0]) + platform.Substring(1);
            }
        }

        private static bool LooksLikeRawId(string s)
        {
            if (string.IsNullOrEmpty(s))
                return true;
            int digits = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsDigit(s[i]))
                    digits++;
            }
            return digits >= 8 && digits * 10 >= s.Length * 7;
        }

        private static bool Contains(string hay, string needle)
        {
            if (string.IsNullOrEmpty(hay) || string.IsNullOrEmpty(needle))
                return false;
            return hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
