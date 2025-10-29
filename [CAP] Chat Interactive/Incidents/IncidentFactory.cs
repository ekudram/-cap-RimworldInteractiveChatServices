/*
// IncidentFactory.cs
using CAP_ChatInteractive.Incidents;
using RimWorld;
using Verse;
using System.Linq;

public static class IncidentFactory
{
    public static GenericIncidentHelper CreateIncidentHelper(string incidentDefName, string customMessage = null)
    {
        var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(incidentDefName);
        if (incidentDef == null) return null;

        // Check if it's a weather incident
        if (IsWeatherIncident(incidentDef))
        {
            var weatherDef = DefDatabase<WeatherDef>.GetNamedSilentFail(incidentDefName);
            if (weatherDef != null)
            {
                // Create WeatherIncidentHelper with the correct constructor pattern
                var helper = new WeatherIncidentHelper();
                helper.SetIncidentDef(incidentDef); // Use existing setter method
                helper.SetWeatherDef(weatherDef);   // Use existing setter method
                helper.CustomMessage = customMessage;
                return helper;
            }
        }

        // Check if it's a game condition incident
        if (IsGameConditionIncident(incidentDef))
        {
            // If GameConditionIncidentHelper exists, create it similarly
            // For now, fall back to GenericIncidentHelper
            var helper = new GenericIncidentHelper();
            helper.SetIncidentDef(incidentDef);
            helper.CustomMessage = customMessage;
            return helper;
        }

        // Default generic handler
        var genericHelper = new GenericIncidentHelper();
        genericHelper.SetIncidentDef(incidentDef);
        genericHelper.CustomMessage = customMessage;
        return genericHelper;
    }

    private static bool IsWeatherIncident(IncidentDef def)
    {
        string defName = def.defName.ToLower();
        return defName.Contains("weather") ||
               DefDatabase<WeatherDef>.AllDefs.Any(w => w.defName == def.defName);
    }

    private static bool IsGameConditionIncident(IncidentDef def)
    {
        return def.Worker is IncidentWorker_MakeGameCondition;
    }
}
*/