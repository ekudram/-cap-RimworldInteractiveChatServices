// XmlTextSanitizer.cs
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
// Strip illegal XML control chars (esp. NUL 0x00) from text that can enter
// Verse.Message / Letter archive and break save (Scribe XML writer).
using System.Text;

namespace CAP_ChatInteractive.Utilities
{
    public static class XmlTextSanitizer
    {
        /// <summary>
        /// Remove characters illegal in XML 1.0 text nodes:
        /// U+0000–U+0008, U+000B, U+000C, U+000E–U+001F (keep TAB/LF/CR).
        /// </summary>
        public static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            bool needsWork = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsIllegalXmlChar(text[i]))
                {
                    needsWork = true;
                    break;
                }
            }

            if (!needsWork)
                return text;

            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!IsIllegalXmlChar(c))
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public static bool ContainsIllegalXmlChar(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                if (IsIllegalXmlChar(text[i]))
                    return true;
            }

            return false;
        }

        private static bool IsIllegalXmlChar(char c)
        {
            // XML 1.0 allowed: #x9 | #xA | #xD | [#x20-#xD7FF] | ...
            if (c == '\t' || c == '\n' || c == '\r')
                return false;
            if (c < 0x20)
                return true;
            return false;
        }
    }
}
