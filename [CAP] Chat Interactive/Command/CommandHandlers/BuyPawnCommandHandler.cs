// BuyPawnCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Pawn purchase command handler
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
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

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null && assignmentManager.HasAssignedPawn(messageWrapper))
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                    if (existingPawn != null && !existingPawn.Dead && existingPawn.Spawned)
                    {
                        return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn to check on them.";
                    }
                }
                // Additionally, check if viewer has any pawns by name in the colony
                if (DoesViewerHavePawnByName(messageWrapper.Username))
                {
                    return $"You already have a pawn in the colony with your name! Use !mypawn to check on them.";
                }

                // Initialize raceSettings to null
                RaceSettings raceSettings = null;

                // Validate the pawn request FIRST to get raceSettings
                if (!IsValidPawnRequest(raceName, xenotypeName, out raceSettings))
                {
                    // Provide specific error messages
                    if (raceSettings == null)
                        return $"Race '{raceName}' not found or not humanlike.";

                    if (!raceSettings.Enabled)
                        return $"Race '{raceName}' is disabled for purchase.";

                    return $"Invalid pawn request for {raceName}.";
                }

                // NOW parse age with the validated raceSettings
                int age = ParseAge(ageString, raceSettings);

                // Validate age against race settings
                if (age < raceSettings.MinAge || age > raceSettings.MaxAge)
                {
                    return $"Age must be between {raceSettings.MinAge} and {raceSettings.MaxAge} for {raceName}.";
                }

                // Validate xenotype if applicable - FIXED: Added null check for raceSettings
                if (!string.IsNullOrEmpty(xenotypeName) && xenotypeName != "Baseliner" && ModsConfig.BiotechActive && raceSettings != null)
                {
                    // Check if xenotype is enabled (using your new EnabledXenotypes dictionary)
                    if (raceSettings.EnabledXenotypes != null &&
                        raceSettings.EnabledXenotypes.ContainsKey(xenotypeName) &&
                        !raceSettings.EnabledXenotypes[xenotypeName])
                    {
                        return $"Xenotype '{xenotypeName}' is disabled for {raceName}.";
                    }

                    // Or if you want to check if it's allowed at all
                    if (!raceSettings.AllowCustomXenotypes && xenotypeName != "Baseliner")
                    {
                        return $"Custom xenotypes are not allowed for {raceName}.";
                    }
                }

                // Calculate final price with xenotype multiplier
                int basePrice = raceSettings.BasePrice;
                float xenotypeMultiplier = GetXenotypeMultiplier(raceSettings, xenotypeName);
                int finalPrice = (int)(basePrice * xenotypeMultiplier);

                // Check if viewer can afford
                if (viewer.Coins < finalPrice)
                {
                    return $"You need {finalPrice}{currencySymbol} to purchase a {raceName} pawn! You have {viewer.Coins}{currencySymbol}.";
                }

                if (!IsGameReadyForPawnPurchase())
                {
                    return "Game not ready for pawn purchase (no colony, in menu, etc.)";
                }

                var result = GenerateAndSpawnPawn(messageWrapper.Username, raceName, xenotypeName, genderName, age, raceSettings);

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

                    // Send notification
                    string xenotypeInfo = xenotypeName != "Baseliner" ? $" ({xenotypeName})" : "";
                    string ageInfo = ageString != "Random" ? $", Age: {age}" : "";

                    // Send gold letter for pawn purchases (always considered major)
                    MessageHandler.SendGoldLetter(
                        $"New Colonist - {messageWrapper.Username}",
                        $"{messageWrapper.Username} has purchased a {raceName}{xenotypeInfo} of {ageInfo} years.\n\nCost: {finalPrice:N0}{currencySymbol}\nPawn: {result.Pawn?.Name?.ToStringFull ?? "Unknown"}"
                    );

                    return $"Successfully purchased {raceName} pawn for {finalPrice:N0}{currencySymbol}! Welcome to the colony!";
                }
                else
                {
                    return $"Failed to purchase pawn: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling buy pawn command: {ex}");
                MessageHandler.SendFailureLetter("Pawn Purchase Error",
                    $"Error purchasing pawn: {ex.Message}");
                return "Error purchasing pawn. Please try again.";
            }
        }

        private static BuyPawnResult GenerateAndSpawnPawn(string username, string raceName, string xenotypeName, string genderName, int age, RaceSettings raceSettings)
        {
            try
            {
                var playerMaps = Current.Game.Maps.Where(map => map.IsPlayerHome).ToList();
                if (!playerMaps.Any())
                {
                    return new BuyPawnResult(false, "No player home maps found.");
                }

                var map = playerMaps.First();

                // Get pawn kind def for the race
                var pawnKindDef = GetPawnKindDefForRace(raceName);
                if (pawnKindDef == null)
                {
                    return new BuyPawnResult(false, $"Could not find pawn kind for race: {raceName}");
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
                    return new BuyPawnResult(false, $"Race '{raceName}' not found.");
                }

                // Validate gender against race restrictions
                // Validate gender against race restrictions - USE CENTRALIZED SETTINGS
                //var raceSettings = RaceSettingsManager.GetRaceSettings(raceDef.defName);
                if (raceSettings != null)
                {
                    var requestedGender = ParseGender(genderName);
                    if (requestedGender.HasValue && !IsGenderAllowed(raceSettings.AllowedGenders, requestedGender.Value))
                    {
                        string allowedText = GetAllowedGendersDescription(raceSettings.AllowedGenders);
                        return new BuyPawnResult(false,
                            $"The {raceName} race allows {allowedText}. Please choose a different gender or use 'random'.");
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
                    return new BuyPawnResult(false, "Could not find valid spawn location for pawn.");
                }

                // Send letter notification we do this when we reture
                // TaggedString letterTitle = $"{username} Joins Colony";
                // TaggedString letterText = $"{username} has purchased a {raceName} pawn and joined the colony!";
                // PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref letterText, ref letterTitle, pawn);

                // Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, pawn);

                return new BuyPawnResult(true, "Pawn purchased successfully!", pawn);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating pawn: {ex}");
                return new BuyPawnResult(false, $"Generation error: {ex.Message}");
            }
        }

        public static PawnKindDef GetPawnKindDefForRace(string raceName)
        {
            // Use centralized race lookup
            var raceDef = RaceUtils.FindRaceByName(raceName);
            if (raceDef == null)
            {
                Logger.Warning($"Race not found: {raceName}");
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
            Logger.Warning($"No pawn kind found for race: {raceDef.defName}, using default Colonist");
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

        private static float GetXenotypeMultiplier(RaceSettings raceSettings, string xenotypeName)
        {
            if (xenotypeName == "Baseliner" || string.IsNullOrEmpty(xenotypeName))
                return 1.0f;

            // Ensure dictionary is initialized
            if (raceSettings.XenotypePrices == null)
                raceSettings.XenotypePrices = new Dictionary<string, float>();

            if (raceSettings.XenotypePrices.TryGetValue(xenotypeName, out float multiplier))
            {
                return multiplier;
            }

            // Use gene-based pricing as default
            return GeneUtils.CalculateXenotypeGeneCost(xenotypeName);
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

        private static bool DoesViewerHavePawnByName(string username)
        {
            // Search all maps for pawns with the viewer's username as nickname
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.Name is NameTriple nameTriple &&
                        nameTriple.Nick.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    else if (pawn.Name is NameSingle nameSingle &&
                             nameSingle.Name.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
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

        public static void CleanupDeadPawnAssignments()
        {
            foreach (var viewer in Viewers.All)
            {
                if (!string.IsNullOrEmpty(viewer.AssignedPawnId))
                {
                    var pawn = FindPawnByThingId(viewer.AssignedPawnId);
                    if (pawn == null || pawn.Dead)
                    {
                        viewer.AssignedPawnId = null;
                    }
                }
            }
            Viewers.SaveViewers();
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
                return "no genders (custom race)";

            if (allowedGenders.AllowMale && !allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "only male";

            if (!allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "only female";

            if (allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "male or female only (no other)";

            return "any gender";
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
                    return "You must specify a race. Usage: !pawn <race> [xenotype] [gender] [age]";
                }

                // Call the existing handler with parsed parameters
                return HandleBuyPawnCommandInternal(messageWrapper, raceName, xenotypeName, genderName, ageString);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error parsing pawn command: {ex}");
                return "Error parsing command. Usage: !pawn <race> [xenotype] [gender] [age]";
            }
        }

        // Smart parameter parsing
        private static void ParsePawnParameters(string[] args, out string raceName, out string xenotypeName, out string genderName, out string ageString)
        {
            raceName = "";
            xenotypeName = "Baseliner";
            genderName = "Random";
            ageString = "Random";

            if (args.Length == 0) return;

            raceName = args[0];

            // Track what we've assigned
            bool hasXenotype = false;
            bool hasGender = false;
            bool hasAge = false;

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i].ToLower();

                // Check for gender (highest priority - unambiguous)
                if (!hasGender && (arg == "male" || arg == "female" || arg == "m" || arg == "f"))
                {
                    genderName = args[i];
                    hasGender = true;
                    continue;
                }

                // Check for age (also unambiguous if it's a number)
                if (!hasAge && (int.TryParse(arg, out int age) || arg == "random"))
                {
                    ageString = args[i];
                    hasAge = true;
                    continue;
                }

                // If we get here and don't have a xenotype yet, assume it's a xenotype
                if (!hasXenotype)
                {
                    xenotypeName = args[i];
                    hasXenotype = true;
                }
            }
        }

        // List methods
        public static string ListAvailableRaces()
        {
            var availableRaces = RaceUtils.GetEnabledRaces();

            if (availableRaces.Count == 0)
            {
                return "No races available for purchase.";
            }

            // Also show how many total races exist for context
            var allRaces = RaceUtils.GetAllHumanlikeRaces();
            var raceSettings = JsonFileManager.LoadRaceSettings();

            var raceList = availableRaces.Select(r =>
            {
                var inSettings = raceSettings.ContainsKey(r.defName);
                var settings = inSettings ? raceSettings[r.defName] : null;
                return $"{r.LabelCap.RawText}{(inSettings ? "" : " [NEW]")}";
            });

            string result = $"Available races ({availableRaces.Count} of {allRaces.Count()} total): {string.Join(", ", raceList.Take(8))}";

            if (availableRaces.Count > 8)
                result += $" (and {availableRaces.Count - 8} more...)";

            if (availableRaces.Count < allRaces.Count())
                result += $"\n{allRaces.Count() - availableRaces.Count} races are disabled in settings";

            return result;
        }

        public static string ListAvailableXenotypes(string raceName = null)
        {
            if (!ModsConfig.BiotechActive)
            {
                return "Biotech DLC not active - only baseliners available.";
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

                            return $"Xenotypes available for {raceDef.LabelCap}: {string.Join(", ", enabledXenotypes)}" +
                                   (allowedXenotypes.Count > 12 ? $" (... {allowedXenotypes.Count - 12} more)" : "");
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
                    return "No xenotypes found (except Baseliner).";
                }

                return $"Common xenotypes: {string.Join(", ", allXenotypes)}" +
                       (allXenotypes.Count >= 15 ? " (and many more - try !pawn <race> to see race-specific xenotypes)" : "");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error listing xenotypes: {ex}");
                return "Error retrieving xenotype list. You can still use custom xenotype names.";
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
                string status = pawn.Spawned ? "alive and in colony" : "alive but not in colony";
                string health = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
                int traitCount = pawn.story?.traits?.allTraits?.Count ?? 0;
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                int maxTraits = settings?.MaxTraits ?? 4;

                return $"Your pawn {pawn.Name} is {status}. Health: {health}, Age: {pawn.ageTracker.AgeBiologicalYears}, Traits: {traitCount}/{maxTraits}";
            }
            else
            {
                return "You don't have an active pawn in the colony. Use !pawn to purchase one!";
            }
        }

        public static string DebugRaceSettings(string raceName)
        {
            var raceDef = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(
                d => d.race?.Humanlike == true &&
                (d.defName.Equals(raceName, StringComparison.OrdinalIgnoreCase) ||
                 d.label.Equals(raceName, StringComparison.OrdinalIgnoreCase)));

            if (raceDef == null)
                return $"Race not found: {raceName}";

            var settings = JsonFileManager.GetRaceSettings(raceDef.defName);

            return $"Race: {raceDef.defName}, Enabled: {settings.Enabled}, Price: {settings.BasePrice}, " +
                   $"MinAge: {settings.MinAge}, MaxAge: {settings.MaxAge}";
        }

        public static string TestPawnKindSelection(string raceName)
        {
            var raceDef = RaceUtils.FindRaceByName(raceName);
            if (raceDef == null) return $"Race not found: {raceName}";

            var selectedPawnKind = GetPawnKindDefForRace(raceName);
            if (selectedPawnKind != null)
            {
                return $"Selected pawn kind for {raceName}: {selectedPawnKind.defName} (Faction: {selectedPawnKind.defaultFactionDef?.defName ?? "None"}, isPlayer: {selectedPawnKind.defaultFactionDef?.isPlayer ?? false})";
            }

            return $"No pawn kind found for {raceName}, using default Colonist";
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