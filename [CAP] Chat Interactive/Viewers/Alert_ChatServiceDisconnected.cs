// File: Alert_ChatServiceDisconnected.cs
//
// Copyright (c) Captolamia
// This file is part of CAP Chat Interactive (RICS).
// Licensed under the GNU Affero General Public License v3.0 or later.
// See LICENSE.txt in the project root for full license text.
//

using RimWorld;
using Verse;

namespace CAP_ChatInteractive
{
    /// <summary>
    /// Right-side warning while one chat service is enabled and not connected.
    /// <see cref="AlertsReadout"/> already checks each alert every 24 frames, so <see cref="GetReport"/> only reads flags.
    /// </summary>
    public abstract class Alert_ChatServiceDisconnected : Alert
    {
        protected Alert_ChatServiceDisconnected()
        {
            defaultPriority = AlertPriority.Medium;
        }

        protected abstract string LabelKey { get; }

        protected abstract bool HasService(CAPChatInteractiveMod mod);

        protected abstract bool IsEnabled(CAPChatInteractiveMod mod);

        protected abstract bool IsConnected(CAPChatInteractiveMod mod);

        protected abstract bool IsConnecting(CAPChatInteractiveMod mod);

        protected abstract bool IsSuppressed(CAPChatInteractiveMod mod);

        /// <summary>
        /// AlertsReadout checks each alert every 24 frames. This only reads connection flags.
        /// </summary>
        public override AlertReport GetReport()
        {
            CAPChatInteractiveMod mod = CAPChatInteractiveMod.Instance;
            if (mod == null || !HasService(mod))
                return false;

            if (!IsEnabled(mod) || IsSuppressed(mod) || IsConnecting(mod) || IsConnected(mod))
                return false;

            return true;
        }

        public override string GetLabel() => LabelKey.Translate();

        public override TaggedString GetExplanation() => "RICS.Alert.ServiceDisconnectedExplain".Translate();

        protected override void OnClick()
        {
            CAPChatInteractiveMod mod = CAPChatInteractiveMod.Instance;
            if (mod == null)
                return;

            if (Find.WindowStack.WindowOfType<Dialog_ModSettings>() != null)
                return;

            Find.WindowStack.Add(new Dialog_ModSettings(mod));
        }
    }

    /// <summary>Right-side warning while Twitch is enabled and disconnected.</summary>
    public class Alert_TwitchDisconnected : Alert_ChatServiceDisconnected
    {
        protected override string LabelKey => "RICS.Alert.TwitchDisconnected";

        protected override bool HasService(CAPChatInteractiveMod mod) =>
            mod.TwitchService != null && mod.Settings?.TwitchSettings != null;

        protected override bool IsEnabled(CAPChatInteractiveMod mod) => mod.Settings.TwitchSettings.Enabled;

        protected override bool IsConnected(CAPChatInteractiveMod mod) => mod.TwitchService.IsConnected;

        protected override bool IsConnecting(CAPChatInteractiveMod mod) => mod.TwitchService.IsConnecting;

        protected override bool IsSuppressed(CAPChatInteractiveMod mod) => mod.Settings.TwitchSettings.SuppressDisconnectAlert;
    }

    /// <summary>Right-side warning while YouTube is enabled and disconnected.</summary>
    public class Alert_YouTubeDisconnected : Alert_ChatServiceDisconnected
    {
        protected override string LabelKey => "RICS.Alert.YouTubeDisconnected";

        protected override bool HasService(CAPChatInteractiveMod mod) =>
            mod.YouTubeService != null && mod.Settings?.YouTubeSettings != null;

        protected override bool IsEnabled(CAPChatInteractiveMod mod) => mod.Settings.YouTubeSettings.Enabled;

        protected override bool IsConnected(CAPChatInteractiveMod mod) => mod.YouTubeService.IsConnected;

        protected override bool IsConnecting(CAPChatInteractiveMod mod) => mod.YouTubeService.IsConnecting;

        protected override bool IsSuppressed(CAPChatInteractiveMod mod) => mod.Settings.YouTubeSettings.SuppressDisconnectAlert;
    }

    /// <summary>Right-side warning while Kick is enabled and disconnected.</summary>
    public class Alert_KickDisconnected : Alert_ChatServiceDisconnected
    {
        protected override string LabelKey => "RICS.Alert.KickDisconnected";

        protected override bool HasService(CAPChatInteractiveMod mod) =>
            mod.KickService != null && mod.Settings?.KickSettings != null;

        protected override bool IsEnabled(CAPChatInteractiveMod mod) => mod.Settings.KickSettings.Enabled;

        protected override bool IsConnected(CAPChatInteractiveMod mod) => mod.KickService.IsConnected;

        protected override bool IsConnecting(CAPChatInteractiveMod mod) => mod.KickService.IsConnecting;

        protected override bool IsSuppressed(CAPChatInteractiveMod mod) => mod.Settings.KickSettings.SuppressDisconnectAlert;
    }
}
