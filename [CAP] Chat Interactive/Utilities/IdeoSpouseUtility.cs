// IdeoSpouseUtility.cs
// Ideology spouse-count helpers for romance interaction gates.

using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Utilities
{
    public static class IdeoSpouseUtility
    {
        /// <summary>
        /// True if this pawn's ideo allows more than one spouse (MaxTwo / MaxThree / MaxFour / Unlimited).
        /// Default monogamy (no multi precept) returns false. No Ideology → false.
        /// </summary>
        public static bool AllowsMultipleSpouses(Pawn pawn)
        {
            if (pawn == null || !ModsConfig.IdeologyActive || pawn.Ideo == null)
                return false;

            var settings = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings;
            if (settings != null && !settings.RomanceRespectsSpouseCountIdeo)
                return false;

            try
            {
                foreach (Precept precept in pawn.Ideo.PreceptsListForReading)
                {
                    if (precept?.def == null) continue;
                    string dn = precept.def.defName ?? "";
                    if (!dn.StartsWith("SpouseCount_")) continue;

                    // Gender-specific precepts: SpouseCount_Male_MaxTwo, Female_Unlimited, etc.
                    if (dn.IndexOf("MaxTwo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("MaxThree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("MaxFour", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        dn.IndexOf("Unlimited", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.Debug($"IdeoSpouseUtility.AllowsMultipleSpouses: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Either side's culture allows multi-spouse romance ( freer additional partners ).
        /// </summary>
        public static bool CultureAllowsAdditionalPartners(Pawn a, Pawn b)
        {
            return AllowsMultipleSpouses(a) || AllowsMultipleSpouses(b);
        }
    }
}
