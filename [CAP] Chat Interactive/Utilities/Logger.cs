// Logger.cs
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
// This static class provides logging functionality with different log levels and color-coded messages for the CAP Chat Interactive
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