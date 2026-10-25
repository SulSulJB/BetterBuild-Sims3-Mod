# Better Build — The Sims 3
**Version 1.0.0 | Created by Jojobou (SulSulJB)**
**C# Source Code | AI-Assisted Development with GPT-6**

Thank you for your interest in Better Build! 💗
Better Build is a mod for **The Sims 3** designed to improve and modernize the Build & Buy Mode experience while preserving the original game's visual identity and functionality.
As a longtime Sims player, I created Better Build to improve some of the features that have bothered me over the years and make building more convenient, enjoyable, and accessible.
The project focuses on improving catalog navigation, object searching, interface usability, and overall convenience while respecting the original game.
This GitHub repository contains the C# source code for Better Build.

Independent Fan-Made Project & Disclaimer

Better Build is an independently developed, unofficial fan-made modification for The Sims 3, created by Jojobou (SulSulJB).

This project is not affiliated with, associated with, authorized, endorsed, sponsored, or supported by Electronic Arts (EA) or Maxis.

Electronic Arts and Maxis have not participated in the development, production, testing, or distribution of Better Build and are not responsible for its functionality, compatibility, maintenance, or technical support.

All original The Sims 3 game assets, software, trademarks, and intellectual property remain the property of Electronic Arts and their respective rights holders.

Better Build is an independent community project created for personal enjoyment and shared with fellow Sims 3 players.

---

Features

Integrated Search Bar

Better Build adds a search bar directly into the Build & Buy interface.

- Search for objects by name or keyword.
- Search within the currently selected category.
- Enable **Search the Entire Catalog** to search across all categories.
- No separate search window is required.

Expanded Build & Buy Catalog

The Build & Buy catalog has been expanded to provide a more comfortable building experience.

- Displays more objects at once.
- Reduces unnecessary navigation.
- Designed primarily for modern 16:9 displays.
- Preserves the familiar Sims 3 interface.

Improved Create a Style (CASt)

Better Build introduces improvements to the Create a Style pattern catalog.

- Patterns are organized into pages of 12 items.
- Pattern categories load their contents when selected rather than loading every pattern immediately.
- Reduces some of the loading time associated with the original pattern catalog.
- Makes browsing patterns more convenient.

Improved Buy Mode Thumbnails

Better Build uses the highest-quality object thumbnails available in the game for the Buy catalog.

This makes objects easier to identify while browsing.

**Note:** HD thumbnails are currently limited mainly to Buy Mode. See Known Issues & Limitations below.

MoveObjects ON/OFF

Enable or disable MoveObjects directly from the Build & Buy interface.

- No need to manually type the cheat.
- Quickly switch between enabled and disabled states.
- Integrated into the interface for convenient access.

Quick Save

Save your game directly from Build & Buy Mode using the Better Build save button.

- Uses The Sims 3's native saving system.
- Provides convenient access to saving while building.

**Note:** Some limitations currently affect Quick Save, particularly in Edit Town. See Known Issues below.

Object Height Controls

Better Build adds convenient keyboard controls for adjusting object height.

With an object selected:

- **8:** Lower the object.
- **9:** Raise the object.

These controls provide additional flexibility when decorating and positioning objects.

DLC & Custom Content Filters

Better Build improves access to existing content filtering controls within the interface, making it more convenient to navigate installed game content.

---

📦 Installation

Requirements

**MonoPatcher by LazyDuchess is required.**

MonoPatcher must be downloaded and installed separately.

Better Build will not function correctly without it.

Installing Better Build

Better Build uses two different `.package` files.

**1. Main / Script Package**

Place the main Better Build package in:

`Documents\Electronic Arts\The Sims 3\Mods\Packages`

**2. Overrides Package**

Place the Better Build Overrides package in:

`Documents\Electronic Arts\The Sims 3\Mods\Overrides`

Your installation should look like this:

- **The Sims 3**
  - **Mods**
    - **Packages**
      - Better Build.package
    - **Overrides**
      - Better Build Overrides.package

