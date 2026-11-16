// File: Logger.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//

using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace CAP_ChatInteractive
{
    public static class Logger
    {
        private const string Prefix = "<color=#4A90E2>[RICS]</color>";
        private const string DebugPrefix = "<color=#FF7518>[DEBUG]</color>";

        /// <summary>Filled once the active mod list can be read. Later errors reuse it.</summary>
        private static string cachedAddonList;

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
            // Context stays on the same error so a pasted line shows the game and the RICS addons.
            Log.Error($"{Prefix} <color=#FF0000>{message}\n{BuildErrorContext()}</color>");
        }

        public static void Debug(string message)
        {
            // Use settings-based debug toggle
            if (CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.EnableDebugLogging == true)
            {
                // Colored method
                //Log.Message($"{Prefix} {DebugPrefix} <color=#FF5F1F>{message}</color>");
                // No color method
                Log.Message($"[RICS][DEBUG] {message}");  // No color for debug messages version
            }
        }

        public static void Twitch(string message)
        {
            Log.Message($"{Prefix} <color=#9146FF>[Twitch]</color> {message}");
        }

        public static void YouTube(string message)
        {
            Log.Message($"{Prefix} <color=#FF0000>[YouTube]</color> {message}");
        }

        /// <summary>
        /// About.xml version, RimWorld version, and loaded mods that require RICS.
        /// Must not call <see cref="Error"/> or it would recurse.
        /// </summary>
        private static string BuildErrorContext()
        {
            try
            {
                return "RICS " + RicsModVersion()
                    + " | RimWorld " + RimWorldVersion()
                    + " | Addons: " + RicsAddonList();
            }
            catch (Exception)
            {
                return "RICS unknown | RimWorld unknown | Addons: unknown";
            }
        }

        /// <summary>Mod info version from About.xml. Falls back to the chat version string.</summary>
        private static string RicsModVersion()
        {
            ModMetaData meta = FindRicsMeta();
            if (meta != null && !string.IsNullOrEmpty(meta.ModVersion))
            {
                string id = string.IsNullOrEmpty(meta.PackageIdPlayerFacing)
                    ? meta.PackageId
                    : meta.PackageIdPlayerFacing;
                return string.IsNullOrEmpty(id)
                    ? meta.ModVersion
                    : meta.ModVersion + " (" + id + ")";
            }

            string settingsVersion = CAPChatInteractiveMod.Instance?.Settings?.GlobalSettings?.modVersion;
            return string.IsNullOrEmpty(settingsVersion) ? "unknown" : settingsVersion;
        }

        private static ModMetaData FindRicsMeta()
        {
            ModContentPack content = CAPChatInteractiveMod.Instance?.Content;
            if (content?.ModMetaData != null)
                return content.ModMetaData;

            ModMetaData beta = ModLister.GetActiveModWithIdentifier("Captolamia.RICS.Beta", true);
            if (beta != null)
                return beta;

            return ModLister.GetActiveModWithIdentifier("Captolamia.RICS", true);
        }

        private static string RimWorldVersion()
        {
            string withRev = VersionControl.CurrentVersionStringWithRev;
            if (!string.IsNullOrEmpty(withRev))
                return withRev;

            string plain = VersionControl.CurrentVersionString;
            return string.IsNullOrEmpty(plain) ? "unknown" : plain;
        }

        /// <summary>
        /// Active mods that depend on RICS, or load after the release or dev package.
        /// Own addons use load-after because one hard dependency cannot name both package ids.
        /// </summary>
        private static string RicsAddonList()
        {
            if (cachedAddonList != null)
                return cachedAddonList;

            var names = new List<string>();
            bool sawAnyMod = false;
            foreach (ModMetaData mod in ModsConfig.ActiveModsInLoadOrder)
            {
                if (mod == null)
                    continue;

                sawAnyMod = true;
                if (IsRicsPackage(mod.PackageId) || !RequiresRics(mod))
                    continue;

                names.Add(FormatAddon(mod));
            }

            if (!sawAnyMod)
                return "unknown";

            cachedAddonList = names.Count == 0 ? "none" : string.Join(", ", names.ToArray());
            return cachedAddonList;
        }

        private static string FormatAddon(ModMetaData mod)
        {
            var label = new StringBuilder();
            label.Append(string.IsNullOrEmpty(mod.Name) ? mod.PackageId : mod.Name);
            if (!string.IsNullOrEmpty(mod.ModVersion))
            {
                label.Append(' ');
                label.Append(mod.ModVersion);
            }

            string id = string.IsNullOrEmpty(mod.PackageIdPlayerFacing)
                ? mod.PackageId
                : mod.PackageIdPlayerFacing;
            if (!string.IsNullOrEmpty(id))
            {
                label.Append(" (");
                label.Append(id);
                label.Append(')');
            }

            return label.ToString();
        }

        private static bool RequiresRics(ModMetaData mod)
        {
            if (DependencyListNamesRics(mod.Dependencies))
                return true;

            // Storyteller, Research Vote, the chatbot addon, and Personal Storage
            // name RICS here instead of in modDependencies.
            if (IdListNamesRics(mod.LoadAfter))
                return true;

            return IdListNamesRics(mod.ForceLoadAfter);
        }

        private static bool DependencyListNamesRics(List<ModDependency> deps)
        {
            if (deps == null)
                return false;

            for (int i = 0; i < deps.Count; i++)
            {
                ModDependency dep = deps[i];
                if (dep == null)
                    continue;

                if (IsRicsPackage(dep.packageId))
                    return true;

                if (IdListNamesRics(dep.alternativePackageIds))
                    return true;
            }

            return false;
        }

        private static bool IdListNamesRics(List<string> ids)
        {
            if (ids == null)
                return false;

            for (int i = 0; i < ids.Count; i++)
            {
                if (IsRicsPackage(ids[i]))
                    return true;
            }

            return false;
        }

        /// <summary>Release and dev package ids, with or without the Steam postfix.</summary>
        private static bool IsRicsPackage(string packageId)
        {
            string id = NormalizePackageId(packageId);
            return id == "captolamia.rics" || id == "captolamia.rics.beta";
        }

        private static string NormalizePackageId(string packageId)
        {
            if (string.IsNullOrEmpty(packageId))
                return string.Empty;

            string id = packageId.Trim().ToLowerInvariant();
            const string steam = "_steam";
            if (id.EndsWith(steam, StringComparison.Ordinal))
                id = id.Substring(0, id.Length - steam.Length);

            return id;
        }
    }
}
