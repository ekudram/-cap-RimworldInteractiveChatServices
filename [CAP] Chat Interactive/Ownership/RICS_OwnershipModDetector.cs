// RICS_OwnershipModDetector.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Detects Possessions Plus and other ownership mods. Forces RICS ownership off when conflicting.
using RimWorld;
using System;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public static class RICS_OwnershipModDetector
    {
        public const string PossessionsPlusPackageId = "Side1iner.PossessionsPlus";

        private static bool? _ppCached;
        private static bool _notifiedThisSession;

        public static bool IsPossessionsPlusActive()
        {
            if (_ppCached.HasValue)
                return _ppCached.Value;

            try
            {
                _ppCached = ModLister.GetActiveModWithIdentifier(PossessionsPlusPackageId) != null
                            || Type.GetType("PossessionsPlus.CompOwnedByPawn_Item, PossessionsPlus") != null;
            }
            catch
            {
                _ppCached = false;
            }

            return _ppCached.Value;
        }

        /// <summary>Call when settings UI opens or game starts — lock out RICS ownership if PP is loaded.</summary>
        public static void EnforceConflictRules(CAPGlobalChatSettings settings, bool notify = true)
        {
            if (settings == null)
                return;

            if (!IsPossessionsPlusActive())
                return;

            if (settings.UseRicsPawnOwnership)
                settings.UseRicsPawnOwnership = false;

            if (notify && !_notifiedThisSession)
            {
                _notifiedThisSession = true;
                try
                {
                    Messages.Message(
                        "RICS.Ownership.Conflict.PossessionsPlus".Translate(),
                        MessageTypeDefOf.CautionInput);
                }
                catch
                {
                    // Messages may be unavailable during early init
                }
            }
        }

        public static bool CanEnableRicsOwnership(CAPGlobalChatSettings settings)
        {
            if (IsPossessionsPlusActive())
                return false;
            return true;
        }
    }
}
