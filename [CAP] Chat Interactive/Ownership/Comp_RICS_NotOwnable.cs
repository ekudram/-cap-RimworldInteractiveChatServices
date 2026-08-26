// Comp_RICS_NotOwnable.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class Comp_RICS_NotOwnable : ThingComp
    {
        private bool notOwnable;

        public bool NotOwnable
        {
            get => notOwnable;
            set => notOwnable = value;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref notOwnable, "ricsNotOwnable", false);
        }

        public override string CompInspectStringExtra()
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive() || !notOwnable)
                return null;
            return "RICS.Ownership.Inspect.NotOwnable".Translate();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!RICS_OwnershipUtility.IsRicsOwnershipActive())
                yield break;

            var owned = parent?.TryGetComp<Comp_RICS_OwnedByPawn>();
            if (owned?.Owner != null)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = notOwnable
                    ? "RICS.Ownership.Gizmo.MarkOwnable".Translate()
                    : "RICS.Ownership.Gizmo.MarkNotOwnable".Translate(),
                defaultDesc = "RICS.Ownership.Gizmo.NotOwnableDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/ForbidOn", reportFailure: false) ?? TexCommand.ForbidOn,
                isActive = () => notOwnable,
                toggleAction = () =>
                {
                    notOwnable = !notOwnable;
                    if (notOwnable)
                        owned?.ClearOwner("marked not ownable");
                }
            };
        }
    }
}
