using UnityEngine;
using Verse;

namespace CAP_ChatInteractive
{
    public static class Logger
    {
        private const string Prefix = "<color=#4A90E2>[CAP]</color>";

        public static void Message(string message)
        {
            Log.Message($"{Prefix} {message}");
        }

        public static void Warning(string message)
        {
            Log.Warning($"{Prefix} <color=#FFA500>{message}</color>");
        }

        public static void Error(string message)
        {
            Log.Error($"{Prefix} <color=#FF0000>{message}</color>");
        }

        public static void Debug(string message)
        {
            // Use settings-based debug toggle
            if (CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.EnableDebugLogging == true)
                Log.Message($"{Prefix} <color=#888888>[DEBUG] {message}</color>");
        }

        public static void Twitch(string message)
        {
            Log.Message($"{Prefix} <color=#9146FF>[Twitch]</color> {message}");
        }

        public static void YouTube(string message)
        {
            Log.Message($"{Prefix} <color=#FF0000>[YouTube]</color> {message}");
        }
    }
}