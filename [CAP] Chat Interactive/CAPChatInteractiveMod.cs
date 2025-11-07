// CAPChatInteractiveMod.cs
// Copyright (c) Captolamia. All rights reserved.
// Licensed under the AGPLv3 License. See LICENSE file in the project root for full license information.
// Main mod class for [CAP] Chat Interactive RimWorld mod
// Handles initialization, settings, and service management.
// Store, Traits, Weather, and other systems will be initialized when the game starts.
using _CAP__Chat_Interactive.Interfaces;
using _CAP__Chat_Interactive.Utilities;
using Google.Apis.YouTube.v3;
using LudeonTK;
using RimWorld;
using System;   
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class CAPChatInteractiveMod : Mod
    {
        public static CAPChatInteractiveMod Instance { get; private set; }
        public CAPChatInteractiveSettings Settings { get; private set; }
        public IAlienCompatibilityProvider AlienProvider { get; private set; }

        // Service managers (we'll create these later)
        private TwitchService _twitchService;
        private YouTubeChatService _youTubeService;


        public CAPChatInteractiveMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Logger.Debug("CAPChatInteractiveMod constructor started");

            Settings = GetSettings<CAPChatInteractiveSettings>();

            // Force GameComponent creation if a game is already running
            if (Current.Game != null && Current.Game.components != null)
            {
                var existingComponent = Current.Game.GetComponent<CAPChatInteractive_GameComponent>();
                if (existingComponent == null)
                {
                    Current.Game.components.Add(new CAPChatInteractive_GameComponent(Current.Game));
                    Logger.Debug("GameComponent added to existing game");
                }
            }

            Logger.Message("[CAP] Chat Interactive mod loaded successfully!");
            // Force viewer loading by accessing the All property
            var viewerCount = Viewers.All.Count; // This triggers static constructor
            Logger.Debug($"Pre-loaded {viewerCount} viewers");

            // Register commands from XML Defs first
            RegisterDefCommands();

            // Then initialize services (which will use the registered commands)
            InitializeServices();

            Logger.Debug("CAPChatInteractiveMod constructor completed");
        }

        private void RegisterDefCommands()
        {
            Logger.Debug("Registering commands from Defs...");

            // Register all ChatCommandDefs with the processor
            foreach (var commandDef in DefDatabase<ChatCommandDef>.AllDefs)
            {
                commandDef.RegisterCommand();
            }

            Logger.Message($"Registered {DefDatabase<ChatCommandDef>.AllDefsListForReading.Count} commands from Defs");
        }

        private void InitializeServices()
        {
            Logger.Debug("InitializeServices started");

            _twitchService = new TwitchService(Settings.TwitchSettings);
            // Logger.Debug($"TwitchService created. AutoConnect: {Settings.TwitchSettings.AutoConnect}, CanConnect: {Settings.TwitchSettings.CanConnect}");
            _youTubeService = new YouTubeChatService(Settings.YouTubeSettings);

            // Auto-connect if configured
            if (Settings.TwitchSettings.AutoConnect && Settings.TwitchSettings.CanConnect)
            {
                Logger.Debug("Auto-connecting to Twitch at startup");
                _twitchService.Connect();
            }
            else
            {
                Logger.Debug($"Skipping auto-connect - AutoConnect: {Settings.TwitchSettings.AutoConnect}, CanConnect: {Settings.TwitchSettings.CanConnect}");
            }

            if (Settings.YouTubeSettings.AutoConnect && Settings.YouTubeSettings.CanConnect)
            {
                _youTubeService.Connect();
            }

            Logger.Debug("InitializeServices completed");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Close the original mod settings window and open our custom one
            Find.WindowStack.TryRemove(typeof(Dialog_ModSettings), true);
            Find.WindowStack.Add(new Dialog_ChatInteractiveSettings());
        }

        public object GetChatService(string platform)
        {
            return platform?.ToLowerInvariant() switch
            {
                "twitch" => _twitchService,
                "youtube" => _youTubeService,
                _ => null
            };
        }
        public override string SettingsCategory() => "[CAP] Chat Interactive";

        // Public access to services for other parts of your mod
        public TwitchService TwitchService => _twitchService;
        public YouTubeChatService YouTubeService => _youTubeService;

        public override void WriteSettings()
        {
            base.WriteSettings();
            // Store will be initialized when game starts
        }

        public static GameComponent_PawnAssignmentManager GetPawnAssignmentManager()
        {
            return Current.Game?.GetComponent<GameComponent_PawnAssignmentManager>();
        }
    }

    // Add this to GeneUtils.cs or your main mod class
    [StaticConstructorOnStartup]
    public static class GeneDebugActions
    {
        static GeneDebugActions()
        {
            // Auto-register debug actions on game start
        }

        [DebugAction("CAP", "Analyze Xenotype Pricing", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugAnalyzeXenotypePricing()
        {
            var options = new List<DebugMenuOption>();

            foreach (var xenotype in DefDatabase<XenotypeDef>.AllDefs)
            {
                options.Add(new DebugMenuOption(xenotype.defName, DebugMenuOptionMode.Action, () =>
                {
                    AnalyzeAndLogXenotypePricing(xenotype);
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        [DebugAction("CAP", "Recalculate All Xenotype Prices", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugRecalculateXenotypePrices()
        {
            // Recalculate and update all xenotype prices in race settings
            var raceSettings = JsonFileManager.LoadRaceSettings();
            bool updated = false;

            foreach (var raceSetting in raceSettings.Values)
            {
                foreach (var xenotypeKey in raceSetting.XenotypePrices.Keys.ToList())
                {
                    float newPrice = GeneUtils.CalculateXenotypeGeneCost(xenotypeKey);
                    if (newPrice != raceSetting.XenotypePrices[xenotypeKey])
                    {
                        raceSetting.XenotypePrices[xenotypeKey] = newPrice;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                JsonFileManager.SaveFile("RaceSettings.json", JsonFileManager.SerializeRaceSettings(raceSettings));
                Messages.Message("Xenotype prices updated with gene-based pricing", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message("No xenotype price changes needed", MessageTypeDefOf.NeutralEvent);
            }
        }

        [DebugAction("CAP", "Show Xenotype Gene Details", allowedGameStates = AllowedGameStates.Playing)]
        public static void DebugShowXenotypeGeneDetails()
        {
            var options = new List<DebugMenuOption>();

            foreach (var xenotype in DefDatabase<XenotypeDef>.AllDefs)
            {
                options.Add(new DebugMenuOption(xenotype.defName, DebugMenuOptionMode.Action, () =>
                {
                    ShowXenotypeGeneDetails(xenotype);
                }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private static void AnalyzeAndLogXenotypePricing(XenotypeDef xenotype)
        {
            float geneCost = GeneUtils.CalculateXenotypeGeneCost(xenotype.defName);
            var genes = GeneUtils.GetXenotypeGenes(xenotype.defName);
            var geneDetails = GeneUtils.GetXenotypeGeneDetails(xenotype.defName);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"=== {xenotype.defName} Pricing Analysis ===");
            sb.AppendLine($"Final Multiplier: {geneCost:F2}x");
            sb.AppendLine($"Total Genes: {genes.Count}");
            sb.AppendLine($"Gene Summary: {GeneUtils.GetXenotypeGeneSummary(xenotype.defName)}");
            sb.AppendLine();
            sb.AppendLine("Gene Details:");
            foreach (var detail in geneDetails)
            {
                sb.AppendLine($"  {detail}");
            }

            Log.Message(sb.ToString());
            Messages.Message($"Check log for {xenotype.defName} pricing details", MessageTypeDefOf.NeutralEvent);
        }

        private static void ShowXenotypeGeneDetails(XenotypeDef xenotype)
        {
            var geneDetails = GeneUtils.GetXenotypeGeneDetails(xenotype.defName);
            float geneCost = GeneUtils.CalculateXenotypeGeneCost(xenotype.defName);

            string message = $"{xenotype.defName} - Final Price: {geneCost:F2}x\n\n";
            message += string.Join("\n", geneDetails);

            Find.WindowStack.Add(new Dialog_MessageBox(message, "OK", null, null, null, "Xenotype Gene Details"));
        }
    }
}