// Comp_RICS_OwnedByPawn.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
//
// Null-safe pawn ownership on weapons/apparel. Avoids Possessions Plus failure modes:
// destroyed owners, destroyed maps, and inspect/gizmo spam when OffMap.
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class Comp_RICS_OwnedByPawn : ThingComp
    {
        private Pawn owner;
        public int OwnershipStartDay = -1;

        public Pawn Owner
        {
            get
            {
                // Anti-PP-bug: soft-clear destroyed / discarded references
                if (owner != null && (owner.Destroyed || owner.Discarded))
                    owner = null;
                return owner;
            }
            private set => owner = value;
        }

        private bool OffMap => parent?.MapHeld == null && parent?.Map == null;

        public bool HasLivingOwner
        {
            get
            {
                var o = Owner;
                return o != null && !o.Dead && !o.Destroyed;
            }
        }

        public void SetOwner(Pawn pawn, string reason = null)
        {
            if (parent == null || parent.Destroyed)
                return;

            if (RICS_OwnershipUtility.IsMarkedNotOwnable(parent) && pawn != null)
            {
                Messages.Message("RICS.Ownership.Reject.NotOwnable".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (pawn != null && pawn.Destroyed)
                return;

            Owner = pawn;
            if (Owner != null)
            {
                try
                {
                    Map map = Owner.MapHeld ?? Find.CurrentMap;
                    OwnershipStartDay = map != null ? GenLocalDate.DayOfYear(map) + 1 : -1;
                }
                catch
                {
                    OwnershipStartDay = -1;
                }
            }
            else
            {
                OwnershipStartDay = -1;
            }

            // MVP: suppress negative ownership-loss thoughts (always)
        }

        public void ClearOwner(string reason = null) => SetOwner(null, reason);

        public bool BlocksUseBy(Pawn pawn)
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return false;
            if (parent == null || parent.Destroyed)
                return false;
            // Anti-PP-bug: do not enforce on destroyed-map / null-map stragglers
            if (OffMap && parent.ParentHolder == null)
                return false;

            var o = Owner;
            if (o == null)
                return false;
            if (pawn == null)
                return true;
            if (o == pawn)
                return false;
            // Hostiles can loot
            try
            {
                if (pawn.HostileTo(Faction.OfPlayer))
                    return false;
            }
            catch { }

            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref owner, "ricsOwner");
            Scribe_Values.Look(ref OwnershipStartDay, "ricsOwnershipStartDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (owner != null && (owner.Destroyed || owner.Discarded))
                    owner = null;
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            // Anti-PP-bug: clear quietly — no tick spam when items vanish with maps
            owner = null;
            OwnershipStartDay = -1;
            base.PostDestroy(mode, previousMap);
        }

        public override string CompInspectStringExtra()
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                return null;
            var o = Owner;
            if (o == null)
                return null;
            return "RICS.Ownership.Inspect.OwnedBy".Translate(o.LabelShortCap);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                yield break;
            if (parent == null || parent.Destroyed)
                yield break;

            var o = Owner;

            Texture2D setIcon = ContentFinder<Texture2D>.Get("UI/Commands/ForbidOff", reportFailure: false)
                               ?? TexCommand.ForbidOff;
            yield return new Command_Action
            {
                defaultLabel = "RICS.Ownership.Gizmo.SetOwner".Translate(),
                defaultDesc = "RICS.Ownership.Gizmo.SetOwnerDesc".Translate(),
                icon = setIcon,
                action = () => Find.WindowStack.Add(new Dialog_RICS_AssignItemOwner(this))
            };

            if (o != null)
            {
                Texture2D clearIcon = ContentFinder<Texture2D>.Get("UI/Commands/Halt", reportFailure: false)
                                     ?? TexCommand.ForbidOn;
                yield return new Command_Action
                {
                    defaultLabel = "RICS.Ownership.Gizmo.ClearOwner".Translate(),
                    defaultDesc = "RICS.Ownership.Gizmo.ClearOwnerDesc".Translate(),
                    icon = clearIcon,
                    action = () =>
                    {
                        ClearOwner("RICS UI");
                        Messages.Message(
                            "RICS.Ownership.Cleared".Translate(parent.LabelNoCount),
                            MessageTypeDefOf.TaskCompletion,
                            historical: false);
                    }
                };
            }
        }
    }
}
