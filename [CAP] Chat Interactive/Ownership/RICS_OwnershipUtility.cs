// RICS_OwnershipUtility.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
using RimWorld;
using System;
using System.Reflection;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public static class RICS_OwnershipUtility
    {
        public static bool IsRicsOwnershipActive()
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
                if (settings == null || !settings.UseRicsPawnOwnership)
                    return false;
                if (RICS_OwnershipModDetector.IsPossessionsPlusActive())
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsMarkedNotOwnable(Thing thing)
        {
            var c = thing?.TryGetComp<Comp_RICS_NotOwnable>();
            return c != null && c.NotOwnable;
        }

        public static Comp_RICS_OwnedByPawn GetOwnershipComp(Thing thing)
        {
            return thing?.TryGetComp<Comp_RICS_OwnedByPawn>();
        }

        public static Pawn GetOwner(Thing thing)
        {
            return GetOwnershipComp(thing)?.Owner;
        }

        public static bool SetOwner(Thing thing, Pawn owner, string reason = null)
        {
            if (thing == null || thing.Destroyed)
                return false;

            // Prefer Possessions Plus when present (RICS ownership forced off)
            if (RICS_OwnershipModDetector.IsPossessionsPlusActive())
                return TrySetPossessionsPlusOwner(thing, owner, reason);

            if (!IsRicsOwnershipActive())
                return false;

            var comp = GetOwnershipComp(thing);
            if (comp == null)
            {
                string defName = thing.def?.defName ?? "?";
                Logger.Warning(
                    $"[RICS Ownership] No Comp_RICS_OwnedByPawn on {defName}. " +
                    "Restart RimWorld after enabling ownership so def injection runs, " +
                    "and ensure Possessions Plus is not loaded.");
                return false;
            }

            if (owner != null && IsMarkedNotOwnable(thing))
                return false;

            comp.SetOwner(owner, reason);
            return true;
        }

        public static bool ClearOwner(Thing thing, string reason = null)
            => SetOwner(thing, null, reason);

        public static bool BlocksUseBy(Thing thing, Pawn pawn)
        {
            if (thing == null || pawn == null)
                return false;
            if (RICS_OwnershipModDetector.IsPossessionsPlusActive())
                return false; // PP handles its own blocks
            var comp = GetOwnershipComp(thing);
            return comp != null && comp.BlocksUseBy(pawn);
        }

        /// <summary>Reflection path used when Possessions Plus is the active ownership system.</summary>
        public static bool TrySetPossessionsPlusOwner(Thing item, Pawn ownerPawn, string reason = null)
        {
            try
            {
                if (item == null || ownerPawn == null)
                    return false;

                Type ownershipCompType = Type.GetType("PossessionsPlus.CompOwnedByPawn_Item, PossessionsPlus");
                if (ownershipCompType == null)
                    return false;
                if (!(item is ThingWithComps thingWithComps))
                    return false;

                var getCompMethod = typeof(ThingWithComps).GetMethod("GetComp")?.MakeGenericMethod(ownershipCompType);
                if (getCompMethod == null)
                    return false;

                var ownershipComp = getCompMethod.Invoke(thingWithComps, null);
                if (ownershipComp == null)
                    return false;

                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var setOwner = ownershipCompType.GetMethod("SetOwner", flags);
                if (setOwner != null)
                {
                    // SetOwner(Pawn, bool force = false, string reason = null)
                    var pars = setOwner.GetParameters();
                    if (pars.Length >= 1)
                    {
                        object[] args;
                        if (pars.Length >= 3)
                            args = new object[] { ownerPawn, true, reason ?? "RICS purchase" };
                        else if (pars.Length == 2)
                            args = new object[] { ownerPawn, true };
                        else
                            args = new object[] { ownerPawn };
                        setOwner.Invoke(ownershipComp, args);
                        return true;
                    }
                }

                var ownerField = ownershipCompType.GetField("owner", flags);
                if (ownerField == null)
                    return false;
                ownerField.SetValue(ownershipComp, ownerPawn);

                var startDayField = ownershipCompType.GetField("OwnershipStartDay", flags);
                if (startDayField != null)
                {
                    try
                    {
                        int currentDay = GenLocalDate.DayOfYear(ownerPawn.MapHeld ?? Find.CurrentMap) + 1;
                        startDayField.SetValue(ownershipComp, currentDay);
                    }
                    catch { }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[RICS Ownership] PossessionsPlus SetOwner failed: {ex.Message}");
                return false;
            }
        }
    }
}
