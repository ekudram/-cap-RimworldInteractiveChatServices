// MyPawnCommandHandler_Owned.cs
// Copyright (c) Captolamia — AGPLv3
using CAP_ChatInteractive.Ownership;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class MyPawnCommandHandler_Owned
    {
        public const int MaxShown = 10;
        private const string ReturnDivider = " | ";

        private static readonly Dictionary<string, List<int>> LastListByViewer =
            new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        public static string HandleOwned(ChatMessageWrapper user, Pawn pawn, string[] args)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return "RICS.MPCH.Owned.Disabled".Translate();

            string filter = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "all";
            bool wantWeapons = filter == "all" || filter == "weapon" || filter == "weapons";
            bool wantArmor = filter == "all" || filter == "armor" || filter == "armour"
                             || filter == "apparel" || filter == "gear" || filter == "clothes";

            if (!wantWeapons && !wantArmor)
                return "RICS.MPCH.Owned.Usage".Translate();

            var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            var weapons = wantWeapons ? RICS_OwnedItemsCollector.WeaponsSorted(all) : new List<RICS_OwnedItem>();
            var armor = wantArmor ? RICS_OwnedItemsCollector.ApparelSorted(all) : new List<RICS_OwnedItem>();

            var shown = new List<RICS_OwnedItem>();
            var sb = new StringBuilder();
            sb.Append("RICS.MPCH.Owned.Header".Translate(pawn.LabelShortCap));

            int n = 1;
            if (wantWeapons)
            {
                sb.Append(ReturnDivider);
                sb.Append("RICS.MPCH.Owned.Weapons".Translate());
                int extra = Math.Max(0, weapons.Count - MaxShown);
                var slice = weapons.Take(MaxShown).ToList();
                if (slice.Count == 0)
                    sb.Append(" ").Append("RICS.MPCH.Owned.None".Translate());
                else
                {
                    foreach (var item in slice)
                    {
                        shown.Add(item);
                        sb.Append(" ").Append(FormatLine(n++, item));
                    }
                    if (extra > 0)
                        sb.Append(" ").Append("RICS.MPCH.Owned.More".Translate(extra));
                }
            }

            if (wantArmor)
            {
                sb.Append(ReturnDivider);
                sb.Append("RICS.MPCH.Owned.Armor".Translate());
                int extra = Math.Max(0, armor.Count - MaxShown);
                var slice = armor.Take(MaxShown).ToList();
                if (slice.Count == 0)
                    sb.Append(" ").Append("RICS.MPCH.Owned.None".Translate());
                else
                {
                    foreach (var item in slice)
                    {
                        shown.Add(item);
                        sb.Append(" ").Append(FormatLine(n++, item, armor: true));
                    }
                    if (extra > 0)
                        sb.Append(" ").Append("RICS.MPCH.Owned.More".Translate(extra));
                }
            }

            Cache(user, shown);
            sb.Append(ReturnDivider).Append("RICS.MPCH.Owned.DisownHint".Translate());
            return sb.ToString();
        }

        public static string HandleDisown(ChatMessageWrapper user, Pawn pawn, string[] args)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return "RICS.MPCH.Owned.Disabled".Translate();

            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return "RICS.MPCH.Disown.Usage".Translate();

            string query = string.Join(" ", args).Trim();
            var all = RICS_OwnedItemsCollector.CollectForPawn(pawn);
            if (all.Count == 0)
                return "RICS.MPCH.Disown.NoneOwned".Translate();

            Thing target = null;

            if (int.TryParse(query, out int num) && num > 0)
            {
                string key = CacheKey(user);
                if (!LastListByViewer.TryGetValue(key, out var ids) || ids == null || ids.Count == 0)
                    return "RICS.MPCH.Disown.NeedList".Translate();
                if (num > ids.Count)
                    return "RICS.MPCH.Disown.BadNumber".Translate(ids.Count);

                int thingId = ids[num - 1];
                target = all.FirstOrDefault(i => i.Thing != null && i.Thing.thingIDNumber == thingId)?.Thing;
                if (target == null || target.Destroyed)
                    return "RICS.MPCH.Disown.Gone".Translate();
            }
            else
            {
                var matches = all.Where(i => i.Thing != null && !i.Thing.Destroyed
                    && i.Thing.LabelCap.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
                if (matches.Count == 0)
                    return "RICS.MPCH.Disown.NotFound".Translate(query);
                if (matches.Count > 1)
                    return "RICS.MPCH.Disown.Ambiguous".Translate(matches.Count, query);
                target = matches[0].Thing;
            }

            if (!RICS_OwnershipUtility.ClearOwner(target, "viewer disown"))
                return "RICS.MPCH.Disown.Failed".Translate(target.LabelNoCount);

            return "RICS.MPCH.Disown.Ok".Translate(target.LabelNoCount);
        }

        private static string FormatLine(int n, RICS_OwnedItem item, bool armor = false)
        {
            // LabelCap already includes quality (e.g. "Hyperweave gloves (legendary 98%)").
            string label = item.Thing?.LabelCap ?? "?";
            string where = item.Where;
            if (armor)
            {
                string ar = RICS_OwnedItemsCollector.ArmorSummary(item);
                if (!string.IsNullOrEmpty(ar))
                    return $"{n}. {label} ({ar}) — {where}";
            }
            return $"{n}. {label} — {where}";
        }

        private static void Cache(ChatMessageWrapper user, List<RICS_OwnedItem> shown)
        {
            string key = CacheKey(user);
            LastListByViewer[key] = shown
                .Where(i => i.Thing != null)
                .Select(i => i.Thing.thingIDNumber)
                .ToList();
        }

        private static string CacheKey(ChatMessageWrapper user)
        {
            return (user?.Username ?? user?.PlatformUserId ?? "unknown").ToLowerInvariant();
        }
    }
}
