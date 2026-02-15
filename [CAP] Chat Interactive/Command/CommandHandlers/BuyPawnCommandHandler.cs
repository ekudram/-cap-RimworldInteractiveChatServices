// BuyPawnCommadnHandler.cs
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
// Pawn purchase command handler
using _CAP__Chat_Interactive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class BuyPawnCommandHandler
    {
        private static string HandleBuyPawnCommandInternal(ChatMessageWrapper messageWrapper, string raceName, string xenotypeName = "Baseliner", string genderName = "Random", string ageString = "Random")
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(messageWrapper);
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
                // NOTE THIS WORKS NOW, returns any pawn ase
                Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                if (existingPawn != null)
                {
                    // return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn health to check on them.";
                    return "RICS.BPCH.AlreadyHasPawn".Translate(existingPawn.Name.ToStringFull);
                }
                // EXtra check and modified.  Redundant now but lets keep it for a bit.
                // SIMPLIFIED: Check if viewer already has a pawn assigned using direct dictionary lookup
                if (assignmentManager != null)
                {
                    // Get the platform identifier directly from the message
                    string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";

                    // Check if this platform ID exists in the assignments dictionary
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
                    {
                        existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        if (existingPawn != null && !existingPawn.Dead && existingPawn.Faction == Faction.OfPlayer)
                        {
                            // return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn to check on them.";
                            return "RICS.BPCH.AlreadyHasPawn".Translate(existingPawn.Name.ToStringFull);
                        }
                    }

                    // Also check for legacy username assignments as fallback (for older saves)
                    string usernameLower = messageWrapper.Username.ToLowerInvariant();
                    if (assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
                    {
                        existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                        if (existingPawn != null && !existingPawn.Dead && existingPawn.Spawned)
                        {
                            // return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn to check on them.";
                            return "RICS.BPCH.AlreadyHasPawn".Translate(existingPawn.Name.ToStringFull);
                        }
                    }
                }

                // Initialize raceSettings to null
                RaceSettings raceSettings = null;

                // Validate the pawn request FIRST to get raceSettings
                if (!IsValidPawnRequest(raceName, xenotypeName, out raceSettings))
                {
                    // Provide specific error messages
                    if (raceSettings == null)
                        // return $"Race '{raceName}' not found or not humanlike.";
                        return "RICS.BPCH.RaceNotFound".Translate(raceName);

                    if (!raceSettings.Enabled)
                        // return $"Race '{raceName}' is disabled for purchase.";
                        return "RICS.BPCH.RaceDisabled".Translate(raceName);

                    // return $"Invalid pawn request for {raceName}.";
                    return "RICS.BPCH.InvalidRaceRequest".Translate(raceName);  
                }

                // NOW parse age with the validated raceSettings
                int age = ParseAge(ageString, raceSettings);

                // Validate age against race settings
                if (age < raceSettings.MinAge || age > raceSettings.MaxAge)
                {
                    // return $"Age must be between {raceSettings.MinAge} and {raceSettings.MaxAge} for {raceName}.";
                    return "RICS.BPCH.AgeOutOfRange".Translate(raceName, raceSettings.MinAge, raceSettings.MaxAge);
                }

                // Validate xenotype if applicable - FIXED: Added null check for raceSettings
                if (!string.IsNullOrEmpty(xenotypeName) && xenotypeName != "Baseliner" && ModsConfig.BiotechActive && raceSettings != null)
                {
                    // Check if xenotype is enabled (using your new EnabledXenotypes dictionary)
                    if (raceSettings.EnabledXenotypes != null &&
                        raceSettings.EnabledXenotypes.ContainsKey(xenotypeName) &&
                        !raceSettings.EnabledXenotypes[xenotypeName])
                    {
                        // return $"Xenotype '{xenotypeName}' is disabled for {raceName}.";
                        return "RICS.BPCH.XenotypeDisabled".Translate(xenotypeName, raceName);
                    }

                    // Or if you want to check if it's allowed at all
                    if (!raceSettings.AllowCustomXenotypes && xenotypeName != "Baseliner")
                    {
                        // return $"Custom xenotypes are not allowed for {raceName}.";
                        return "RICS.BPCH.CustomXenotypesDisabled".Translate(raceName);
                    }
                }

                // Calculate final price with xenotype price (not multiplier)
                int basePrice = raceSettings.BasePrice;
                float xenotypePrice = GetXenotypePrice(raceSettings, xenotypeName);
                int finalPrice = (int)(basePrice + xenotypePrice);

                // Check if viewer can afford
                if (viewer.Coins < finalPrice)
                {
                    // return $"You need {finalPrice}{currencySymbol}
                    // to purchase a {raceName} pawn! You have {viewer.Coins}{currencySymbol}.";
                    return "RICS.BPCH.InsufficientFunds"
                        .Translate(finalPrice, currencySymbol, raceName, viewer.Coins, currencySymbol);
                }

                if (!IsGameReadyForPawnPurchase())
                {
                    // return "Game not ready for pawn purchase (no colony, in menu, etc.)";
                    return "RICS.BPCH.GameNotReady".Translate();
                }

                var result = GenerateAndSpawnPawn(messageWrapper.Username, raceName, xenotypeName, genderName, age, raceSettings);

                // In HandleBuyPawnCommandInternal, update the success block:

                if (result.Success)
                {
                    // Deduct coins and update karma
                    viewer.TakeCoins(finalPrice);
                    viewer.GiveKarma(CalculateKarmaChange(finalPrice));

                    // Save pawn assignment to viewer
                    if (result.Pawn != null && assignmentManager != null)
                    {
                        assignmentManager.AssignPawnToViewer(messageWrapper, result.Pawn);
                    }

                    // Get location info AFTER spawn
                    string locationInfo = "";
                    if (result.Pawn != null && result.Pawn.Spawned && result.Pawn.Map != null)
                    {
                        IntVec3 pos = result.Pawn.Position;
                        string mapName = result.Pawn.Map.Parent.LabelCap ?? "Home Map";
                        locationInfo = $"RICS.BPCH.Letter.Delivery".Translate(pos.x, pos.z, mapName);  // Note: y is usually 0
                    }
                    else
                    {
                        locationInfo = "RICS.BPCH.Letter.Delivery.Unknown".Translate();
                    }

                    // Send notification
                    string xenotypeInfo = xenotypeName != "Baseliner" ? $" ({xenotypeName})" : "";
                    string ageInfo = ageString != "Random" ? $", Age: {age}" : "";

                    // Send gold letter for pawn purchases (always considered major)
                    string goldLetterTitle = "RICS.BPCH.Letter.Title".Translate(raceName);
                    string goldLetterText = "RICS.BPCH.Letter.Text".Translate(
                        messageWrapper.Username,
                        raceName,
                        xenotypeInfo,
                        age.ToString(),
                        finalPrice.ToString("N0"),
                        currencySymbol,
                        result.Pawn?.Name.ToStringFull ?? "Unnamed",
                        locationInfo  // New {7} placeholder
                    );

                    // Pass the pawn as look target so clicking the letter jumps to it
                    MessageHandler.SendGoldLetter(goldLetterTitle, goldLetterText, new LookTargets(result.Pawn));

                    // Return success message with more details
                    return "RICS.BPCH.PurchaseSuccess".Translate(
                        raceName,
                        xenotypeInfo,
                        finalPrice,
                        currencySymbol,
                        result.Pawn?.Name.ToStringFull ?? "your new pawn"
                    ) + $" {locationInfo}";  // Optional: add location to chat response too
                }
                else
                {
                    return $"Failed to purchase pawn: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling buy pawn command: {ex}");
                return "Error purchasing pawn. Check log and copy stack trace for developer!";
            }
        }

        private static BuyPawnResult GenerateAndSpawnPawn(string username, string raceName, string xenotypeName, string genderName, int age, RaceSettings raceSettings)
        {
            try
            {
                var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();
                if (!playerMaps.Any())
                {
                    // return new BuyPawnResult(false, "No player home maps found.");
                    return new BuyPawnResult(false, "RICS.BPCH.NoHomeMap".Translate()); 
                }

                var map = playerMaps.First();

                // Get pawn kind def for the race
                var pawnKindDef = GetPawnKindDefForRace(raceName);
                if (pawnKindDef == null)
                {
                    // return new BuyPawnResult(false, $"Could not find pawn kind for race: {raceName}");
                    return new BuyPawnResult(false, "RICS.BPCH.PawnKindNotFound".Translate(raceName));
                }

                // Get xenotype def if specified and Biotech is active
                XenotypeDef xenotypeDef = null;
                if (ModsConfig.BiotechActive && xenotypeName != "Baseliner")
                {
                    xenotypeDef = DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(
                        x => x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase) ||
                             x.label.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

                    if (xenotypeDef == null)
                    {
                        Logger.Warning($"Custom xenotype '{xenotypeName}' detected. Custom xenotype support may vary.");
                    }
                }

                var raceDef = RaceUtils.FindRaceByName(raceName);
                if (raceDef == null)
                {
                    // return new BuyPawnResult(false, $"Race '{raceName}' not found.");
                    return new BuyPawnResult(false, "RICS.BPCH.RaceDefNotFound".Translate(raceName));
                }

                // Validate gender against race restrictions
                //var raceSettings = RaceSettingsManager.GetRaceSettings(raceDef.defName);
                if (raceSettings != null)
                {
                    var requestedGender = ParseGender(genderName);
                    if (requestedGender.HasValue && !IsGenderAllowed(raceSettings.AllowedGenders, requestedGender.Value))
                    {
                        string allowedText = GetAllowedGendersDescription(raceSettings.AllowedGenders);
                        //return new BuyPawnResult(false,
                        //    $"The {raceName} race allows {allowedText}. Please choose a different gender or use 'random'.");
                        return new BuyPawnResult(false,
                            "RICS.BPCH.GenderNotAllowed".Translate(raceName, allowedText)); 
                    }
                }

                // Prepare generation request with specific age and xenotype
                var request = new PawnGenerationRequest(
                    kind: pawnKindDef,
                    faction: Faction.OfPlayer,
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: true,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: true,
                    allowGay: true,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    fixedBiologicalAge: age, // Use the validated age
                    fixedChronologicalAge: null,
                    fixedGender: ParseGender(genderName),
                    fixedLastName: null,
                    forcedXenotype: xenotypeDef // Set the forced xenotype here
                );

                // Generate pawn
                Pawn pawn = PawnGenerator.GeneratePawn(request);

                // Set custom name
                if (pawn.Name is NameTriple nameTriple)
                {
                    pawn.Name = new NameTriple(nameTriple.First, username, nameTriple.Last);
                }
                else
                {
                    pawn.Name = new NameSingle(username);
                }

                // Use improved pawn spawning that works for space biomes
                if (!TrySpawnPawnInSpaceBiome(pawn, map))
                {
                    // return new BuyPawnResult(false, "Could not find valid spawn location for pawn.");
                    return new BuyPawnResult(false, "RICS.BPCH.SpawnLocationNotFound".Translate());
                }

                // Send letter notification we do this when we reture
                // TaggedString letterTitle = $"{username} Joins Colony";
                // TaggedString letterText = $"{username} has purchased a {raceName} pawn and joined the colony!";
                // PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref letterText, ref letterTitle, pawn);

                // Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, pawn);

                // return new BuyPawnResult(true, "Pawn purchased successfully!", pawn);
                return new BuyPawnResult(true, "RICS.BPCH.PawnGenerated".Translate(), pawn);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating pawn: {ex}");
                // return new BuyPawnResult(false, $"Generation error: {ex.Message}");
                return new BuyPawnResult(false, "RICS.BPCH.GenerationError".Translate(ex.Message)); 
            }
        }

        public static PawnKindDef GetPawnKindDefForRace(string raceName)
        {

            // Use centralized race lookup
            var raceDef = RaceUtils.FindRaceByName(raceName);
            
            if (raceDef == null)
            {
                // Logger.Warning($"Race not found: {raceName}");
                Logger.Warning("RICS.BPCH.Debug.RaceNotFound".Translate(raceName));
                return PawnKindDefOf.Colonist;
            }

            Logger.Debug($"Looking for pawn kind def for race: {raceDef.defName}");

            // Strategy 1: Look for player faction pawn kinds for this race
            var playerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef)
                .Where(pk => IsPlayerFactionPawnKind(pk))
                .ToList();

            if (playerPawnKinds.Any())
            {
                var bestMatch = playerPawnKinds.FirstOrDefault(pk => pk.defName.Contains("Colonist") || pk.defName.Contains("Player"));
                if (bestMatch != null)
                {
                    Logger.Debug($"Found player faction pawn kind: {bestMatch.defName}");
                    return bestMatch;
                }

                Logger.Debug($"Using first player faction pawn kind: {playerPawnKinds[0].defName}");
                return playerPawnKinds[0];
            }

            // Strategy 2: Look for any pawn kind with this race that has isPlayer=true in its faction
            var factionPlayerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef && pk.defaultFactionDef != null)
                .Where(pk => pk.defaultFactionDef.isPlayer)
                .ToList();

            if (factionPlayerPawnKinds.Any())
            {
                Logger.Debug($"Found pawn kind with player faction: {factionPlayerPawnKinds[0].defName}");
                return factionPlayerPawnKinds[0];
            }

            // Strategy 3: Look for pawn kinds with player-like names
            var namedPlayerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef)
                .Where(pk => IsLikelyPlayerPawnKind(pk))
                .ToList();

            if (namedPlayerPawnKinds.Any())
            {
                Logger.Debug($"Found likely player pawn kind: {namedPlayerPawnKinds[0].defName}");
                return namedPlayerPawnKinds[0];
            }

            // Strategy 4: Fallback to any pawn kind for this race
            var anyPawnKind = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(pk => pk.race == raceDef);
            if (anyPawnKind != null)
            {
                Logger.Debug($"Using fallback pawn kind: {anyPawnKind.defName}");
                return anyPawnKind;
            }

            // Final fallback
            // Logger.Warning($"No pawn kind found for race: {raceDef.defName}, using default Colonist");
            Logger.Warning("RICS.BPCH.Debug.NoPawnKindFound".Translate(raceDef.defName));   
            return PawnKindDefOf.Colonist;
        }

        private static bool IsPlayerFactionPawnKind(PawnKindDef pawnKind)
        {
            if (pawnKind == null) return false;

            // Check if it uses PlayerColony faction (core RimWorld)
            if (pawnKind.defaultFactionDef == FactionDefOf.PlayerColony)
                return true;

            // Check if the faction def has isPlayer = true
            if (pawnKind.defaultFactionDef?.isPlayer == true)
                return true;

            // Check for player colony faction in the defName
            if (pawnKind.defaultFactionDef?.defName?.ToLower().Contains("player") == true ||
                pawnKind.defaultFactionDef?.defName?.ToLower().Contains("colony") == true)
                return true;

            return false;
        }

        private static bool IsLikelyPlayerPawnKind(PawnKindDef pawnKind)
        {
            if (pawnKind == null) return false;

            string defNameLower = pawnKind.defName.ToLower();

            // Look for player/colonist naming patterns
            var playerKeywords = new[] { "colonist", "player", "settler", "civilian", "neutral" };
            if (playerKeywords.Any(keyword => defNameLower.Contains(keyword)))
                return true;

            // Exclude obviously hostile/non-player pawn kinds
            var hostileKeywords = new[] { "raider", "pirate", "savage", "hostile", "enemy", "animal", "wild" };
            if (hostileKeywords.Any(keyword => defNameLower.Contains(keyword)))
                return false;

            // Check if it has low combat power (typical for colonists)
            if (pawnKind.combatPower > 0 && pawnKind.combatPower < 100)
                return true;

            return false;
        }

        private static bool IsValidPawnRequest(string raceDefName, string xenotypeName, out RaceSettings raceSettings)
        {
            raceSettings = null;

            // Use centralized race lookup
            var raceDef = RaceUtils.FindRaceByName(raceDefName);
            if (raceDef == null)
            {
                Logger.Warning($"Race not found: {raceDefName}");
                return false;
            }

            // Get race settings - this will never return null now
            raceSettings = RaceSettingsManager.GetRaceSettings(raceDef.defName);

            // Check if race is enabled using centralized logic
            if (!raceSettings.Enabled)
            {
                Logger.Debug($"Race disabled: {raceDef.defName}");
                return false;
            }

            // Check xenotype if specified and Biotech is active
            if (!string.IsNullOrEmpty(xenotypeName) && xenotypeName != "Baseliner" && ModsConfig.BiotechActive)
            {
                // Check if xenotype is allowed for this race
                if (!IsXenotypeAllowed(raceSettings, xenotypeName))
                {
                    Logger.Debug($"Xenotype not allowed: {xenotypeName} for race {raceDef.defName}");
                    return false;
                }
            }

            return true;
        }

        private static bool IsXenotypeAllowed(RaceSettings raceSettings, string xenotypeName)
        {
            Logger.Debug($"Checking xenotype '{xenotypeName}' for race settings:");
            Logger.Debug($"  EnabledXenotypes count: {raceSettings.EnabledXenotypes?.Count ?? 0}");
            Logger.Debug($"  AllowCustomXenotypes: {raceSettings.AllowCustomXenotypes}");

            if (raceSettings.EnabledXenotypes != null)
            {
                foreach (var kvp in raceSettings.EnabledXenotypes)
                {
                    Logger.Debug($"    {kvp.Key} = {kvp.Value}");
                }

                // Check exact match first
                if (raceSettings.EnabledXenotypes.ContainsKey(xenotypeName))
                {
                    bool result = raceSettings.EnabledXenotypes[xenotypeName];
                    Logger.Debug($"  Exact match found: {result}");
                    return result;
                }

                // Check case insensitive match
                var caseInsensitiveMatch = raceSettings.EnabledXenotypes.Keys
                    .FirstOrDefault(k => k.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase));

                if (caseInsensitiveMatch != null)
                {
                    bool result = raceSettings.EnabledXenotypes[caseInsensitiveMatch];
                    Logger.Debug($"  Case-insensitive match '{caseInsensitiveMatch}': {result}");
                    return result;
                }
            }

            Logger.Debug($"  No match found, using default logic");

            // Rest of the existing logic...
            // Ensure dictionaries are initialized
            if (raceSettings.EnabledXenotypes == null)
                raceSettings.EnabledXenotypes = new Dictionary<string, bool>();
            if (raceSettings.XenotypePrices == null)
                raceSettings.XenotypePrices = new Dictionary<string, float>();

            // If we have specific xenotype settings, only allow enabled ones
            if (raceSettings.EnabledXenotypes.Count > 0)
            {
                // If xenotype isn't in the enabled list, check if it's a custom xenotype
                if (!raceSettings.EnabledXenotypes.ContainsKey(xenotypeName))
                {
                    // If it's a custom xenotype, check if custom xenotypes are allowed
                    if (IsCustomXenotype(xenotypeName))
                        return raceSettings.AllowCustomXenotypes;

                    // If it's a base game xenotype but not in the list, it's not allowed
                    return false;
                }

                // Xenotype is in the list and enabled
                return raceSettings.EnabledXenotypes[xenotypeName];
            }

            // No specific xenotype restrictions - check if custom xenotypes are allowed
            if (IsCustomXenotype(xenotypeName) && !raceSettings.AllowCustomXenotypes)
                return false;

            return true;
        }

        private static bool IsCustomXenotype(string xenotypeName)
        {
            // Check if this is a custom xenotype (not in the base game DefDatabase)
            return DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(xenotypeName, StringComparison.OrdinalIgnoreCase)) == null;
        }

        private static float GetXenotypePrice(RaceSettings raceSettings, string xenotypeName)
        {
            if (xenotypeName == "Baseliner" || string.IsNullOrEmpty(xenotypeName))
                return 0f; // Baseliner adds no extra cost

            // Ensure dictionary is initialized
            if (raceSettings.XenotypePrices == null)
            {
                raceSettings.XenotypePrices = new Dictionary<string, float>();
                return 0f; // No price defined yet
            }

            // Try to get the price from settings
            if (raceSettings.XenotypePrices.TryGetValue(xenotypeName, out float price))
            {
                return price;
            }

            // If not found in settings, return 0 and log a warning
            Logger.Warning($"No price found for xenotype '{xenotypeName}', using 0 as default");
            return 0f;
        }

        private static bool IsGameReadyForPawnPurchase()
        {
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   Current.Game.Maps.Any(map => map.IsPlayerHome);
        }

        private static int CalculateKarmaChange(int price)
        {
            return (int)(price / 1000f * 2); // Scale karma with price
        }

        private static int ParseAge(string ageString, RaceSettings raceSettings)
        {
            if (ageString.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                // Generate random age within the race settings range
                return Rand.Range(raceSettings.MinAge, raceSettings.MaxAge + 1);
            }

            if (int.TryParse(ageString, out int age))
            {
                // Clamp the age to the race settings range
                return Math.Max(raceSettings.MinAge, Math.Min(raceSettings.MaxAge, age));
            }

            // Fallback to random age if parsing fails
            return Rand.Range(raceSettings.MinAge, raceSettings.MaxAge + 1);
        }

        private static Pawn FindPawnByThingId(string thingId)
        {
            if (string.IsNullOrEmpty(thingId))
                return null;

            // Search all maps for the pawn
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.ThingID == thingId)
                        return pawn;
                }
            }

            // Also check world pawns (in caravan, etc.)
            var worldPawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.ThingID == thingId);
            if (worldPawn != null)
                return worldPawn;

            return null;
        }

        private static Gender? ParseGender(string genderName)
        {
            if (string.IsNullOrEmpty(genderName)) return null;

            return genderName.ToLowerInvariant() switch
            {
                "male" or "m" => Gender.Male,
                "female" or "f" => Gender.Female,
                _ => null // Random gender
            };
        }

        // Helper methods

        private static bool IsGenderAllowed(AllowedGenders allowedGenders, Gender gender)
        {
            return gender switch
            {
                Gender.Male => allowedGenders.AllowMale,
                Gender.Female => allowedGenders.AllowFemale,
                Gender.None => allowedGenders.AllowOther,
                _ => true
            };
        }

        private static string GetAllowedGendersDescription(AllowedGenders allowedGenders)
        {
            if (!allowedGenders.AllowMale && !allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                // return "no genders (custom race)";
                return "RICS.BPCH.Gender.None".Translate();

            if (allowedGenders.AllowMale && !allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                // return "only male";
                return "RICS.BPCH.Gender.OnlyMale".Translate();

            if (!allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                // return "only female";
                return "RICS.BPCH.Gender.OnlyFemale".Translate();   

            if (allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                // return "male or female only (no other)";
                return "RICS.BPCH.Gender.MaleOrFemale".Translate();

            // return "any gender";
            return "RICS.BPCH.Gender.Any".Translate();
        }

        private static bool TrySpawnPawnInSpaceBiome(Pawn pawn, Map map)
        {
            try
            {
                Logger.Debug($"Attempting to spawn pawn in biome: {map.Biome.defName}");

                // Strategy 1: Try standard edge spawning (works for ground maps)
                if (CellFinder.TryFindRandomEdgeCellWith(
                    c => map.reachability.CanReachColony(c) && !c.Fogged(map),
                    map,
                    CellFinder.EdgeRoadChance_Neutral,
                    out IntVec3 spawnLoc))
                {
                    GenSpawn.Spawn(pawn, spawnLoc, map, WipeMode.Vanish);
                    Logger.Debug($"Spawned pawn at edge cell: {spawnLoc}");
                    return true;
                }

                // Strategy 2: For space biomes or when edge spawning fails, try near existing colonists
                var existingColonist = map.mapPawns.FreeColonists.FirstOrDefault();
                if (existingColonist != null)
                {
                    if (CellFinder.TryFindRandomCellNear(existingColonist.Position, map, 8,
                        c => c.Standable(map) && !c.Fogged(map) && c.Walkable(map),
                        out IntVec3 nearColonistPos))
                    {
                        GenSpawn.Spawn(pawn, nearColonistPos, map, WipeMode.Vanish);
                        Logger.Debug($"Spawned pawn near colonist: {nearColonistPos}");
                        return true;
                    }
                }

                // Strategy 3: Try any valid cell in the player's base area
                if (map.areaManager.Home.ActiveCells != null)
                {
                    var homeCells = map.areaManager.Home.ActiveCells.Where(c =>
                        c.Standable(map) && !c.Fogged(map)).ToList();

                    if (homeCells.Count > 0)
                    {
                        IntVec3 homePos = homeCells.RandomElement();
                        GenSpawn.Spawn(pawn, homePos, map, WipeMode.Vanish);
                        Logger.Debug($"Spawned pawn in home area: {homePos}");
                        return true;
                    }
                }

                // Strategy 4: Use drop pod delivery as last resort
                Logger.Debug("Attempting drop pod delivery as fallback...");
                return TryDropPodDelivery(pawn, map);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TrySpawnPawnInSpaceBiome: {ex}");
                return false;
            }
        }

        private static bool TryDropPodDelivery(Pawn pawn, Map map)
        {
            try
            {
                // Find a safe drop position
                IntVec3 dropPos;
                if (DropCellFinder.TryFindDropSpotNear(map.Center, map, out dropPos,
                    allowFogged: false, canRoofPunch: true, maxRadius: 20))
                {
                    // Use RimWorld's built-in drop pod utility - much simpler!
                    List<Thing> thingsToDeliver = new List<Thing> { pawn };

                    DropPodUtility.DropThingsNear(
                        dropPos,
                        map,
                        thingsToDeliver,
                        openDelay: 110,
                        leaveSlag: false,
                        canRoofPunch: true,
                        forbid: true,
                        allowFogged: false
                    );

                    Logger.Debug($"Delivered pawn via drop pod at: {dropPos}");
                    return true;
                }

                Logger.Error("Could not find valid drop position for pawn delivery");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in drop pod delivery: {ex}");
                return false;
            }
        }


        // New method to handle the command with argument parsing
        public static string HandleBuyPawnCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                // Parse arguments
                ParsePawnParameters(args, out string raceName, out string xenotypeName, out string genderName, out string ageString);

                Logger.Debug($"Parsed - Race: {raceName}, Xenotype: {xenotypeName}, Gender: {genderName}, Age: {ageString}");

                // Validate that we have at least a race name
                if (string.IsNullOrEmpty(raceName))
                {
                    // return "You must specify a race. Usage: !pawn [race] [xenotype] [gender] [age]";
                    return "RICS.BPCH.Usage".Translate();   
                }

                // Check if the race exists - try to find it
                var raceDef = RaceUtils.FindRaceByName(raceName);

                if (raceDef == null)
                {
                    // Try to find similar races for better error messages
                    var allRaces = RaceUtils.GetAllHumanlikeRaces();
                    var similarRaces = allRaces
                        .Where(r => r.defName.IndexOf(raceName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.label.IndexOf(raceName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(r => r.label)
                        .Take(3)
                        .ToList();

                    string errorMessage = "RICS.BPCH.RaceNotFound".Translate(raceName);

                    if (similarRaces.Any())
                    {
                        errorMessage += " " + "RICS.BPCH.RaceNotFound.Suggestion".Translate(string.Join(", ", similarRaces));
                    }
                    else
                    {
                        errorMessage += " " + "RICS.BPCH.RaceNotFound.UseList".Translate();
                    }

                    return errorMessage;
                }

                // Call the existing handler with parsed parameters
                return HandleBuyPawnCommandInternal(messageWrapper, raceName, xenotypeName, genderName, ageString);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing pawn command: {ex}");
                return "Error parsing command. Usage: !pawn [race] [xenotype] [gender] [age]";
            }
        }

        // Smart parameter parsing with multi-word race support
        private static void ParsePawnParameters(string[] args, out string raceName, out string xenotypeName, out string genderName, out string ageString)
        {
            // Initialize defaults
            raceName = "";
            xenotypeName = "Baseliner";
            genderName = "Random";
            ageString = "Random";

            if (args.Length == 0) return;

            // Track which arguments have been used
            var usedArgs = new bool[args.Length];

            // STEP 1: Identify AGE (numbers)
            for (int i = 0; i < args.Length; i++)
            {
                if (usedArgs[i]) continue;

                if (int.TryParse(args[i], out int age) && age > 0 && age <= 150)
                {
                    ageString = args[i];
                    usedArgs[i] = true;
                    break; // Only one age parameter expected
                }
            }

            // STEP 2: Identify GENDER (limited values)
            for (int i = 0; i < args.Length; i++)
            {
                if (usedArgs[i]) continue;

                string argLower = args[i].ToLower();
                if (argLower == "male" || argLower == "female" ||
                    argLower == "m" || argLower == "f")
                {
                    genderName = args[i]; // Keep original case
                    usedArgs[i] = true;
                    break; // Only one gender parameter expected
                }
            }

            // STEP 3: Identify XENOTYPE (check against known xenotypes)
            var knownXenotypes = GetKnownXenotypes();
            for (int i = 0; i < args.Length; i++)
            {
                if (usedArgs[i]) continue;

                string arg = args[i];

                // Check if this argument matches a known xenotype
                if (knownXenotypes.Contains(arg, StringComparer.OrdinalIgnoreCase))
                {
                    xenotypeName = arg;
                    usedArgs[i] = true;
                    break; // Only one xenotype parameter expected (except Baseliner)
                }
            }

            // STEP 4: RACE is what's left (can be multi-word)
            // Collect all unused arguments as potential race name parts
            var raceParts = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!usedArgs[i])
                {
                    raceParts.Add(args[i]);
                }
            }

            if (raceParts.Count == 0)
            {
                // No race specified - this will be caught by validation
                return;
            }

            // Now try to find the best matching race from the remaining parts
            raceName = FindBestRaceMatch(raceParts.ToArray());

            // If no exact match found, join all parts as the race name
            if (string.IsNullOrEmpty(raceName))
            {
                raceName = string.Join(" ", raceParts);
            }
        }

        // Helper method to get all known xenotypes for validation
        private static HashSet<string> GetKnownXenotypes()
        {
            var xenotypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Baseliner" // Always include Baseliner
    };

            if (ModsConfig.BiotechActive)
            {
                // Add all xenotypes from the game
                foreach (var xenotypeDef in DefDatabase<XenotypeDef>.AllDefs)
                {
                    if (!string.IsNullOrEmpty(xenotypeDef.defName))
                    {
                        xenotypes.Add(xenotypeDef.defName);
                    }
                }
            }

            return xenotypes;
        }

        // Helper method to find the best race match from remaining arguments
        private static string FindBestRaceMatch(string[] potentialRaceArgs)
        {
            if (potentialRaceArgs.Length == 0) return string.Empty;

            // Get all known race names (defName and label)
            var knownRaces = RaceUtils.GetAllHumanlikeRaces()
                .SelectMany(r => new[] { r.defName, r.label })
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .ToList();

            // Try from longest combination to shortest
            for (int wordCount = Math.Min(3, potentialRaceArgs.Length); wordCount >= 1; wordCount--)
            {
                for (int startIndex = 0; startIndex <= potentialRaceArgs.Length - wordCount; startIndex++)
                {
                    // Create a candidate race name from consecutive words
                    var candidateParts = potentialRaceArgs
                        .Skip(startIndex)
                        .Take(wordCount)
                        .ToArray();

                    string candidateRace = string.Join(" ", candidateParts);

                    // Check for exact match
                    var exactMatch = knownRaces.FirstOrDefault(r =>
                        r.Equals(candidateRace, StringComparison.OrdinalIgnoreCase));

                    if (exactMatch != null)
                    {
                        // Mark these parts as used (optional - for debugging)
                        return exactMatch;
                    }
                }
            }

            // No match found - return the first word as fallback
            return potentialRaceArgs[0];
        }
        // List methods
        public static string ListAvailableRaces()
        {
            var availableRaces = RaceUtils.GetEnabledRaces();

            if (availableRaces.Count == 0)
            {
                // return "No races available for purchase.";
                return "RICS.BPCH.NoRacesAvailable".Translate();
            }

            // Also show how many total races exist for context
            var allRaces = RaceUtils.GetAllHumanlikeRaces();

            var raceSettings = RaceSettingsManager.RaceSettings;
            if (raceSettings.Count == 0)
            {
                Logger.Debug("No race settings found, initializing defaults...");
                // This will trigger the initialization in Dialog_PawnSettings.LoadRaceSettings
                JsonFileManager.LoadRaceSettings();
                // Just creating the dialog will initialize the settings
            }

            var raceList = availableRaces.Select(r =>
            {
                var inSettings = raceSettings.ContainsKey(r.defName);
                var settings = inSettings ? raceSettings[r.defName] : null;
                return $"{r.LabelCap.RawText}{(inSettings ? "" : " " + "RICS.BPCH.RacesList.New".Translate())}";
            });

            // string result = $"Available races ({availableRaces.Count} of {allRaces.Count()} total): {string.Join(", ", raceList.Take(8))}";
            string result = "RICS.BPCH.RacesList".Translate(availableRaces.Count, allRaces.Count()) +
                ": " + string.Join(", ", raceList.Take(8));
            if (availableRaces.Count > 8)
                // result += $" (and {availableRaces.Count - 8} more...)";
                result += " " + "RICS.BPCH.RacesList.More".Translate(availableRaces.Count - 8);

            // removed extra info
            //if (availableRaces.Count < allRaces.Count())
            //    result += $"\n{allRaces.Count() - availableRaces.Count} races are disabled in settings";

            return result;
        }

        public static string ListAvailableXenotypes(string raceName = null)
        {
            if (!ModsConfig.BiotechActive)
            {
                // return "Biotech DLC not active - only baseliners available.";
                return "RICS.BPCH.BiotechNotActive".Translate();
            }

            try
            {
                // If a race is specified, show xenotypes available for that race
                if (!string.IsNullOrEmpty(raceName))
                {
                    var raceDef = RaceUtils.FindRaceByName(raceName);
                    if (raceDef != null)
                    {
                        var raceSettings = JsonFileManager.GetRaceSettings(raceDef.defName);
                        var allowedXenotypes = GetAllowedXenotypesForRace(raceDef);

                        if (allowedXenotypes.Any())
                        {
                            var enabledXenotypes = allowedXenotypes
                                .Where(x => !raceSettings.EnabledXenotypes.ContainsKey(x) || raceSettings.EnabledXenotypes[x])
                                .OrderBy(x => x)
                                .Take(12)
                                .ToList();

                            //return $"Xenotypes available for {raceDef.LabelCap}: {string.Join(", ", enabledXenotypes)}" +
                            //       (allowedXenotypes.Count > 12 ? $" (... {allowedXenotypes.Count - 12} more)" : "");

                            var resultRace = "RICS.BPCH.XenotypesForRace".Translate(
                                raceDef.LabelCap,
                                string.Join(", ", enabledXenotypes)
                            );

                            if (allowedXenotypes.Count > 12)
                            {
                                resultRace += " " + "RICS.BPCH.XenotypesForRace.More".Translate(allowedXenotypes.Count - 12);
                            }

                            return resultRace;
                        }
                    }
                }

                // General xenotype list (fallback)
                var allXenotypes = DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => x != XenotypeDefOf.Baseliner &&
                               !string.IsNullOrEmpty(x.defName) &&
                               x.inheritable) // Only inheritable xenotypes (most player-facing ones)
                    .Select(x => x.defName)
                    .OrderBy(x => x)
                    .Take(15)
                    .ToList();

                if (!allXenotypes.Any())
                {
                    // return "No xenotypes found (except Baseliner).";
                    return "RICS.BPCH.NoXenotypesFound".Translate();
                }

                // return $"Common xenotypes: {string.Join(", ", allXenotypes)}" + (allXenotypes.Count >= 15 ? " (and many more - try !pawn <race> to see race-specific xenotypes)" : "");
                var result = "RICS.BPCH.CommonXenotypes".Translate(string.Join(", ", allXenotypes));

                if (allXenotypes.Count >= 15)
                {
                    result += " " + "RICS.BPCH.CommonXenotypes.More".Translate();
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing xenotypes: {ex}");
                // return "Error retrieving xenotype list. You can still use custom xenotype names.";
                return "RICS.BPCH.XenotypeListError".Translate();
            }
        }

        private static List<string> GetAllowedXenotypesForRace(ThingDef raceDef)
        {
            // Use the same logic as in Dialog_PawnSettings
            if (CAPChatInteractiveMod.Instance?.AlienProvider != null)
            {
                return CAPChatInteractiveMod.Instance.AlienProvider.GetAllowedXenotypes(raceDef);
            }

            // Fallback: return all xenotypes if no restrictions
            if (ModsConfig.BiotechActive)
            {
                return DefDatabase<XenotypeDef>.AllDefs
                    .Where(x => x != XenotypeDefOf.Baseliner)
                    .Select(x => x.defName)
                    .ToList();
            }

            return new List<string>();
        }

        // MyPawn command
        public static string HandleMyPawnCommand(ChatMessageWrapper messageWrapper)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

            // UPDATED: Use platform ID-based lookup
            var pawn = assignmentManager?.GetAssignedPawn(messageWrapper);

            if (pawn != null)  // Found assigned pawn even pawn.Dead 
            {
                // string status = pawn.Spawned ? "alive and in colony" : "alive but not in colony";
                string status = pawn.Spawned ? "RICS.BPCH.MyPawn.Status.AliveAndInColony".Translate() : "RICS.BPCH.MyPawn.Status.AliveNotInColony".Translate();
                string health = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                int traitCount = pawn.story?.traits?.allTraits?.Count ?? 0;
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;

                // return $"Your pawn {pawn.Name} is {status}. Health: {health}, Age: {pawn.ageTracker.AgeBiologicalYears}, Traits: {traitCount}/{maxTraits}";

                return "RICS.BPCH.MyPawn.HasPawn".Translate(
                    pawn.Name.ToString(), // Convert Name to string
                    status,
                    health,
                    pawn.ageTracker.AgeBiologicalYears.ToString(), // Convert int to string
                    traitCount.ToString(), // Convert int to string
                    maxTraits.ToString() // Convert int to string
                );
            }
            else
            {
                // return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
                return "RICS.BPCH.MyPawn.NoPawn".Translate();
            }
        }
    }

    public class BuyPawnResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Pawn Pawn { get; }

        public BuyPawnResult(bool success, string message, Pawn pawn = null)
        {
            Success = success;
            Message = message;
            Pawn = pawn;
        }
    }
}