**Important:** Do not place the Overrides package inside the Packages folder.

---

Classic & Modern Versions

Better Build is available in two visual versions.

Better Build — Classic

Designed to preserve more of the original Sims 3 visual style.

Recommended for players who prefer the classic appearance of the game.

Better Build — Modern

Features a modernized appearance for selected interface elements introduced by Better Build.

Designed to complement modern interface recolors and custom UI modifications.

The Modern version is not a complete UI replacement.

---

Custom UI Compatibility

Better Build was designed with custom UI compatibility in mind.

**Tested interfaces:**

- Cloud UI — cowplantcartel
- Sakura Pink UI — aecyia

Some elements introduced by Better Build may retain their own appearance and might not perfectly match another UI recolor.

Additionally, modifications that replace the same Build/Buy resources may cause visual conflicts.

Functional compatibility does not guarantee that every interface element will match another UI perfectly.

---

⚠️ Mod Compatibility

To avoid conflicting interface changes or duplicated functionality, removing the following mods is recommended before installing Better Build:

- Tiny UI Fix
- LazyDuchess' Search Mod
- eca's QuickCheat

Better Build includes its own search and quick-control features and modifies certain Build & Buy interface systems.

These recommendations are made strictly for compatibility purposes.

**Better Build was developed independently from these mods.**

---

⚠️ Known Issues & Limitations

### HD Thumbnails in Build Mode

HD thumbnails are currently implemented mainly in Buy Mode.

Build Mode continues to use the game's native thumbnails.

During development, an expanded HD implementation was tested in Build Mode. However, it caused significant performance slowdowns in categories containing large numbers of items, particularly Floors and Wall Coverings.

For Version 1.0, stability and performance were prioritized.

Unused Space in the Build Catalog

Depending on the selected category and screen resolution, a small unused area may appear on the right side of the Build catalog.

Using the entire available width was tested during development but could cause performance issues when the catalog recalculated its contents.

This limitation is intentional in Version 1.0.

Quick Save

Quick Save may not always preserve the current game mode after saving.

In some situations, saving from Build & Buy Mode may return the player to Live Mode.

Quick Save also has known limitations in Edit Town, particularly when no active household is selected.

These issues are being considered for future improvements.

Tiny UI Fix

Tiny UI Fix is not currently fully compatible with Better Build.

Using both modifications together may produce interface conflicts or unexpected behavior.

Custom UI Differences

Although Better Build has been tested with certain UI modifications, minor visual differences may occur.

Some custom interface elements may require additional compatibility adjustments.

Screen Resolution

Better Build was primarily developed and tested for 16:9 screen resolutions.

Other aspect ratios may work, but some interface elements may appear differently positioned.

Create a Style Pagination

Better Build intentionally changes how patterns are loaded and displayed.

Patterns are displayed in pages of 12 instead of loading the entire catalog at once.

This behavior is part of the performance improvements and is not considered a bug.

---

## ✅ After Installation

Before launching The Sims 3:

1. Fully close the game.
2. Open `Documents\Electronic Arts\The Sims 3`.
3. Delete the following cache files if they exist:

   - `scriptCache.package`
   - `compositorCache.package`
   - `simCompositorCache.package`
   - `CASPartCache.package`
   - `socialCache.package`

4. Launch The Sims 3 normally.

The game will automatically recreate these files.

Clearing the cache is also recommended after updating Better Build.

---

## 🗑️ Uninstallation

To uninstall Better Build:

1. Close The Sims 3.
2. Remove the main Better Build package from `Mods\Packages`.
3. Remove the Better Build Overrides package from `Mods\Overrides`.
4. If using the Modern version, remove any associated Better Build Modern UI files.
5. Clear the game cache.

---

## 🐛 Bug Reports & Feedback

Better Build is still a relatively new project, and unexpected issues may occur depending on the game configuration and installed modifications.

When reporting a problem, please include as much of the following information as possible:

