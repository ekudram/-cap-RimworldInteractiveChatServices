// Create new file: IncidentWorker_CallForAid.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    // Updated IncidentWorker_CallForAid.cs
    // Updated IncidentWorker_CallForAid.cs
    public class IncidentWorker_CallForAid : IncidentWorker_RaidFriendly
    {
        private bool _letterSent = false;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            bool baseCanFire = base.CanFireNowSub(parms);
            if (!baseCanFire)
            {
                Logger.Debug($"[MilitaryAid] Base CanFireNowSub returned false for map {parms.target}");
            }
            return true;
        }

        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            Map map = (Map)parms.target;

            // Try base method first
            if (base.TryResolveRaidFaction(parms) && parms.faction != null)
            {
                Logger.Debug($"[MilitaryAid] Base faction resolution successful: {parms.faction.Name}");
                return true;
            }

            Logger.Debug("[MilitaryAid] Base faction resolution failed, trying fallback...");

            var validFactions = Find.FactionManager.AllFactions
                .Where(f => !f.def.isPlayer &&
                           !f.def.hidden &&
                           !f.defeated &&
                           f.PlayerRelationKind >= FactionRelationKind.Neutral)
                .ToList();

            if (!validFactions.Any())
            {
                Logger.Debug("[MilitaryAid] No valid friendly factions found");
                return false;
            }

            parms.faction = validFactions.RandomElementByWeight(f => f.PlayerGoodwill + 100f);
            Logger.Debug($"[MilitaryAid] Selected fallback faction: {parms.faction.Name}");
            return true;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Logger.Debug($"[MilitaryAid] Executing military aid");

            // Prevent the base class from sending its standard letter
            // We'll handle the letter in our command handler
            _letterSent = false;

            bool success = base.TryExecuteWorker(parms);

            if (success)
            {
                Logger.Debug($"[MilitaryAid] Military aid executed successfully");
            }
            else
            {
                Logger.Debug($"[MilitaryAid] Military aid execution failed");
            }

            return success;
        }
    }
}