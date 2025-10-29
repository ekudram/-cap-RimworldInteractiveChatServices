// Traits.cs
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive.Traits
{
    public static class TraitsManager
    {
        public static Dictionary<string, BuyableTrait> AllBuyableTraits { get; private set; } = new Dictionary<string, BuyableTrait>();
        private static bool isInitialized = false;
        private static readonly object lockObject = new object();

        //static Traits()
        //{
        //    // Initialize on first access
        //    InitializeTraits();
        //}

        public static void InitializeTraits()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                Logger.Debug("Initializing Traits System...");

                if (!LoadTraitsFromJson())
                {
                    CreateDefaultTraits();
                    SaveTraitsToJson();
                }
                else
                {
                    ValidateAndUpdateTraits();
                }

                isInitialized = true;
                Logger.Message($"[CAP] Traits System initialized with {AllBuyableTraits.Count} traits");
            }
        }

        private static bool LoadTraitsFromJson()
        {
            string jsonContent = JsonFileManager.LoadFile("Traits.json");
            if (string.IsNullOrEmpty(jsonContent))
                return false;

            try
            {
                var loadedTraits = JsonFileManager.DeserializeTraits(jsonContent);
                AllBuyableTraits.Clear();

                foreach (var kvp in loadedTraits)
                {
                    AllBuyableTraits[kvp.Key] = kvp.Value;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Logger.Error($"Error loading traits JSON: {e.Message}");
                return false;
            }
        }

        private static void CreateDefaultTraits()
        {
            AllBuyableTraits.Clear();

            var allTraitDefs = DefDatabase<TraitDef>.AllDefs.ToList();

            int traitsCreated = 0;
            foreach (var traitDef in allTraitDefs)
            {
                try
                {
                    if (traitDef.degreeDatas != null)
                    {
                        foreach (var degree in traitDef.degreeDatas)
                        {
                            string key = GetTraitKey(traitDef, degree.degree);
                            if (!AllBuyableTraits.ContainsKey(key))
                            {
                                var buyableTrait = new BuyableTrait(traitDef, degree);
                                AllBuyableTraits[key] = buyableTrait;
                                traitsCreated++;
                            }
                        }
                    }
                    else
                    {
                        string key = GetTraitKey(traitDef, 0);
                        if (!AllBuyableTraits.ContainsKey(key))
                        {
                            var buyableTrait = new BuyableTrait(traitDef);
                            AllBuyableTraits[key] = buyableTrait;
                            traitsCreated++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating buyable trait for {traitDef.defName}: {ex.Message}");
                }
            }
        }

        private static void ValidateAndUpdateTraits()
        {
            var allTraitDefs = DefDatabase<TraitDef>.AllDefs;
            int addedTraits = 0;
            int removedTraits = 0;

            // Add any new traits that aren't in our system
            foreach (var traitDef in allTraitDefs)
            {
                if (traitDef.degreeDatas != null)
                {
                    foreach (var degree in traitDef.degreeDatas)
                    {
                        string key = GetTraitKey(traitDef, degree.degree);
                        if (!AllBuyableTraits.ContainsKey(key))
                        {
                            var buyableTrait = new BuyableTrait(traitDef, degree);
                            AllBuyableTraits[key] = buyableTrait;
                            addedTraits++;
                        }
                    }
                }
                else
                {
                    string key = GetTraitKey(traitDef, 0);
                    if (!AllBuyableTraits.ContainsKey(key))
                    {
                        var buyableTrait = new BuyableTrait(traitDef);
                        AllBuyableTraits[key] = buyableTrait;
                        addedTraits++;
                    }
                }
            }

            // Remove traits that no longer exist in the game
            var keysToRemove = new List<string>();
            foreach (var kvp in AllBuyableTraits)
            {
                var traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(kvp.Value.DefName);
                if (traitDef == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
                else
                {
                    // Check if degree still exists
                    if (traitDef.degreeDatas != null)
                    {
                        bool degreeExists = traitDef.degreeDatas.Any(d => d.degree == kvp.Value.Degree);
                        if (!degreeExists)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                AllBuyableTraits.Remove(key);
                removedTraits++;
            }

            if (addedTraits > 0 || removedTraits > 0)
            {
                Logger.Message($"Traits updated: +{addedTraits} traits, -{removedTraits} traits");
                SaveTraitsToJson(); // Save changes
            }
        }

        private static string GetTraitKey(TraitDef traitDef, int degree)
        {
            return $"{traitDef.defName}_{degree}";
        }

        public static void SaveTraitsToJson()
        {
            LongEventHandler.QueueLongEvent(() =>
            {
                lock (lockObject)
                {
                    try
                    {
                        string jsonContent = JsonFileManager.SerializeTraits(AllBuyableTraits);
                        JsonFileManager.SaveFile("Traits.json", jsonContent);
                    }
                    catch (System.Exception e)
                    {
                        Logger.Error($"Error saving traits JSON: {e.Message}");
                    }
                }
            }, null, false, null, showExtraUIInfo: false, forceHideUI: true);
        }

        public static BuyableTrait GetBuyableTrait(string defName, int degree = 0)
        {
            string key = GetTraitKey(DefDatabase<TraitDef>.GetNamed(defName), degree);
            return AllBuyableTraits.TryGetValue(key, out BuyableTrait trait) ? trait : null;
        }

        public static IEnumerable<BuyableTrait> GetEnabledTraits()
        {
            return AllBuyableTraits.Values.Where(trait => trait.CanAdd || trait.CanRemove);
        }

        public static IEnumerable<BuyableTrait> GetTraitsByMod(string modName)
        {
            return GetEnabledTraits().Where(trait => trait.ModSource == modName);
        }

        public static IEnumerable<string> GetAllModSources()
        {
            return AllBuyableTraits.Values
                .Select(trait => trait.ModSource)
                .Distinct()
                .OrderBy(source => source);
        }
        public static (int total, int enabled, int disabled) GetTraitsStatistics()
        {
            int total = AllBuyableTraits.Count;
            int enabled = GetEnabledTraits().Count();
            int disabled = total - enabled;
            return (total, enabled, disabled);
        }
    }
}