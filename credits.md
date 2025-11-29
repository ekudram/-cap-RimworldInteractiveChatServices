# Credits & Acknowledgments

**Mod Disclaimer:** This mod is designed for use with RimWorld and interfaces with RimWorld's assets and systems. This mod is not official and is not endorsed by Ludeon Studios. RimWorld is a trademark and copyrighted work of Ludeon Studios Inc. All rights reserved by Ludeon.

## Conceptual Inspiration
- **TwitchToolkit Concept**: The general concept of integrating Twitch chat with RimWorld's pawn system was inspired by hodlhodl1132's original TwitchToolkit project (AGPLv3). However, this implementation represents a complete ground-up rewrite with substantial architectural differences and no code was directly copied.

## Assets
- **RimWorld Font**: Used with explicit permission from Tynan Sylvester for modder use. [Forum Link](https://ludeon.com/forums/index.php?topic=11022.0)
- **Game Icons**: References RimWorld's built-in icon assets (not distributed with this mod)

## Open Source Dependencies
This project uses the following NuGet packages:
- **TwitchLib.Client** (MIT License) - Twitch chat integration
- **Newtonsoft.Json** (MIT License) - JSON serialization
- **Google.Apis.YouTube.v3** (Apache 2.0 License) - YouTube Live Chat integration
- **Krafs.Rimworld.Ref** - RimWorld API references

## RimWorld Modding Community
Special thanks to the vibrant RimWorld modding community for:
- Shared knowledge and modding techniques
- Documentation and examples of RimWorld's API
- Collaborative problem-solving approaches

## Development Approach
This mod was developed using standard C# design patterns and common solutions to chat integration challenges. Any similarities to other mods reflect convergent evolution in solving similar problems within the same technical constraints.

### Key Architectural Differences from Similar Mods:
- Platform-based user identification system (vs username-only approaches)
- Multi-platform chat integration (Twitch + YouTube)
- Comprehensive queue management with fairness algorithms
- Enhanced security through platform user ID verification
- Different data persistence and serialization approach
- Original pawn assignment and management systems

---

*All code in this repository represents original work developed specifically for this project. While some high-level concepts may overlap with other RimWorld chat integration mods, the implementation is architecturally distinct and contains no directly copied code.*
