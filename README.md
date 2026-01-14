# CAP Chat Interactive: RimWorld Chat Integration Mod

**Connect your chat. Control the colony.**

Turn your Twitch or YouTube viewers into the ultimate RimWorld storyteller! This mod seamlessly integrates live chat into your game, allowing your community to buy colonists, trigger events, gift gear, and shape your colony's destiny in real-time.

## 🚀 Key Features

### 🎙️ Live Chat Integration
- **Twitch**: Full chat integration for reading and sending messages.
- **YouTube**: Read chat with a simple API key; optional OAuth for sending messages.
- **Kick Support**: Under active development!

### 👥 Interactive Pawn System
- **Buy Pawns**: Viewers can purchase custom colonists to join your story.
- **Gear & Equipment**: Chat can buy weapons, armor, and items for specific pawns.
- **Pawn Management**: Heal, revive, and modify traits through chat commands.
- **Queue System**: Manage multiple viewer pawns efficiently with timestamps and fairness algorithms.

### 🌪️ Dynamic Storytelling
- **Incident Control**: Let chat trigger raids, quests, trade caravans, and other events.
- **Weather Control**: Viewers can change weather conditions on demand.
- **Storyteller Role**: Your chat becomes the colony's storyteller!

### 💎 Economy & Engagement
- **Coin System**: Viewers earn currency for participation.
- **Karma System**: Reward positive behavior and interactions.
- **Daily Lootboxes**: Exciting daily rewards to keep viewers engaged.

### 🔧 Mod-Friendly Design
- **Auto-Detection**: Dynamically adds events, items, weather, and races from other mods.
- **Flexible Settings**: Comprehensive configuration through modern, user-friendly dialogs.
- **Extensible Framework**: Built to grow with your modlist, supporting multiple platforms.

### ⚙️ Setup & Support
- **Easy Configuration**: All settings accessible through in-game dialog windows.
- **Modern UI**: Clean, readable layouts for all management screens.
- **Documentation**: Detailed guides on [GitHub Wiki](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/wiki).
- **Report Issues**: [GitHub Issues](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/issues).
- **Discussions**: [GitHub Discussions](https://github.com/ekudram/-cap-RimworldInteractiveChatServices/discussions).

Perfect for streamers, content creators, and anyone who wants to make their RimWorld storytelling truly interactive!

## 📅 Planned Features
1. Kick.com Support
2. Translations

## 🐛 Known Issues
Please report issues with HugLib Log or at least the stack trace of the error.

## Project History and Attribution

CAP Chat Interactive is a community-driven mod conceptually inspired by hodlhodl1132's original **TwitchToolkit** project. We extend our thanks for the foundational ideas and ensure proper attribution to the original work.

This implementation is a complete ground-up rewrite in **.NET 4.7.2**, featuring significant architectural improvements for better security, scalability, and multi-platform support:

- **Platform-Based Identification**: Uses secure platform user IDs to prevent spoofing.
- **Queue Management**: Includes timestamps and fairness algorithms for efficient handling.
- **Multi-Platform Extensibility**: Supports Twitch, YouTube, and future platforms like Kick.
- **Enhanced Security**: Built-in verification to maintain integrity.
- **Data Persistence**: Distinct storage solutions tailored for RimWorld integration.

We are fully committed to complying with the **GNU Affero GPL v3** license under which the original TwitchToolkit was released. All derivative elements are attributed, and our complete source code is available on GitHub.

As a preservation effort for the RimWorld modding community, our goal is to maintain and innovate on interactive chat concepts, providing users with more choices and features. We welcome constructive dialogue with hodlhodl1132 regarding any concerns.

For credits and acknowledgments of contributors, see [CREDITS.md](CREDITS.md).
