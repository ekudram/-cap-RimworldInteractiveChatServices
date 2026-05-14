// IncidentWorker_CallForAid.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive.
// 
// CAP Chat Interactive is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// CAP Chat Interactive is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with CAP Chat Interactive. If not, see <https://www.gnu.org/licenses/>.
//
// Custom IncidentWorker to handle friendly military aid raids with improved faction selection and logging.
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public class IncidentWorker_CallForAid : IncidentWorker_RaidFriendly
    {
        //private bool _letterSent = false;

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
            //_letterSent = false;

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