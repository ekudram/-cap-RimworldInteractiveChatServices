// BuyableWeather.cs
using RimWorld;
using Verse;

namespace CAP_ChatInteractive.Incidents
{
    public class BuyableWeather
    {
        public string DefName { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }

        // Purchase settings
        public int BaseCost { get; set; } = 200;
        public string KarmaType { get; set; } = "Neutral";
        public int EventCap { get; set; } = 3;
        public bool Enabled { get; set; } = true;

        // Additional data
        public string ModSource { get; set; } = "RimWorld";
        public int Version { get; set; } = 1;

        public BuyableWeather() { }

        public BuyableWeather(WeatherDef weatherDef)
        {
            DefName = weatherDef.defName;
            Label = weatherDef.label;
            Description = weatherDef.description; // $"Change weather to {weatherDef.label}";
            ModSource = weatherDef.modContentPack?.Name ?? "RimWorld";

            SetDefaultPricing(weatherDef);
            Logger.Debug($"Created weather: {DefName}, Label: {Label}, Cost: {BaseCost}");
        }

        private void SetDefaultPricing(WeatherDef weatherDef)
        {
            string defName = weatherDef.defName.ToLower();

            // Doom weather types - most expensive and destructive
            if (defName.Contains("tox") || defName.Contains("blood") || defName.Contains("vomit") ||
                defName.Contains("doom") || defName.Contains("cataclysm"))
            {
                BaseCost = 600; // 2x storm pricing
                KarmaType = "Doom";
            }
            // Major storms and extreme weather
            else if (defName.Contains("hurricane") || defName.Contains("tornado") ||
                     defName.Contains("catastrophe") || defName.Contains("blizzard") ||
                     defName.Contains("torrential") || defName.Contains("storm"))
            {
                BaseCost = 300;
                KarmaType = "Bad";
            }
            // Cold weather types
            else if (defName.Contains("snow"))
            {
                BaseCost = 200;
                KarmaType = isHeavySnow(defName) ? "Bad" : "Neutral";
            }
            // Precipitation and reduced visibility
            else if (defName.Contains("rain") || defName.Contains("fog"))
            {
                BaseCost = 150;
                KarmaType = "Neutral";
            }
            // Clear weather - beneficial
            else if (defName.Contains("clear") || defName.Contains("sunny"))
            {
                BaseCost = 100;
                KarmaType = "Good";
            }
            // Default for unclassified weather
            else
            {
                BaseCost = 175;
                KarmaType = "Neutral";
            }
        }

        private bool isHeavySnow(string defName)
        {
            return defName.Contains("hard") || defName.Contains("heavy");
        }
    }
    public class TemperatureVariant
    {
        public string BaseWeatherDefName { get; set; }
        public string ColdVariantDefName { get; set; }
        public float ThresholdTemperature { get; set; } = 0f;
    }
}