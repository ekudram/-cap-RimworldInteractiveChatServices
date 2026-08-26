// CompProperties_RICS_NotOwnable.cs
// Copyright (c) Captolamia
// Part of CAP Chat Interactive (RICS) — AGPLv3
using Verse;

namespace CAP_ChatInteractive.Ownership
{
    public class CompProperties_RICS_NotOwnable : CompProperties
    {
        public CompProperties_RICS_NotOwnable()
        {
            compClass = typeof(Comp_RICS_NotOwnable);
        }
    }
}
