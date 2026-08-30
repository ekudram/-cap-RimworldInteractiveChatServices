// File: BuyPawnCommandHandler.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//
// Pawn purchase command handler
using _CAP__Chat_Interactive.Command.CommandHelpers;
using _CAP__Chat_Interactive.Utilities;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Commands.CommandHandlers
{
    /// <summary>
    /// Handles !pawn purchase and related pawn lookup for chat viewers.
    /// </summary>
    public static class BuyPawnCommandHandler
    {
        private const string ReturnDivider = " | ";

        public static string HandleBuyPawnCommand(ChatMessageWrapper messageWrapper, string[] args)
        {
            try
            {
                ParsePawnParameters(args ?? Array.Empty<string>(), out string raceName, out string xenotypeName, out string genderName, out string ageString);

                if (string.IsNullOrEmpty(raceName))
                {
                    Pawn blocking = FindBlockingAssignedPawn(messageWrapper);
                    if (blocking != null)
                        return "RICS.BPCH.AlreadyHasPawn".Translate(blocking.Name.ToStringFull);

                    var enabledRaces = RaceUtils.GetEnabledRaces()
                        .Where(r => r != null)
                        .OrderBy(r => r.LabelCap.RawText)
                        .ToList();

                    if (enabledRaces.Count == 0)
                        return "RICS.LCH.NoRacesEnabled".Translate();

                    if (enabledRaces.Count == 1)
                    {
                        raceName = enabledRaces[0].defName;
                    }
                    else
                    {
                        string list = RaceUtils.FormatEnabledRacesPriceList(8, out int shown, out int total);
                        if (total > shown)
                            list += ReturnDivider + "RICS.BPCH.RacesList.More".Translate(total - shown);
                        return list + ReturnDivider + "RICS.BPCH.PickARaceHint".Translate();
                    }
                }

                var raceDef = RaceUtils.FindRaceByName(raceName);
                if (raceDef == null)
                {
                    var similarRaces = RaceUtils.GetAllHumanlikeRaces()
                        .Where(r => r.defName.IndexOf(raceName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   r.label.IndexOf(raceName, StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(r => r.label)
                        .Take(3)
                        .ToList();

                    string errorMessage = "RICS.BPCH.RaceNotFound".Translate(raceName);
                    if (similarRaces.Any())
                        errorMessage += ReturnDivider + "RICS.BPCH.RaceNotFound.Suggestion".Translate(string.Join(", ", similarRaces));
                    else
                        errorMessage += ReturnDivider + "RICS.BPCH.RaceNotFound.UseList".Translate();
                    return errorMessage;
                }

                return HandleBuyPawnCommandInternal(messageWrapper, raceName, xenotypeName, genderName, ageString);
            }
            catch (Exception ex)
            {
                Logger.Error($"[BuyPawn] Error parsing pawn command: {ex}");
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

        private static string HandleBuyPawnCommandInternal(ChatMessageWrapper messageWrapper, string raceName, string xenotypeName = "Baseliner", string genderName = "Random", string ageString = "Random")
        {
            try
            {
                if (!IsGameReadyForPawnPurchase())
                    return "RICS.BPCH.GameNotReady".Translate();

                var settings = CAPChatInteractiveMod.Instance.Settings.GlobalSettings;
                var currencySymbol = settings.CurrencyName?.Trim() ?? "¢";
                var viewer = Viewers.GetViewer(messageWrapper);
                var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();

                Pawn existingPawn = FindBlockingAssignedPawn(messageWrapper);
                if (existingPawn != null)
                    return "RICS.BPCH.AlreadyHasPawn".Translate(existingPawn.Name.ToStringFull);

                if (!IsValidPawnRequest(raceName, xenotypeName, out RaceSettings raceSettings))
                {
                    if (raceSettings == null)
                        return "RICS.BPCH.RaceNotFound".Translate(raceName);
                    if (!raceSettings.Enabled)
                        return "RICS.BPCH.RaceDisabled".Translate(raceName);
                    return "RICS.BPCH.InvalidRaceRequest".Translate(raceName);
                }

                // Explicit ages must be in range (no silent clamp); random stays within range
                if (!TryResolveAge(ageString, raceSettings, out int age))
                {
                    return "RICS.BPCH.AgeOutOfRange".Translate(
                        raceSettings.MinAge, raceSettings.MaxAge, raceName);
                }

                // Xenotype: Biotech only; resolve once here (GenerateAndSpawnPawn uses final name as-is)
                string finalXenotypeName = "Baseliner";
                if (ModsConfig.BiotechActive)
                {
                    if (string.IsNullOrEmpty(xenotypeName) ||
                        xenotypeName.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!raceSettings.EnabledXenotypes.TryGetValue("Baseliner", out bool baselinerEnabled) || !baselinerEnabled)
                            finalXenotypeName = PickRandomEnabledXenotype(raceSettings, raceName);
                        else
                            finalXenotypeName = "Baseliner";
                    }
                    else
                    {
                        finalXenotypeName = GetXenotypeDefName(xenotypeName, raceSettings);
                    }

                    bool isEnabled = raceSettings.EnabledXenotypes.TryGetValue(finalXenotypeName, out bool enabled)
                        ? enabled
                        : raceSettings.AllowCustomXenotypes;

                    if (!isEnabled)
                        return "RICS.BPCH.XenotypeDisabled".Translate(xenotypeName, raceName);

                    if (!raceSettings.AllowCustomXenotypes && finalXenotypeName != "Baseliner")
                        return "RICS.BPCH.CustomXenotypesDisabled".Translate(raceName);
                }
                // else: no Biotech → Baseliner only (ignore user xenotype arg)

                int finalPrice = raceSettings.BasePrice;
                if (raceSettings.XenotypePrices.TryGetValue(finalXenotypeName, out float price))
                    finalPrice = (int)price;

                if (viewer.Coins < finalPrice)
                {
                    return "RICS.BPCH.InsufficientFunds"
                        .Translate(finalPrice, currencySymbol, raceName, viewer.Coins);
                }

                var result = GenerateAndSpawnPawn(messageWrapper.Username, raceName, finalXenotypeName, genderName, age, raceSettings);

                if (!result.Success)
                    return result.Message ?? "RICS.BPCH.Error.Purchase".Translate();

                viewer.TakeCoins(finalPrice);
                float karmaEarned = finalPrice * settings.KarmaPerStoreItem / 100f;
                if (karmaEarned > 0f)
                    viewer.GiveKarma(karmaEarned);

                if (result.Pawn != null && assignmentManager != null)
                    assignmentManager.AssignPawnToViewer(messageWrapper, result.Pawn);

                string locationInfo = "RICS.BPCH.Letter.Delivery.Unknown".Translate();
                if (result.DeliveryPosition.IsValid)
                {
                    string mapName = result.Pawn?.Map?.Parent?.LabelCap ?? "Home Map";
                    locationInfo = "RICS.BPCH.Letter.Delivery".Translate(
                        result.DeliveryPosition.x, result.DeliveryPosition.z, mapName);
                }
                else if (result.Pawn != null && result.Pawn.Map != null)
                {
                    IntVec3 pos = result.Pawn.PositionHeld;
                    string mapName = result.Pawn.Map.Parent?.LabelCap ?? "Home Map";
                    locationInfo = "RICS.BPCH.Letter.Delivery".Translate(pos.x, pos.z, mapName);
                }

                string xenotypeInfo = finalXenotypeName != "Baseliner" ? $" ({finalXenotypeName})" : "";
                string goldLetterTitle = "RICS.BPCH.Letter.Title".Translate(raceName);
                string goldLetterText = "RICS.BPCH.Letter.Text".Translate(
                    messageWrapper.Username,
                    raceName,
                    xenotypeInfo,
                    age.ToString(),
                    finalPrice.ToString("N0"),
                    currencySymbol,
                    result.Pawn?.Name.ToStringFull ?? "Unnamed",
                    locationInfo);

                MessageHandler.SendGoldLetter(goldLetterTitle, goldLetterText, new LookTargets(result.Pawn));

                return "RICS.BPCH.PurchaseSuccess".Translate(
                    raceName,
                    xenotypeInfo,
                    finalPrice.ToString("N0"),
                    currencySymbol,
                    result.Pawn?.Name.ToStringFull ?? "your new pawn"
                ) + ReturnDivider + locationInfo;
            }
            catch (Exception ex)
            {
                Logger.Error($"[BuyPawn] Error handling buy pawn command: {ex}");
                return "RICS.BPCH.Error.Purchase".Translate();
            }
        }

        /// <summary>Living pawn still belonging to the player blocks a new purchase.</summary>
        private static Pawn FindBlockingAssignedPawn(ChatMessageWrapper messageWrapper)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            Pawn existingPawn = assignmentManager?.GetAssignedPawn(messageWrapper);
            if (IsBlockingExistingPawn(existingPawn))
                return existingPawn;

            if (assignmentManager == null)
                return null;

            string platformId = $"{messageWrapper.Platform.ToLowerInvariant()}:{messageWrapper.PlatformUserId}";
            if (assignmentManager.viewerPawnAssignments.TryGetValue(platformId, out string thingId))
            {
                existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                if (IsBlockingExistingPawn(existingPawn))
                    return existingPawn;
            }

            string usernameLower = messageWrapper.Username.ToLowerInvariant();
            if (assignmentManager.viewerPawnAssignments.TryGetValue(usernameLower, out thingId))
            {
                existingPawn = GameComponent_PawnAssignmentManager.FindPawnByThingId(thingId);
                if (IsBlockingExistingPawn(existingPawn))
                    return existingPawn;
            }

            return null;
        }

        /// <summary>Living pawn still belonging to the player blocks a new purchase.</summary>
        private static bool IsBlockingExistingPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;
            try
            {
                return pawn.Faction == Faction.OfPlayer || pawn.Faction?.IsPlayer == true;
            }
            catch
            {
                return pawn.Spawned;
            }
        }

        /// <param name="xenotypeName">Already-resolved final xenotype defName (or Baseliner). Do not re-auto-pick here.</param>
        private static BuyPawnResult GenerateAndSpawnPawn(string username, string raceName, string xenotypeName, string genderName, int age, RaceSettings raceSettings)
        {
            try
            {
                Map map = ItemDeliveryHelper.ResolveDeliveryMap(anchorPawn: null, allowUndergroundRedirect: true);
                if (map == null)
                    return new BuyPawnResult(false, "RICS.BPCH.NoHomeMap".Translate());

                var pawnKindDef = GetPawnKindDefForRace(raceName);
                if (pawnKindDef == null)
                    return new BuyPawnResult(false, "RICS.BPCH.PawnKindNotFound".Translate(raceName));

                // Resolve XenotypeDef for generator (name already finalized by caller)
                XenotypeDef xenotypeDef = null;
                string resolvedXenotype = string.IsNullOrEmpty(xenotypeName) ? "Baseliner" : xenotypeName;
                if (ModsConfig.BiotechActive && !resolvedXenotype.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                {
                    xenotypeDef = DefDatabase<XenotypeDef>.GetNamedSilentFail(resolvedXenotype)
                                  ?? DefDatabase<XenotypeDef>.AllDefs
                                      .FirstOrDefault(x => x.label.Equals(resolvedXenotype, StringComparison.OrdinalIgnoreCase));
                }

                var raceDef = RaceUtils.FindRaceByName(raceName);
                if (raceDef == null)
                    return new BuyPawnResult(false, "RICS.BPCH.RaceDefNotFound".Translate(raceName));

                Gender? fixedGender = ParseGender(genderName);
                if (fixedGender.HasValue)
                {
                    if (raceSettings != null && !IsGenderAllowed(raceSettings.AllowedGenders, fixedGender.Value))
                    {
                        string allowedText = GetAllowedGendersDescription(raceSettings.AllowedGenders);
                        return new BuyPawnResult(false, "RICS.BPCH.GenderNotAllowed".Translate(raceName, allowedText));
                    }
                }
                else
                {
                    fixedGender = PickRandomAllowedGender(raceSettings?.AllowedGenders);
                }

                // forceNoGear=false: normal starting clothes; dontGiveWeapon=true: clean delivery
                var request = new PawnGenerationRequest(
                    kind: pawnKindDef,
                    faction: Faction.OfPlayer,
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: false,
                    colonistRelationChanceFactor: 0f,
                    forceAddFreeWarmLayerIfNeeded: true,
                    allowGay: true,
                    allowPregnant: false,
                    allowFood: true,
                    allowAddictions: true,
                    inhabitant: false,
                    certainlyBeenInCryptosleep: false,
                    forceRedressWorldPawnIfFormerColonist: false,
                    worldPawnFactionDoesntMatter: false,
                    biocodeWeaponChance: 0f,
                    biocodeApparelChance: 0f,
                    fixedBiologicalAge: age,
                    fixedChronologicalAge: null,
                    fixedGender: fixedGender,
                    fixedLastName: null,
                    forceNoIdeo: false,
                    forceNoBackstory: false,
                    forbidAnyTitle: false,
                    forceDead: false,
                    forcedXenotype: xenotypeDef,
                    forceBaselinerChance: 0f,
                    developmentalStages: DevelopmentalStage.Adult,
                    forceNoGear: false,
                    dontGiveWeapon: true,
                    onlyUseForcedBackstories: false,
                    maximumAgeTraits: -1,
                    minimumAgeTraits: 0
                );

                Pawn pawn = PawnGenerator.GeneratePawn(request);
                if (pawn == null)
                {
                    Logger.Error("[BuyPawn] PawnGenerator returned null");
                    return new BuyPawnResult(false, "RICS.BPCH.GenerationError".Translate("PawnGenerator returned null"));
                }

                if (pawn.Name is NameTriple nameTriple)
                    pawn.Name = new NameTriple(nameTriple.First, username, nameTriple.Last);
                else
                    pawn.Name = new NameSingle(username);

                if (!ItemDeliveryHelper.TryDeliverGeneratedPawn(pawn, map, out IntVec3 deliveryPos))
                {
                    Logger.Error("[BuyPawn] All spawn strategies failed for purchased pawn");
                    return new BuyPawnResult(false, "RICS.BPCH.SpawnLocationNotFound".Translate());
                }

                return new BuyPawnResult(true, "RICS.BPCH.PawnGenerated".Translate(), pawn, deliveryPos);
            }
            catch (Exception ex)
            {
                Logger.Error($"[BuyPawn] Error generating pawn: {ex}");
                return new BuyPawnResult(false, "RICS.BPCH.GenerationError".Translate(ex.Message));
            }
        }

        /// <summary>
        /// Attempts to find the most appropriate PawnKindDef for a given
        /// </summary>
        /// <param name="raceName"></param>
        /// <returns></returns>
        /// <summary>Best PawnKindDef for race; null if missing (never force Human Colonist on alien race).</summary>
        public static PawnKindDef GetPawnKindDefForRace(string raceName)
        {
            var raceDef = RaceUtils.FindRaceByName(raceName);
            if (raceDef == null)
            {
                Logger.Warning($"[BuyPawn] Race not found for pawn kind lookup: {raceName}");
                return null;
            }

            var playerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef && IsPlayerFactionPawnKind(pk))
                .ToList();
            if (playerPawnKinds.Any())
            {
                return playerPawnKinds.FirstOrDefault(pk =>
                           pk.defName.Contains("Colonist") || pk.defName.Contains("Player"))
                       ?? playerPawnKinds[0];
            }

            var factionPlayerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef && pk.defaultFactionDef != null && pk.defaultFactionDef.isPlayer)
                .ToList();
            if (factionPlayerPawnKinds.Any())
                return factionPlayerPawnKinds[0];

            var namedPlayerPawnKinds = DefDatabase<PawnKindDef>.AllDefs
                .Where(pk => pk.race == raceDef && IsLikelyPlayerPawnKind(pk))
                .ToList();
            if (namedPlayerPawnKinds.Any())
                return namedPlayerPawnKinds[0];

            var anyPawnKind = DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(pk => pk.race == raceDef);
            if (anyPawnKind != null)
                return anyPawnKind;

            Logger.Warning($"[BuyPawn] No pawn kind found for race: {raceDef.defName}");
            return null;
        }

        /// <summary>
        /// Determines if the given pawn kind is associated with a player faction.
        /// </summary>
        /// <param name="pawnKind"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Heuristic check to determine if a PawnKindDef is likely intended for player/colonist use based on naming patterns and combat power.
        /// </summary>
        /// <param name="pawnKind"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Validates the pawn request based on race
        /// </summary>
        /// <param name="raceDefName"></param>
        /// <param name="xenotypeName"></param>
        /// <param name="raceSettings"></param>
        /// <returns></returns>
        private static bool IsValidPawnRequest(string raceDefName, string xenotypeName, out RaceSettings raceSettings)
        {
            raceSettings = null;
            var raceDef = RaceUtils.FindRaceByName(raceDefName);
            if (raceDef == null)
                return false;

            raceSettings = RaceSettingsManager.GetRaceSettings(raceDef.defName);
            if (!raceSettings.Enabled)
                return false;

            if (!string.IsNullOrEmpty(xenotypeName) && xenotypeName != "Baseliner" && ModsConfig.BiotechActive)
            {
                if (!IsXenotypeAllowed(raceSettings, xenotypeName))
                    return false;
            }

            return true;
        }

        private static bool IsXenotypeAllowed(RaceSettings raceSettings, string xenotypeInput)
        {
            string xenoDefName = GetXenotypeDefName(xenotypeInput, raceSettings);

            if (raceSettings.EnabledXenotypes == null)
                raceSettings.EnabledXenotypes = new Dictionary<string, bool>();
            if (raceSettings.XenotypePrices == null)
                raceSettings.XenotypePrices = new Dictionary<string, float>();

            if (raceSettings.EnabledXenotypes.ContainsKey(xenoDefName))
                return raceSettings.EnabledXenotypes[xenoDefName];

            if (raceSettings.EnabledXenotypes.Count > 0)
            {
                if (IsCustomXenotype(xenotypeInput, raceSettings))
                    return raceSettings.AllowCustomXenotypes;
                return false;
            }

            if (IsCustomXenotype(xenotypeInput, raceSettings) && !raceSettings.AllowCustomXenotypes)
                return false;

            return true;
        }

        /// <summary>
        /// Determines if the given xenotype input is a custom xenotype (not in DefDatabase
        /// </summary>
        /// <param name="input"></param>
        /// <param name="raceSettings"></param>
        /// <returns></returns>
        private static bool IsCustomXenotype(string input, RaceSettings raceSettings)
        {

            string defName = GetXenotypeDefName(input, raceSettings);
            return DefDatabase<XenotypeDef>.AllDefs.FirstOrDefault(x =>
                x.defName.Equals(defName, StringComparison.OrdinalIgnoreCase)) == null;
        }

        /// <summary>
        /// Checks if the game is in a state where a pawn can be purchased and spawned
        /// </summary>
        /// <returns></returns>
        private static bool IsGameReadyForPawnPurchase()
        {
            // Ready when the shared delivery pipeline can pick a map (home, current, or surface redirect)
            return Current.Game != null &&
                   Current.ProgramState == ProgramState.Playing &&
                   ItemDeliveryHelper.ResolveDeliveryMap(anchorPawn: null, allowUndergroundRedirect: true) != null;
        }

        /// <summary>
        /// Random → in-range random. Explicit number → no clamp (false if out of range).
        /// Unparseable → random.
        /// </summary>
        private static bool TryResolveAge(string ageString, RaceSettings raceSettings, out int age)
        {
            age = raceSettings.MinAge;
            if (string.IsNullOrEmpty(ageString) || ageString.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                age = Rand.Range(raceSettings.MinAge, raceSettings.MaxAge + 1);
                return true;
            }

            if (!int.TryParse(ageString, out age))
            {
                age = Rand.Range(raceSettings.MinAge, raceSettings.MaxAge + 1);
                return true;
            }

            return age >= raceSettings.MinAge && age <= raceSettings.MaxAge;
        }

        /// <summary>
        /// Parses a gender string into a Gender enum value
        /// </summary>
        /// <param name="genderName"></param>
        /// <returns></returns>
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

        private static Gender? PickRandomAllowedGender(AllowedGenders allowed)
        {
            if (allowed == null)
                return null;

            var options = new List<Gender>();
            if (allowed.AllowMale)
                options.Add(Gender.Male);
            if (allowed.AllowFemale)
                options.Add(Gender.Female);
            if (allowed.AllowOther)
                options.Add(Gender.None);

            if (options.Count == 0)
                return null;
            return options.RandomElement();
        }

        /// <summary>
        /// Checks if the specified gender is allowed
        /// </summary>
        /// <param name="allowedGenders"></param>
        /// <param name="gender"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns a human-readable description
        /// </summary>
        /// <param name="allowedGenders"></param>
        /// <returns></returns>
        private static string GetAllowedGendersDescription(AllowedGenders allowedGenders)
        {
            if (!allowedGenders.AllowMale && !allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "RICS.BPCH.Gender.None".Translate();
            if (allowedGenders.AllowMale && !allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "RICS.BPCH.Gender.OnlyMale".Translate();
            if (!allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "RICS.BPCH.Gender.OnlyFemale".Translate();
            if (allowedGenders.AllowMale && allowedGenders.AllowFemale && !allowedGenders.AllowOther)
                return "RICS.BPCH.Gender.MaleOrFemale".Translate();
            return "RICS.BPCH.Gender.Any".Translate();
        }

        private static string GetXenotypeDefName(string input, RaceSettings raceSettings)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Equals("Baseliner", StringComparison.OrdinalIgnoreCase))
                return "Baseliner";

            string clean = input.Trim();
            if (raceSettings?.EnabledXenotypes == null)
                return clean;

            var exact = raceSettings.EnabledXenotypes.Keys
                .FirstOrDefault(k => k.Equals(clean, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            var fuzzy = raceSettings.EnabledXenotypes.Keys
                .Where(k => k.ToLowerInvariant().Contains(clean.ToLowerInvariant()) ||
                            clean.ToLowerInvariant().Contains(k.ToLowerInvariant()))
                .OrderBy(k => Math.Abs(k.Length - clean.Length))
                .FirstOrDefault();

            return fuzzy ?? clean;
        }

        /// <summary>
        /// Attempts to find the best matching race for the given potential race arguments.
        /// </summary>
        /// <param name="potentialRaceArgs"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Picks a random enabled xenotype from the race settings, excluding
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="raceDefName"></param>
        /// <returns></returns>
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
                    return preferred;
            }

            return enabled.RandomElement();
        }

        public static string HandleMyPawnCommand(ChatMessageWrapper messageWrapper)
        {
            var assignmentManager = CAPChatInteractiveMod.GetPawnAssignmentManager();
            var pawn = assignmentManager?.GetAssignedPawn(messageWrapper);

            if (pawn == null)
                return "RICS.Pawn.NoPawn".Translate();

            if (pawn.Dead)
            {
                string deathDetails;
                try
                {
                    deathDetails = GameComponent_PawnAssignmentManager.GetPawnDeathInfo(pawn).ToString();
                }
                catch
                {
                    deathDetails = "deceased";
                }
                return "RICS.Pawn.Dead".Translate()
                       + ReturnDivider
                       + "RICS.Return.PawnDeadReason".Translate(deathDetails);
            }

            string status = pawn.Spawned
                ? "RICS.BPCH.MyPawn.Status.AliveAndInColony".Translate()
                : "RICS.BPCH.MyPawn.Status.AliveNotInColony".Translate();
            string health = pawn.health.summaryHealth.SummaryHealthPercent.ToStringPercent();
            int traitCount = pawn.story?.traits?.allTraits?.Count ?? 0;
            int maxTraits = CAPChatInteractiveMod.Instance.Settings.GlobalSettings?.MaxTraits ?? 4;

            return "RICS.BPCH.MyPawn.HasPawn".Translate(
                pawn.Name.ToString(),
                status,
                health,
                pawn.ageTracker.AgeBiologicalYears.ToString(),
                traitCount.ToString(),
                maxTraits.ToString());
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
