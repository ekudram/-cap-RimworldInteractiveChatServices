// MessageSplitter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CAP_ChatInteractive.Utilities
{
    public static class MessageSplitter
    {
        private const int TWITCH_MAX_LENGTH = 500;
        private const int YOUTUBE_MAX_LENGTH = 200;

        public static List<string> SplitMessage(string message, string platform, string username = null)
        {
            int maxLength = GetPlatformMaxLength(platform);

            if (message.Length <= maxLength)
                return new List<string> { message };

            return SplitLongMessage(message, maxLength, username, platform);
        }

        private static int GetPlatformMaxLength(string platform)
        {
            return platform.ToLowerInvariant() switch
            {
                "youtube" => YOUTUBE_MAX_LENGTH,
                "twitch" => TWITCH_MAX_LENGTH,
                _ => YOUTUBE_MAX_LENGTH
            };
        }

        private static List<string> SplitLongMessage(string message, int maxLength, string username, string platform)
        {
            return platform.ToLowerInvariant() == "youtube"
                ? SplitForYouTube(message, maxLength, username)
                : SplitForTwitch(message, maxLength, username);
        }

        private static List<string> SplitForTwitch(string message, int maxLength, string username)
        {
            var parts = new List<string>();
            string prefix = username != null ? $"@{username} " : "";
            int prefixLength = prefix.Length;
            int effectiveMax = maxLength - prefixLength;

            if (message.Contains("Available weather:") || message.Contains("Available commands:"))
            {
                return SplitListMessage(message, effectiveMax, prefix);
            }

            var sentences = SplitIntoSentences(message);
            var currentPart = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentPart.Length + sentence.Length > effectiveMax)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(prefix + currentPart.ToString().Trim());
                        currentPart.Clear();
                    }

                    if (sentence.Length > effectiveMax)
                    {
                        var wordParts = SplitByWords(sentence, effectiveMax);
                        parts.AddRange(wordParts.Select(p => prefix + p));
                        continue;
                    }
                }

                if (currentPart.Length > 0)
                    currentPart.Append(" ");
                currentPart.Append(sentence);
            }

            if (currentPart.Length > 0)
            {
                parts.Add(prefix + currentPart.ToString().Trim());
            }

            return AddPagination(parts, prefix);
        }

        private static List<string> SplitForYouTube(string message, int maxLength, string username)
        {
            var parts = new List<string>();
            string prefix = username != null ? $"@{username} " : "";
            int prefixLength = prefix.Length;
            int effectiveMax = maxLength - prefixLength;

            if (message.Contains("Available weather:") || message.Contains("Available commands:"))
            {
                return SplitListMessage(message, effectiveMax, prefix);
            }

            var sentences = SplitIntoSentences(message);
            var currentPart = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentPart.Length + sentence.Length > effectiveMax)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(prefix + currentPart.ToString().Trim());
                        currentPart.Clear();
                    }

                    if (sentence.Length > effectiveMax)
                    {
                        var chunks = SplitSentenceForYouTube(sentence, effectiveMax);
                        parts.AddRange(chunks.Select(c => prefix + c));
                        continue;
                    }
                }

                if (currentPart.Length > 0)
                    currentPart.Append(" ");
                currentPart.Append(sentence);
            }

            if (currentPart.Length > 0)
            {
                parts.Add(prefix + currentPart.ToString().Trim());
            }

            return AddPagination(parts, prefix);
        }

        private static List<string> SplitSentenceForYouTube(string sentence, int maxLength)
        {
            return SplitByWords(sentence, maxLength);
        }

        private static List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var current = new StringBuilder();

            foreach (char c in text)
            {
                current.Append(c);
                if (c == '.' || c == '!' || c == '?' || c == ',' || c == ';')
                {
                    sentences.Add(current.ToString().Trim());
                    current.Clear();
                }
            }

            if (current.Length > 0)
                sentences.Add(current.ToString().Trim());

            return sentences.Where(s => !string.IsNullOrEmpty(s)).ToList();
        }

        private static List<string> SplitByWords(string text, int maxLength)
        {
            var parts = new List<string>();
            var words = text.Split(' ');
            var currentPart = new StringBuilder();

            foreach (var word in words)
            {
                if (currentPart.Length + word.Length + 1 > maxLength)
                {
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString().Trim());
                        currentPart.Clear();
                    }

                    if (word.Length > maxLength)
                    {
                        var chunks = SplitString(word, maxLength);
                        parts.AddRange(chunks);
                        continue;
                    }
                }

                if (currentPart.Length > 0)
                    currentPart.Append(" ");
                currentPart.Append(word);
            }

            if (currentPart.Length > 0)
                parts.Add(currentPart.ToString().Trim());

            return parts;
        }

        private static List<string> SplitString(string text, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < text.Length; i += chunkSize)
            {
                chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
            }
            return chunks;
        }

        // Your extraction methods (they're correct!)
        private static List<string> ExtractWeatherItems(string message)
        {
            var itemsPart = message.Substring("Available weather:".Length).Trim();
            var items = itemsPart.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(i => i.Trim())
                                 .ToList();
            return items;
        }

        private static List<string> ExtractCommandItems(string message)
        {
            var itemsPart = message.Substring("Available commands:".Length).Trim();
            var items = itemsPart.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(i => i.Trim())
                                 .ToList();
            return items;
        }

        private static List<string> SplitListMessage(string message, int maxLength, string prefix)
        {
            if (message.StartsWith("Available weather:"))
            {
                var items = ExtractWeatherItems(message);
                return BuildListPages(items, "Available weather", maxLength, prefix);
            }
            else if (message.StartsWith("Available commands:"))
            {
                var items = ExtractCommandItems(message);
                return BuildListPages(items, "Available commands", maxLength, prefix);
            }

            return new List<string> { prefix + message };
        }

        private static List<string> BuildListPages(List<string> items, string title, int maxLength, string prefix)
        {
            var pages = new List<string>();
            var currentPage = new StringBuilder();
            currentPage.Append(title + ": ");

            foreach (var item in items)
            {
                if (currentPage.Length + item.Length + 2 > maxLength)
                {
                    pages.Add(prefix + currentPage.ToString().TrimEnd(',', ' '));
                    currentPage.Clear();
                    currentPage.Append(title + " (cont.): ");
                }

                currentPage.Append(item + ", ");
            }

            if (currentPage.Length > title.Length + 2)
            {
                pages.Add(prefix + currentPage.ToString().TrimEnd(',', ' '));
            }

            return AddPagination(pages, prefix);
        }

        private static List<string> AddPagination(List<string> parts, string prefix)
        {
            if (parts.Count > 1)
            {
                for (int i = 0; i < parts.Count; i++)
                {
                    var cleanPart = parts[i];
                    if (!string.IsNullOrEmpty(prefix) && cleanPart.StartsWith(prefix))
                    {
                        cleanPart = cleanPart.Substring(prefix.Length);
                    }
                    parts[i] = $"{prefix}{cleanPart} ({i + 1}/{parts.Count})";
                }
            }

            return parts;
        }
    }
}