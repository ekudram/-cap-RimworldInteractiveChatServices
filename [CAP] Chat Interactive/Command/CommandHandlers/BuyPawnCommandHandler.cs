// BuyPawnCommandHandler.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
//
// Pawn purchase command handler
using _CAP__Chat_Interactive.Utilities;
using CAP_ChatInteractive;
using CAP_ChatInteractive.Commands.CommandHandlers;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    public static class BuyPawnCommandHandler
    {
        public static string HandleBuyPawnCommand(ChatMessageWrapper user, string raceName, string xenotypeName = "Baseliner", string genderName = "Random", string ageString = "Random")
        {
            try
            {
                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(user.Username);
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the new manager
                if (assignmentManager != null && assignmentManager.HasAssignedPawn(user.Username))
                {
                    Pawn existingPawn = assignmentManager.GetAssignedPawn(user.Username);
                    if (existingPawn != null && !existingPawn.Dead && existingPawn.Spawned)
                    {
                        return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn to check on them.";
                    }
                }
                // Additionally, check if viewer has any pawns by name in the colony
                if (DoesViewerHavePawnByName(user.Username))
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

                // Validate xenotype if applicable
                if (!string.IsNullOrEmpty(xenotypeName) && xenotypeName != "Baseliner" && ModsConfig.BiotechActive)
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

                var result = GenerateAndSpawnPawn(user.Username, raceName, xenotypeName, genderName, age, raceSettings);

                if (result.Success)
                {
                    // Deduct coins and update karma
                    viewer.TakeCoins(finalPrice);
                    viewer.GiveKarma(CalculateKarmaChange(finalPrice));

                    // Save pawn assignment to viewer
                    if (result.Pawn != null && assignmentManager != null)
                    {
                        assignmentManager.AssignPawnToViewer(user.Username, result.Pawn);
                    }

                    // Send notification
                    string xenotypeInfo = xenotypeName != "Baseliner" ? $" ({xenotypeName})" : "";
                    string ageInfo = ageString != "Random" ? $", Age: {age}" : "";

                    // Send gold letter for pawn purchases (always considered major)
                    MessageHandler.SendGoldLetter(
                        $"New Colonist - {user.Username}",
                        $"{user.Username} has purchased a {raceName}{xenotypeInfo} pawn!{ageInfo}\n\nCost: {finalPrice}{currencySymbol}\nPawn: {result.Pawn?.Name?.ToStringFull ?? "Unknown"}"
                    );

                    return $"Successfully purchased {raceName} pawn for {finalPrice}{currencySymbol}! Welcome to the colony!";
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

                // Find spawn location
                if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => map.reachability.CanReachColony(c) && !c.Fogged(map),
                    map,
                    CellFinder.EdgeRoadChance_Neutral,
                    out IntVec3 spawnLoc))
                {
                    return new BuyPawnResult(false, "Could not find valid spawn location.");
                }

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
                        // Custom xenotype - you might need special handling here
                        Logger.Warning($"Custom xenotype '{xenotypeName}' detected. Custom xenotype support may vary.");
                        // For custom xenotypes, we'd need to handle them differently
                        // For now, we'll proceed without forcing a xenotype
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

                // Spawn pawn
                GenSpawn.Spawn(pawn, spawnLoc, map, WipeMode.Vanish);

                // Send letter notification
                TaggedString letterTitle = $"{username} Joins Colony";
                TaggedString letterText = $"{username} has purchased a {raceName} pawn and joined the colony!";
                PawnRelationUtility.TryAppendRelationsWithColonistsInfo(ref letterText, ref letterTitle, pawn);

                Find.LetterStack.ReceiveLetter(letterTitle, letterText, LetterDefOf.PositiveEvent, pawn);

                return new BuyPawnResult(true, "Pawn purchased successfully!", pawn);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error generating pawn: {ex}");
                return new BuyPawnResult(false, $"Generation error: {ex.Message}");
            }
        }

        private static PawnKindDef GetPawnKindDefForRace(string raceName)
        {
            // Use centralized race lookup
            var raceDef = RaceUtils.FindRaceByName(raceName);
            if (raceDef != null)
            {
                // Find a pawn kind def that uses this race
                var pawnKindDef = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(
                    pk => pk.race == raceDef && pk.defaultFactionDef == FactionDefOf.PlayerColony);

                if (pawnKindDef != null)
                    return pawnKindDef;

                // Fallback to any pawn kind def for this race
                pawnKindDef = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(
                    pk => pk.race == raceDef);

                return pawnKindDef ?? PawnKindDefOf.Colonist;
            }

            return PawnKindDefOf.Colonist;
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
            // Check if race is enabled using centralized logic
            if (!RaceUtils.IsRaceEnabled(raceDef.defName))
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
            // Check if xenotype is explicitly disabled in EnabledXenotypes
            if (raceSettings.EnabledXenotypes.ContainsKey(xenotypeName) && !raceSettings.EnabledXenotypes[xenotypeName])
                return false;

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
            return genderName.ToLowerInvariant() switch
            {
                "male" => Gender.Male,
                "female" => Gender.Female,
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