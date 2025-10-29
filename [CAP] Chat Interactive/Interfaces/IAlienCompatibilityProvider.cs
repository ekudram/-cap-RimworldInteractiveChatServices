using RimWorld;
using System.Collections.Generic;
using Verse;

namespace _CAP__Chat_Interactive.Interfaces
{
    public interface ICompatibilityProvider
    {
        string ModId { get;}
    }
    public interface IAlienCompatibilityProvider : ICompatibilityProvider
    {
        bool IsTraitForced(Pawn pawn, string defName, int degree);
        bool IsTraitDisallowed(Pawn pawn, string defName, int degree);
        bool IsTraitAllowed(Pawn pawn, TraitDef traitDef, int degree = -10);
        List<string> GetAllowedXenotypes(ThingDef raceDef);
        bool IsXenotypeAllowed(ThingDef raceDef, XenotypeDef xenotype);
    }
}

