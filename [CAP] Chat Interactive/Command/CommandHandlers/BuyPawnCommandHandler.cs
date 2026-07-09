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
using _CAP__Chat_Interactive.Command.CommandHelpers;
using _CAP__Chat_Interactive.Utilities;
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
        /// <summary>
        /// Handles the !pawn command from chat, parsing arguments and generating a pawn for the viewer.
        /// </summary>
        /// <param name="messageWrapper"></param>
        /// <param name="args"></param>
        /// <returns>
        /// Message to chat indicating success or failure, including details about the pawn or error.
        /// </returns>
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
                return "RICS.BPCH.ParseError".Translate();
            }
        }

        /// <summary>
        /// Parses the command arguments to extract
        /// Helper method to parse command arguments for Pawn purchase, extracting race, xenotype,
        /// </summary>
        /// <param name="args"></param>
        /// <param name="raceName"></param>
        /// <param name="xenotypeName"></param>
        /// <param name="genderName"></param>
        /// <param name="ageString"></param>
        private static void ParsePawnParameters(string[] args, out string raceName, out string xenotypeName, out string genderName, out string ageString)
        {
            // Defaults
            raceName = "";
            xenotypeName = "Baseliner";
            genderName = "Random";
            ageString = "Random";

            if (args.Length == 0) return;

            var usedArgs = new bool[args.Length];

            // STEP 1: Extract AGE (highest certainty - numeric)
            for (int i = 0; i < args.Length; i++)
            {
                if (usedArgs[i]) continue;
                if (int.TryParse(args[i], out int age) && age > 0 && age <= 150)
                {
                    ageString = args[i];
                    usedArgs[i] = true;
                    break;
                }
            }

            // STEP 2: Extract GENDER (limited set, safe)
            for (int i = 0; i < args.Length; i++)
            {
                if (usedArgs[i]) continue;
                string argLower = args[i].ToLowerInvariant();
                if (argLower is "male" or "m" or "female" or "f")
                {
                    genderName = args[i]; // preserve case
                    usedArgs[i] = true;
                    break;
                }
            }

            // STEP 3: Collect ALL remaining args (in original order)
            var remaining = new List<(int index, string value)>();
            for (int i = 0; i < args.Length; i++)
            {
                if (!usedArgs[i])
                    remaining.Add((i, args[i]));
            }
            if (remaining.Count == 0) return;

            // STEP 4: Longest PREFIX match for race (greedy from start of remaining)
            // This is the fix - previous version marked the entire attempted len even when only a sub-match was found
            string bestRace = "";
            int bestLength = 0;
            for (int len = Math.Min(4, remaining.Count); len >= 1; len--)
            {
                var candidateParts = remaining.Take(len).Select(x => x.value).ToArray();
                string matchedRace = FindBestRaceMatch(candidateParts);
                if (!string.IsNullOrEmpty(matchedRace) && len > bestLength)
                {
                    bestRace = matchedRace;
                    bestLength = len;
                    break; // longest first
                }
            }

            if (!string.IsNullOrEmpty(bestRace))
            {
                raceName = bestRace;
                // Mark ONLY the words actually used for the race
                for (int k = 0; k < bestLength; k++)
                {
                    usedArgs[remaining[k].index] = true;
                }
            }
            else
            {
                // Fallback: whole remaining string as race (original behavior for unrecognized input)
                raceName = string.Join(" ", remaining.Select(x => x.value));
                foreach (var r in remaining) usedArgs[r.index] = true;
            }

            // STEP 5: Leftover becomes xenotype (exact user casing preserved for display/letters)
            var leftover = remaining.Where(r => !usedArgs[r.index]).Select(r => r.value).ToArray();
            if (leftover.Length > 0)
            {
                xenotypeName = string.Join(" ", leftover);
            }
        }

        /// <summary>
        /// Handles the internal logic of buying a pawn after parameters have been parsed and validated.
        /// </summary>
        /// <param name="messageWrapper"></param>
        /// <param name="raceName"></param>
        /// <param name="xenotypeName"></param>
        /// <param name="genderName"></param>
        /// <param name="ageString"></param>
        /// <returns></returns>
        private static string HandleBuyPawnCommandInternal(ChatMessageWrapper messageWrapper, string raceName, string xenotypeName = "Baseliner", string genderName = "Random", string ageString = "Random")
        {
            try
            {
                if (!IsGameReadyForPawnPurchase())
                {
                    return "RICS.BPCH.GameNotReady".Translate();
                }

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";

                var viewer = Viewers.GetViewer(messageWrapper);
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                // Check if viewer already has a pawn assigned using the assignment manager
                Pawn existingPawn = assignmentManager.GetAssignedPawn(messageWrapper);
                if (existingPawn != null)
                {
                    // return $"You already have a pawn in the colony: {existingPawn.Name}! Use !mypawn health to check on them.";
                    return "RICS.BPCH.AlreadyHasPawn".Translate(existingPawn.Name.ToStringFull);
                }

                // Redundant check using platform ID for safety (in case assignment manager is null or misbehaving)
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

                // Validate the pawn request FIRST to get raceSettings
                if (!IsValidPawnRequest(raceName, xenotypeName, out RaceSettings raceSettings))
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

                // === XENOTYPE RESOLUTION & VALIDATION ===
                // WHY: Xenotypes are a Biotech DLC feature only.
                // When Biotech is disabled:
                //   - raceSettings.EnabledXenotypes is empty (see RaceSettingsManager.CreateDefaultSettings)
                //   - All pawns are Baseliner by definition
                //   - Any user-supplied xenotype name must be ignored gracefully
                // This guard keeps behavior correct, avoids unnecessary work / confusing debug logs,
                // and matches the defensive pattern already used in IsValidPawnRequest and GenerateAndSpawnPawn.

                string finalXenotypeName = "Baseliner";

                if (ModsConfig.BiotechActive)
                {
                    finalXenotypeName = xenotypeName;  // start with cleaned user input

                    // Auto-pick logic if no xenotype given or Baseliner is disabled for this race
                    // HAR support: now prefers race-specific xenotype (e.g. Nyaron race → Nyaron xenotype)
                    if (string.IsNullOrEmpty(xenotypeName) ||
                        xenotypeName.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!raceSettings.EnabledXenotypes.TryGetValue("Baseliner", out bool baselinerEnabled) || !baselinerEnabled)
                        {
                            finalXenotypeName = PickRandomEnabledXenotype(raceSettings, raceName);
                            Logger.Debug($"Baseliner disabled for {raceName} → auto-picked '{finalXenotypeName}' from RaceSettings (HAR-aware)");
                        }
                        else
                        {
                            finalXenotypeName = "Baseliner";
                        }
                    }
                    else
                    {
                        finalXenotypeName = GetXenotypeDefName(xenotypeName, raceSettings);
                    }

                    // Now validate the final resolved xenotype
                    bool isEnabled = raceSettings.EnabledXenotypes.TryGetValue(finalXenotypeName, out bool enabled)
                        ? enabled
                        : raceSettings.AllowCustomXenotypes;

                    if (!isEnabled)
                    {
                        return "RICS.BPCH.XenotypeDisabled".Translate(xenotypeName, raceName);  // show original user input in error
                    }

                    if (!raceSettings.AllowCustomXenotypes && finalXenotypeName != "Baseliner")
                    {
                        return "RICS.BPCH.CustomXenotypesDisabled".Translate(raceName);
                    }
                }
                else
                {
                    // No Biotech DLC → force Baseliner. Ignore any xenotype the user typed.
                    // This is the only correct behavior. Debug log so developers see it during testing.
                    if (!string.IsNullOrEmpty(xenotypeName) && !xenotypeName.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Debug($"Ignoring xenotype argument '{xenotypeName}' for race '{raceName}' because Biotech DLC is not active. Forcing Baseliner.");
                    }
                    finalXenotypeName = "Baseliner";
                }

                // Price from RaceSettings (no manual adding)
                int finalPrice = raceSettings.BasePrice;
                // if Xenotype get that price instead from Race Settings.
                if (raceSettings.XenotypePrices.TryGetValue(finalXenotypeName, out float price))
                    finalPrice = (int)price;

                // Check if viewer can afford
                if (viewer.Coins < finalPrice)
                {
                    // return $"You need {finalPrice}{currencySymbol}
                    // to purchase a {raceName} pawn! You have {viewer.Coins}{currencySymbol}.";
                    return "RICS.BPCH.InsufficientFunds"
                        .Translate(finalPrice, currencySymbol, raceName, viewer.Coins, currencySymbol);
                }

                // Generate and spawn the pawn
                var result = GenerateAndSpawnPawn(messageWrapper.Username, raceName, finalXenotypeName, genderName, age, raceSettings);

                if (result.Success)
                {
                    // Deduct coins and update karma
                    viewer.TakeCoins(finalPrice);
                    // Use the new store karma setting (pawns are expensive, so they naturally give more karma)
                    float karmaEarned = finalPrice * settings.KarmaPerStoreItem / 100f;
                    if (karmaEarned > 0f)
                    {
                        viewer.GiveKarma(karmaEarned);
                        Logger.Debug($"Awarded {karmaEarned:F2} karma for pawn purchase (price × KarmaPerStoreItem / 100)");
                    }

                    // Save pawn assignment to viewer
                    if (result.Pawn != null && assignmentManager != null)
                    {
                        assignmentManager.AssignPawnToViewer(messageWrapper, result.Pawn);
                    }

                    // Use the exact drop position we already know (bypasses timing issues)
                    string locationInfo = "RICS.BPCH.Letter.Delivery.Unknown".Translate();

                    if (result.DeliveryPosition.IsValid)
                    {
                        string mapName = result.Pawn?.Map?.Parent?.LabelCap ?? "Home Map";
                        locationInfo = "RICS.BPCH.Letter.Delivery".Translate(
                            result.DeliveryPosition.x,
                            result.DeliveryPosition.z,
                            mapName);
                        Logger.Debug($"Letter using known drop position: {result.DeliveryPosition}");
                    }
                    else if (result.Pawn != null && result.Pawn.Map != null)
                    {
                        IntVec3 pos = result.Pawn.PositionHeld;
                        string mapName = result.Pawn.Map.Parent?.LabelCap ?? "Home Map";
                        locationInfo = "RICS.BPCH.Letter.Delivery".Translate(pos.x, pos.z, mapName);
                    }

                    // Send notification
                    string xenotypeInfo = finalXenotypeName != "Baseliner" ? $" ({finalXenotypeName})" : "";
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
                    Logger.Debug("RICS.BPCH.Letter.Text".Translate(
                        messageWrapper.Username,
                        raceName,
                        xenotypeInfo,
                        age.ToString(),
                        finalPrice.ToString("N0"),
                        currencySymbol,
                        result.Pawn?.Name.ToStringFull ?? "Unnamed",
                        locationInfo  // New {7} placeholder
                    ));
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
                return "RICS.BPCH.Error.Purchase".Translate();
            }
        }

        private static BuyPawnResult GenerateAndSpawnPawn(string username, string raceName, string xenotypeName, string genderName, int age, RaceSettings raceSettings)
        {
            try
            {
                Logger.Debug($"GenerateAndSpawnPawn:   xenotypeName: {xenotypeName}");
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

                // === XENOTYPE RESOLUTION (RaceSettings is truth) ===
                XenotypeDef xenotypeDef = null;
                string resolvedXenotype = xenotypeName;

                // Auto-pick if nothing specified or Baseliner is disabled for this race
                // HAR support: now prefers race-specific xenotype (Nyaron example)
                if (string.IsNullOrEmpty(xenotypeName) || xenotypeName.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                {
                    if (!raceSettings.EnabledXenotypes.TryGetValue("Baseliner", out bool baselinerEnabled) || !baselinerEnabled)
                    {
                        resolvedXenotype = PickRandomEnabledXenotype(raceSettings, raceName);
                        Logger.Debug($"Baseliner disabled → auto-picked '{resolvedXenotype}' from RaceSettings (HAR-aware)");
                    }
                    else
                    {
                        resolvedXenotype = "Baseliner";
                    }
                }
                else
                {
                    resolvedXenotype = GetXenotypeDefName(xenotypeName, raceSettings);
                }

                // Try real XenotypeDef only for forcing (HAR handles the rest)
                if (ModsConfig.BiotechActive && resolvedXenotype != "Baseliner")
                {
                    xenotypeDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(resolvedXenotype);
                    if (xenotypeDef == null)
                    {
                        xenotypeDef = DefDatabase<XenotypeDef>.AllDefs
                            .FirstOrDefault(x => x.label.Equals(resolvedXenotype, StringComparison.OrdinalIgnoreCase));
                    }

                    if (xenotypeDef != null)
                        Logger.Debug($"[BuyPawn] Forcing real XenotypeDef: {xenotypeDef.defName}");
                    else
                        Logger.Debug($"[BuyPawn] No real XenotypeDef for '{resolvedXenotype}' → null (HAR will handle)");
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

                Logger.Debug($"GenerateAndSpawnPawn: XenotypeDef = {(xenotypeDef != null ? xenotypeDef.defName + " (" + xenotypeDef.LabelCap + ")" : "null → Baseliner fallback")}");

                // Prepare generation request with specific age and xenotype
                // === EXACT 1.6 PAWNGENERATIONREQUEST (verified from decompile) ===
                // forceNoGear = false lets vanilla generate normal starting clothes (fixes naked pawns).
                // Vacsuit is layered on top for space maps only.
                var request = new PawnGenerationRequest(
                    kind: pawnKindDef,                                      // HAR-aware pawn kind (already resolved by GetPawnKindDefForRace)
                    faction: Faction.OfPlayer,                              // Pawn joins colony immediately (vanilla new-colonist behavior)
                    context: PawnGenerationContext.NonPlayer,               // NonPlayer = purchased viewer pawn (matches caravans/traders)
                    tile: map.Tile,                                         // Required for proper world-tile context
                    forceGenerateNewPawn: true,                             // Always fresh pawn, never reuse world-pawn template
                    allowDead: false,                                       // Never spawn dead
                    allowDowned: false,                                     // Fresh colonists should not be downed
                    canGeneratePawnRelations: false,                        // No unwanted family links for purchased pawns
                    mustBeCapableOfViolence: false,                         // Allow peaceful colonists (viewer choice)
                    colonistRelationChanceFactor: 0f,                       // No extra colonist relations
                    forceAddFreeWarmLayerIfNeeded: true,                    // Safety for cold maps
                    allowGay: true,
                    allowPregnant: false,                                   // No pregnant purchased pawns
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    fixedBiologicalAge: age,                                // Viewer-requested age (or random within race limits)
                    fixedChronologicalAge: null,
                    fixedGender: ParseGender(genderName),                   // Viewer-requested gender or random
                    fixedLastName: null,                                    // We override name later with username
                    forceNoIdeo: false,                                     // Let Ideology apply normally
                    forceNoBackstory: false,                                // Let vanilla backstories generate
                    forbidAnyTitle: false,
                    forceDead: false,
                    forcedXenotype: xenotypeDef,                            // HAR-aware xenotype (Nyaron example) — null falls back correctly
                    forceBaselinerChance: 0f,
                    developmentalStages: DevelopmentalStage.Adult,          // Adult unless race forces otherwise
                    forceNoGear: false,                                     // Let vanilla generate starting clothes (fixes naked pawns)
                    dontGiveWeapon: true,                                   // No random weapon (we want clean delivery)
                    onlyUseForcedBackstories: false,
                    maximumAgeTraits: -1,                                   // Let vanilla decide trait count
                    minimumAgeTraits: 0
                );

                Logger.Debug($"PawnGenerationRequest built for buyer '{username}': " +
                             $"Kind={pawnKindDef?.defName ?? "null"}, " +
                             $"Xenotype={xenotypeDef?.defName ?? "null (HAR default)"}, " +
                             $"Age={age}, Gender={ParseGender(genderName)?.ToString() ?? "Random"}");

                Logger.Debug($"ForcedXenotype in request: {(request.ForcedXenotype?.defName ?? "null (defaults to Baseliner)")}");

                // Generate pawn using RimWorld's full system (PawnGenerator + PawnGenerationRequest)
                // Why: This is the exact same path vanilla uses for new colonists, caravans, and trader pawns.
                // It correctly applies forcedXenotype, age, gender, and HAR race rules.
                Pawn pawn = PawnGenerator.GeneratePawn(request);

                if (pawn != null)
                {
                    var actualXeno = pawn.genes?.Xenotype?.defName ?? "no genes component";
                    Logger.Debug($"Pawn generated successfully - Xenotype: {actualXeno}");
                }
                else
                {
                    Logger.Error("PawnGenerator returned null pawn!");
                    return new BuyPawnResult(false, "RICS.BPCH.GenerationError".Translate("PawnGenerator returned null"));
                }

                // Set custom name (viewer username as nickname)
                // Why: Keeps RimWorld name triple structure while making the pawn clearly belong to the buyer.
                if (pawn.Name is NameTriple nameTriple)
                {
                    pawn.Name = new NameTriple(nameTriple.First, username, nameTriple.Last);
                }
                else
                {
                    pawn.Name = new NameSingle(username);
                }

                // Spawn using robust multi-strategy system (drop pod → locker → colonist → home area)
                // Why: Handles space, underground, and modded maps reliably while preserving vanilla drop-pod feel.
                // Spawn using robust multi-strategy system...
                IntVec3 deliveryPos = IntVec3.Invalid;
                if (!TrySpawnPawnInSpaceBiome(pawn, map, out deliveryPos))
                {
                    Logger.Error("All spawn strategies failed for purchased pawn");
                    return new BuyPawnResult(false, "RICS.BPCH.SpawnLocationNotFound".Translate());
                }

                return new BuyPawnResult(true, "RICS.BPCH.PawnGenerated".Translate(), pawn, deliveryPos);
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

        private static bool IsXenotypeAllowed(RaceSettings raceSettings, string xenotypeInput)
        {
            string xenoDefName = GetXenotypeDefName(xenotypeInput, raceSettings);

            Logger.Debug($"Checking xenotype input '{xenotypeInput}' → resolved '{xenoDefName}'");

            if (raceSettings.EnabledXenotypes != null)
            {
                foreach (var kvp in raceSettings.EnabledXenotypes)
                    Logger.Debug($"    {kvp.Key} = {kvp.Value}");

                if (raceSettings.EnabledXenotypes.ContainsKey(xenoDefName))
                {
                    bool result = raceSettings.EnabledXenotypes[xenoDefName];
                    Logger.Debug($"  Exact match found: {result}");
                    return result;
                }
            }

            Logger.Debug($"  No match found, using default logic");

            if (raceSettings.EnabledXenotypes == null)
                raceSettings.EnabledXenotypes = new Dictionary<string, bool>();
            if (raceSettings.XenotypePrices == null)
                raceSettings.XenotypePrices = new Dictionary<string, float>();

            if (raceSettings.EnabledXenotypes.Count > 0)
            {
                if (!raceSettings.EnabledXenotypes.ContainsKey(xenoDefName))
                {
                    if (IsCustomXenotype(xenotypeInput, raceSettings))  // pass original for custom check
                        return raceSettings.AllowCustomXenotypes;
                    return false;
                }
                return raceSettings.EnabledXenotypes[xenoDefName];
            }

            if (IsCustomXenotype(xenotypeInput, raceSettings) && !raceSettings.AllowCustomXenotypes)
                return false;

            return true;
        }

        // Update IsCustomXenotype (tiny change – now works with labels)
        private static bool IsCustomXenotype(string input, RaceSettings raceSettings)
        {

            string defName = GetXenotypeDefName(input, raceSettings);
            return DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(defName, StringComparison.OrdinalIgnoreCase)) == null;
        }

        private static bool IsGameReadyForPawnPurchase()
        {
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   Current.Game.Maps.Any(map => map.IsPlayerHome);
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

        private static bool TrySpawnPawnInSpaceBiome(Pawn pawn, Map map, out IntVec3 deliveryPosition)
        {
            deliveryPosition = IntVec3.Invalid;
            try
            {
                Logger.Debug($"Attempting to spawn pawn in biome: {map.Biome?.defName} (underground: {ItemDeliveryHelper.IsUndergroundMap(map)})");

                // === PRIORITY 1: Use the robust ItemDeliveryHelper drop position (includes Rimazon marker, relaxed checks, etc.) ===
                if (ItemDeliveryHelper.TryFindSafeDropPosition(map, out IntVec3 safePos))
                {
                    deliveryPosition = safePos;
                    Logger.Debug($"Using safe drop position from ItemDeliveryHelper: {safePos}");

                    // Drop pod delivery (preferred for immersion)
                    List<Thing> things = new List<Thing> { pawn };
                    if (ItemDeliveryHelper.IsSpaceMap(map) || (map.Biome?.inVacuum == true))
                    {
                        ItemDeliveryHelper.TryEquipVacsuit(pawn, map); // your existing method
                    }

                    DropPodUtility.DropThingsNear(safePos, map, things, openDelay: 110, leaveSlag: false, canRoofPunch: true, forbid: true);
                    return true;
                }

                // === PRIORITY 2: Fallback to near locker or existing pawn (simple GenSpawn) ===
                var locker = ItemDeliveryHelper.FindSuitableLockerFor(pawn, map);
                if (locker != null)
                {
                    if (CellFinder.TryFindRandomCellNear(locker.Position, map, 6, c => c.Standable(map) && c.Walkable(map), out var near))
                    {
                        GenSpawn.Spawn(pawn, near, map);
                        deliveryPosition = near;
                        return true;
                    }
                }

                // === PRIORITY 3: Near any player pawn (your requested ultimate fallback) ===
                var anyPlayerPawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p =>
                    p.Faction == Faction.OfPlayer && p.Spawned && !p.Dead);

                if (anyPlayerPawn != null)
                {
                    if (CellFinder.TryFindRandomCellNear(anyPlayerPawn.Position, map, 8,
                        c => c.Standable(map) && c.Walkable(map), out var nearPawn))
                    {
                        GenSpawn.Spawn(pawn, nearPawn, map);
                        deliveryPosition = nearPawn;
                        Logger.Debug($"Spawned next to existing player pawn at {nearPawn}");
                        return true;
                    }
                }

                // === FINAL: Map center ===
                GenSpawn.Spawn(pawn, map.Center, map);
                deliveryPosition = map.Center;
                Logger.Warning($"[RICS] Ultimate fallback: Spawned at map center");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in TrySpawnPawnInSpaceBiome: {ex}");
                deliveryPosition = map?.Center ?? IntVec3.Invalid;
                return false;
            }
        }

        private static bool TryDropPodDelivery(Pawn pawn, Map map, out IntVec3 deliveryPos)
        {
            deliveryPos = IntVec3.Invalid;
            try
            {
                // === CRITICAL: Use ItemDeliveryHelper targeting for consistency with store deliveries ===
                // Why: Reuses tested underground/space/trade-spot logic; avoids duplicating drop-spot code.
                IntVec3 dropPos = ItemDeliveryHelper.GetCustomDropSpot(map);

                Logger.Debug($"Attempting drop pod delivery at custom trade spot: {dropPos}");

                if (!ItemDeliveryHelper.IsValidDeliveryPosition(dropPos, map))
                {
                    Logger.Debug("Trade spot invalid → finding nearest valid cell");
                    if (!DropCellFinder.TryFindDropSpotNear(map.Center, map, out dropPos,
                        allowFogged: false, canRoofPunch: true, maxRadius: 30))
                    {
                        Logger.Error("Could not find any valid drop position for pawn delivery");
                        return false;
                    }
                }

                List<Thing> thingsToDeliver = new List<Thing> { pawn };

                // === VAC SUIT FOR SPACE MAPS ===
                // Why: Orbit + inVacuum biomes need immediate breathing gear. Equipped AFTER generation but BEFORE drop pod.
                if (ItemDeliveryHelper.IsSpaceMap(map) || (map.Biome?.inVacuum == true))
                {
                    Logger.Debug($"Space/vacuum map detected ({map.Biome?.defName}) → equipping vacsuit");
                    TryEquipVacsuit(pawn, map);
                }

                // Use RimWorld's built-in drop pod utility (handles pawns perfectly)
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

                deliveryPos = dropPos;   // <-- Capture the exact position
                Logger.Debug($"Delivered pawn via drop pod at: {dropPos}");
                return true;  // We will pass dropPos up the call stack
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in drop pod delivery: {ex}");
                return false;
            }
        }

        private static string GetXenotypeDefName(string input, RaceSettings raceSettings)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                return "Baseliner";

            string clean = input.Trim();

            if (raceSettings?.EnabledXenotypes == null)
                return clean; // fallback

            // 1. Exact match (ignore case)
            var exact = raceSettings.EnabledXenotypes.Keys
                .FirstOrDefault(k => k.Equals(clean, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                Logger.Debug($"RaceSettings exact match: '{clean}' → '{exact}'");
                return exact;
            }

            // 2. Fuzzy/typo match (partial)
            var fuzzy = raceSettings.EnabledXenotypes.Keys
                .Where(k => k.ToLowerInvariant().Contains(clean.ToLowerInvariant()) ||
                            clean.ToLowerInvariant().Contains(k.ToLowerInvariant()))
                .OrderBy(k => Math.Abs(k.Length - clean.Length))
                .FirstOrDefault();

            if (fuzzy != null)
            {
                Logger.Debug($"RaceSettings fuzzy match: '{clean}' → '{fuzzy}'");
                return fuzzy;
            }

            Logger.Debug($"No match in RaceSettings for '{clean}', passing through");
            return clean;
        }


        private static string FindBestRaceMatch(string[] potentialRaceArgs)
        {
            if (potentialRaceArgs == null || potentialRaceArgs.Length == 0) return string.Empty;

            string candidateRace = string.Join(" ", potentialRaceArgs);

            // Centralized lookup (consistent with IsValidPawnRequest / HandleBuyPawnCommand)
            var knownRaces = RaceUtils.GetAllHumanlikeRaces()
                .SelectMany(r => new[] { r.defName, r.label })
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            // Return known casing (e.g. "Human" not "human")
            return knownRaces.FirstOrDefault(r =>
                r.Equals(candidateRace, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }
        // List methods

        private static string PickRandomEnabledXenotype(RaceSettings settings, string raceDefName = null)
        {
            if (settings?.EnabledXenotypes == null)
                return "Baseliner";

            var enabled = settings.EnabledXenotypes
                .Where(kv => kv.Value && kv.Key != "Baseliner")
                .Select(kv => kv.Key)
                .ToList();

            if (!enabled.Any())
                return "Baseliner"; // safety fallback

            // === HAR / custom race support (Nyaron example) ===
            // Prefer the xenotype whose name matches the race (most common pattern in HAR mods)
            // e.g. Nyaron race → Nyaron xenotype (as defined in PawnKindDef xenotypeSet)
            if (!string.IsNullOrEmpty(raceDefName))
            {
                string cleanRace = raceDefName.Replace("Alien_", "").Trim();

                string preferred = enabled.FirstOrDefault(x =>
                    x.Equals(cleanRace, StringComparison.OrdinalIgnoreCase) ||
                    x.Equals(raceDefName, StringComparison.OrdinalIgnoreCase));

                if (preferred != null)
                {
                    Logger.Debug($"[HAR Support] Picked preferred race xenotype '{preferred}' for race '{raceDefName}'");
                    return preferred;
                }
            }

            // Fallback: random among enabled non-Baseliner xenotypes
            string picked = enabled.RandomElement();
            Logger.Debug($"[HAR Support] Picked random enabled xenotype '{picked}' for race '{raceDefName ?? "unknown"}'");
            return picked;
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
                return "RICS.Pawn.NoPawn".Translate();
            }
        }

        // NEW: Auto-equip vacsuit for space purchases
        // Why: Prevents "pawn dies instantly in vacuum" – uses exact defs from user request.
        // Uses PawnApparelGenerator.GenerateApparelOfDefFor (correct API) + direct Wear.
        private static void TryEquipVacsuit(Pawn pawn, Map map)
        {
            try
            {
                if (pawn == null || pawn.apparel == null)
                    return;

                // Prefer child suit for babies/children
                ThingDef suitDef = pawn.ageTracker.CurLifeStageIndex <= 1
                    ? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitChildren")
                    : DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_Vacsuit");

                ThingDef helmetDef = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_VacsuitHelmet");

                // Generate and equip suit
                if (suitDef != null)
                {
                    Apparel suit = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, suitDef);
                    if (suit != null && ApparelUtility.HasPartsToWear(pawn, suit.def))
                    {
                        pawn.apparel.Wear(suit);
                        Logger.Debug($"Equipped vacsuit on space pawn: {pawn.Name}");
                    }
                }

                // Generate and equip helmet
                if (helmetDef != null)
                {
                    Apparel helmet = PawnApparelGenerator.GenerateApparelOfDefFor(pawn, helmetDef);
                    if (helmet != null && ApparelUtility.HasPartsToWear(pawn, helmet.def))
                    {
                        pawn.apparel.Wear(helmet);
                        Logger.Debug($"Equipped vacsuit helmet on space pawn: {pawn.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"Failed to equip vacsuit on space pawn {pawn?.Name}: {ex.Message}");
            }
        }
    }

    public class BuyPawnResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Pawn Pawn { get; }
        public IntVec3 DeliveryPosition { get; }   // NEW: Exact drop-pod / spawn location

        public BuyPawnResult(bool success, string message, Pawn pawn = null, IntVec3 deliveryPos = default)
        {
            Success = success;
            Message = message;
            Pawn = pawn;
            DeliveryPosition = deliveryPos;
        }
    }
}