// RICS_InheritanceHeirSelector.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Heir order: Spouse (all) → Children → Best Friend → siblings/parents fallback.
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Ownership.Inheritance
{
    public static class RICS_InheritanceHeirSelector
    {
        public static Pawn SelectHeir(Pawn deceased)
        {
            if (deceased?.relations == null)
                return null;

            var spouses = GetRelations(deceased, PawnRelationDefOf.Spouse)
                .Where(IsEligible)
                .ToList();
            if (spouses.Count > 0)
                return spouses.OrderByDescending(p => Opinion(deceased, p)).First();

            var children = GetChildren(deceased).Where(IsEligible).ToList();
            if (children.Count > 0)
                return children.OrderByDescending(p => Opinion(deceased, p)).First();

            var bestFriend = FindBestFriend(deceased);
            if (bestFriend != null)
                return bestFriend;

            var siblings = GetRelations(deceased, PawnRelationDefOf.Sibling).Where(IsEligible).ToList();
            if (siblings.Count > 0)
                return siblings.OrderByDescending(p => Opinion(deceased, p)).First();

            var parents = GetRelations(deceased, PawnRelationDefOf.Parent).Where(IsEligible).ToList();
            if (parents.Count > 0)
                return parents.OrderByDescending(p => Opinion(deceased, p)).First();

            // Last resort: any free colonist on same faction (not deceased)
            try
            {
                return PawnsFinder.AllMaps_FreeColonistsSpawned?
                    .Where(p => p != null && p != deceased && IsEligible(p))
                    .OrderByDescending(p => Opinion(deceased, p))
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsEligible(Pawn p)
        {
            if (p == null || p.Destroyed || p.Dead || p.Discarded)
                return false;
            if (p.Faction != Faction.OfPlayer)
                return false;
            return p.RaceProps?.Humanlike == true;
        }

        private static int Opinion(Pawn a, Pawn b)
        {
            try { return a.relations?.OpinionOf(b) ?? 0; }
            catch { return 0; }
        }

        private static IEnumerable<Pawn> GetRelations(Pawn deceased, PawnRelationDef def)
        {
            if (deceased?.relations?.DirectRelations == null)
                yield break;
            foreach (var rel in deceased.relations.DirectRelations)
            {
                if (rel?.def == def && rel.otherPawn != null && rel.otherPawn != deceased)
                    yield return rel.otherPawn;
            }
        }

        private static List<Pawn> GetChildren(Pawn deceased)
        {
            var set = new HashSet<Pawn>();
            foreach (var c in GetRelations(deceased, PawnRelationDefOf.Child))
                set.Add(c);

            // Also scan colonists who list deceased as Parent
            try
            {
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned ?? Enumerable.Empty<Pawn>())
                {
                    if (p == null || p == deceased || p.relations?.DirectRelations == null)
                        continue;
                    foreach (var rel in p.relations.DirectRelations)
                    {
                        if (rel?.def == PawnRelationDefOf.Parent && rel.otherPawn == deceased)
                            set.Add(p);
                    }
                }
            }
            catch { }

            return set.ToList();
        }

        private static Pawn FindBestFriend(Pawn deceased)
        {
            Pawn best = null;
            int bestOpinion = 40; // require meaningful friendship
            try
            {
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned ?? Enumerable.Empty<Pawn>())
                {
                    if (!IsEligible(p) || p == deceased)
                        continue;
                    int op = Opinion(deceased, p);
                    if (op > bestOpinion)
                    {
                        bestOpinion = op;
                        best = p;
                    }
                }
            }
            catch { }
            return best;
        }
    }
}
