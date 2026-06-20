// BackupUtility.cs
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive. aka Rimworld Interactive Chat Service (RICS)
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
// Reusable utility for per-editor JSON backups with named saves, timestamped quick saves,
// and automatic pruning. Supports separate subfolders under Config/CAP_ChatInteractive/Backups/<EditorName>/
// for CommandManager, Store, Traits, Incidents, Weather, RaceSettings, etc.
// This keeps theme-specific backups (e.g. "Grimwar.json", "RimMagic.json") organized and separate from main settings backups.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace CAP_ChatInteractive
{
    public static class BackupUtility
    {
        /// <summary>
        /// Gets or creates the backup subfolder for a specific editor (e.g. "CommandManager", "StoreEditor").
        /// All named and timestamped backups for that editor go here.
        /// Main settings backups stay in the root Backups folder.
        /// </summary>
        public static string GetEditorBackupFolder(string editorKey)
        {
            if (string.IsNullOrEmpty(editorKey))
                editorKey = "General";

            string rootBackups = Path.Combine(JsonFileManager.GetFilePath("Backups"), editorKey);
            if (!Directory.Exists(rootBackups))
            {
                Directory.CreateDirectory(rootBackups);
            }
            return rootBackups;
        }

        /// <summary>
        /// Quick "Save Backup" — creates a timestamped JSON backup in the editor's subfolder.
        /// Automatically prunes to keep only the 5 most recent timestamped files (named files are never auto-deleted).
        /// </summary>
        public static void SaveQuickBackup(string editorKey, string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent)) return;

            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{editorKey}_Backup_{timestamp}.json";
                string fullPath = Path.Combine(folder, fileName);

                File.WriteAllText(fullPath, jsonContent);
                Logger.Message($"[Backup] Quick backup saved for {editorKey}: {fileName}");

                PruneTimestampedBackups(folder, editorKey);
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error in SaveQuickBackup for {editorKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// "Save As..." — saves with a user-provided base name (e.g. "Grimwar", "RimMagic").
        /// Saves as <Editor>_<SafeName>.json in the editor's subfolder.
        /// Does NOT auto-prune named files (user manages them).
        /// </summary>
        public static void SaveNamedBackup(string editorKey, string baseName, string jsonContent)
        {
            if (string.IsNullOrEmpty(baseName) || string.IsNullOrEmpty(jsonContent)) return;

            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                string safeName = SanitizeFileName(baseName);
                string fileName = $"{editorKey}_{safeName}.json";
                string fullPath = Path.Combine(folder, fileName);

                File.WriteAllText(fullPath, jsonContent);
                Logger.Message($"[Backup] Named backup saved for {editorKey}: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error in SaveNamedBackup for {editorKey}: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads a specific backup file by name from the editor's subfolder.
        /// Returns the JSON content or null if not found / error.
        /// </summary>
        public static string LoadBackupFile(string editorKey, string fileName)
        {
            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                string fullPath = Path.Combine(folder, fileName);

                if (File.Exists(fullPath))
                {
                    return File.ReadAllText(fullPath);
                }
                Logger.Warning($"[Backup] Backup file not found: {fileName} in {editorKey}");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error loading backup {fileName} for {editorKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads the most recent timestamped backup for the editor (ignores named files for "Load Backup" quick action).
        /// Returns JSON content or null.
        /// </summary>
        public static string LoadLatestTimestampedBackup(string editorKey)
        {
            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                var timestampedFiles = Directory.GetFiles(folder, $"{editorKey}_Backup_*.json")
                    .OrderByDescending(f => f)
                    .ToList();

                if (timestampedFiles.Count > 0)
                {
                    return File.ReadAllText(timestampedFiles[0]);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error loading latest timestamped for {editorKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a specific backup file from the editor's subfolder.
        /// Safe to call from any editor. Logs result.
        /// </summary>
        public static bool DeleteBackupFile(string editorKey, string fileName)
        {
            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                string fullPath = Path.Combine(folder, fileName);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    Logger.Message($"[Backup] Deleted backup file: {fileName} from {editorKey}");
                    return true;
                }
                Logger.Warning($"[Backup] Tried to delete non-existent file: {fileName}");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Backup] Error deleting {fileName} for {editorKey}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns list of all backup files (timestamped + named) in the editor's subfolder, sorted newest first.
        /// Useful for building a selection list in a future picker dialog.
        /// </summary>
        public static List<string> GetAllBackupFiles(string editorKey)
        {
            try
            {
                string folder = GetEditorBackupFolder(editorKey);
                return Directory.GetFiles(folder, "*.json")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Select(Path.GetFileName)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Prunes only timestamped backups (those matching *_Backup_*.json) to the 5 most recent.
        /// Named/theme backups (e.g. Grimwar.json) are left alone so users can keep important themed saves.
        /// </summary>
        private static void PruneTimestampedBackups(string folder, string editorKey)
        {
            try
            {
                if (!Directory.Exists(folder)) return;

                var timestamped = Directory.GetFiles(folder, $"{editorKey}_Backup_*.json")
                    .OrderByDescending(f => f)
                    .ToList();

                for (int i = 5; i < timestamped.Count; i++)
                {
                    try
                    {
                        File.Delete(timestamped[i]);
                        Logger.Debug($"[Backup] Pruned old timestamped backup: {Path.GetFileName(timestamped[i])}");
                    }
                    catch (Exception delEx)
                    {
                        Logger.Warning($"[Backup] Failed to prune {Path.GetFileName(timestamped[i])}: {delEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[Backup] Prune error for {editorKey}: {ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Custom";
            // Remove invalid filename chars and limit length
            var invalid = Path.GetInvalidFileNameChars();
            string safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            if (safe.Length > 40) safe = safe.Substring(0, 40);
            if (string.IsNullOrEmpty(safe)) safe = "Custom";
            return safe;
        }
    }
}