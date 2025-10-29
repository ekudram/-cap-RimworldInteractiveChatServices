// Settings/ChatWindowSettings.cs
using RimWorld;
using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public class ChatWindowSettings : IExposable
    {
        public Vector2 DefaultSize = new Vector2(400f, 300f);
        public bool AlwaysOnTop = false;
        public float Opacity = 0.9f;
        public bool ShowTimestamps = false;
        public int MaxMessageHistory = 1000;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DefaultSize, "defaultSize", new Vector2(400f, 300f));
            Scribe_Values.Look(ref AlwaysOnTop, "alwaysOnTop", false);
            Scribe_Values.Look(ref Opacity, "opacity", 0.9f);
            Scribe_Values.Look(ref ShowTimestamps, "showTimestamps", false);
            Scribe_Values.Look(ref MaxMessageHistory, "maxMessageHistory", 1000);
        }
    }
}