- Your version of The Sims 3.
- Your Better Build version.
- Whether you use Classic or Modern.
- Your screen resolution.
- Installed expansion packs and stuff packs.
- Other Build & Buy or UI modifications.
- Screenshots showing the issue.
- Steps to reproduce the problem.
- Whether the issue persists after clearing the game cache.

**Contact:** jojoboupro@gmail.com

Feedback, suggestions, and constructive discussions are always welcome. 💗

---

## 💻 Source Code

This repository is intended to make the C# source code of Better Build publicly available for inspection and technical transparency.

The source code is not a replacement for the downloadable `.package` files.

Better Build relies on The Sims 3's game assemblies and the MonoPatcher framework.

Proprietary Electronic Arts game assemblies are not distributed as part of this repository.

---

## 🤖 Development Transparency — AI Assistance

Better Build is my first modding project for The Sims 3.

I started developing this project because I wanted to improve my personal gameplay experience and bring my own vision of a better Build & Buy Mode to life.

As someone who had no previous experience developing Sims 3 mods, I used **GPT-6 as an AI development assistant** throughout the project.

### Development Contributions

**Concept & Creative Direction — SulSulJB**

The original concept, features, interface vision, design choices, and overall direction of Better Build are my own.

**C# Scripting — AI-Assisted with GPT-6**

GPT-6 assisted with C# code development and implementation, understanding The Sims 3's game engine, exploring technical possibilities, and troubleshooting issues during development.

**UI Layouts (LAYO) — Jojobou with GPT-6 Assistance**

Original Electronic Arts UI layouts were modified and adapted by me, with technical assistance from GPT-6.

Image Resources — SulSulJB

Custom visual resources and interface graphics were created or modified by me.

**Translations — SulSulJB**

Original text and translations were prepared by me, with community contributions credited separately when applicable.

**In-Game Testing & Creative Decisions — SulSulJB**

Features, interface adjustments, gameplay behavior, and development decisions were evaluated through in-game testing as part of my personal project.

### Why I Used AI

AI helped me explore technical possibilities that I would otherwise have struggled to understand or implement on my own.

It has been a learning and development tool that allowed me to work on a project I genuinely care about.

**The use of AI assistance is openly disclosed for transparency.**

I respect that everyone has different opinions about AI-assisted development, and I believe people should have the information they need to decide whether they want to use my mods.

My goal has always been simple: to improve my favorite Sims game and share those improvements with others who might enjoy them. 💗

---

## 💙 Credits & Acknowledgments

### Better Build

Created and developed by **Jojobou (SulSulJB)**

### GPT-6 — OpenAI

AI assistance for C# scripting, technical research, code implementation, and troubleshooting.

### MonoPatcher — LazyDuchess

Better Build uses MonoPatcher as its patching framework.

MonoPatcher is a third-party project and is not original Better Build code.

### Electronic Arts / Maxis

Better Build is an unofficial fan-made modification for The Sims 3.

It is not affiliated with, endorsed by, sponsored by, or produced by Electronic Arts or Maxis.

The Sims, The Sims 3, and their associated software, assets, trademarks, and intellectual property belong to Electronic Arts and their respective rights holders.

### The Sims 3 Community

Thank you to everyone who has tried Better Build, shared feedback, reported bugs, provided translations, or supported this project.

Your encouragement means a lot to me.

---

## 🌸 Download & Community

**Download Better Build:**

[Better Build on Patreon](https://www.patreon.com/jbjojobou/posts/better-build-3-172945517)

**YouTube:**

[Watch the Better Build Showcase](https://www.youtube.com/watch?v=oqmVOm80tWQ)

**Tumblr:**

[SulSulJB on Tumblr](https://sulsuljb.tumblr.com/)

---

## ⚠️ Disclaimer

Better Build is provided **as-is**, without warranties of any kind.

Although the mod is tested before release, bugs, incompatibilities, or unexpected behavior may still occur.

It is strongly recommended to back up important save files before installing or updating any game modification.

Thank you for supporting Better Build and my vision for The Sims 3!

**Happy Building! 💗**

— Jojobou / Sul
