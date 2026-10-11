# InstanceLootPlugin

This plugin is used to provide instance based loot when you're running with "Artifact of Sacrifice" and "Artifact of Command" enabled.

## AI Disclosure

The 2.3.0 update (the RoR2 1.5 update, the new features and bug fixes listed in its changelog, and this README) was written with help from AI. The maintainer read and reviewed all of the AI-written changes. Versions before 2.3.0 contain no AI-generated code.

## Features

- By default, each player gets their own private Command cube for drops from:
  - monster kills (Artifact of Sacrifice)
  - teleporter and boss rewards
  - chests, equipment barrels, lunar pods, adaptive chests and scavenger bags
  - void cradles, void potentials and void seed rewards
  - boss items dropped by Trophy Hunter's Tricorn
  - N'tormat book and page rewards, including N'kuhana chests (each player only sees their own)
  - rewards the game drops once per player in a pile: Void Fields, Simulacrum waves, Halcyonite shrines, the Aurelionite heart, False Son, geodes and other encounters
- Rusted Key and Encrusted Key lockboxes give one shared drop by default.
- Each of these sources can be turned on or off in the config. See [Configuration](#configuration).
- Everything else is always vanilla: shops, printers, scrappers, cauldrons, the Bazaar, Shrines of Chance and lunar coins.
- Ensures drop rates are appropriate for the number of players, since all drops are shared now.
- Adds bad luck protection to prevent long stretches without item drops from kills.
- Adds a hotkey (`F3` by default) to pull all unpicked Command cubes to you, both your private cubes and shared ones.
- Adds a console command `drop_rate <value>` to set a custom drop rate for items (0-100).
- Adds a console command `drop_rate_report` to view drop rate statistics for your run in the debugging window.
- Adds a console command `instance_loot_hotkey <key>` to change the pull hotkey (for example `instance_loot_hotkey F4`).

## Configuration

The config file is `BepInEx/config/programit.InstanceBasedLoot.cfg`. It is created the first time you launch the game with the mod installed. Edit it while the game is closed, then launch the game.

Instancing is decided by the host, so only the host's config matters for these settings.

### Instancing

Set an option to `true` to give each player their own cube from that source, or `false` for one shared cube like vanilla.

| Option | Default | Drops from |
| --- | --- | --- |
| `MonsterDrops` | `true` | Monsters killed with Artifact of Sacrifice |
| `BossRewards` | `true` | Teleporter bosses, Mithrix, Voidling, Aurelionite and other boss encounters |
| `Chests` | `true` | Small, large, legendary and category chests |
| `EquipmentBarrels` | `true` | Equipment barrels |
| `LunarPods` | `true` | Lunar pods |
| `VoidCradles` | `true` | Void cradles |
| `AdaptiveChests` | `true` | Adaptive chests |
| `ScavengerBags` | `true` | Bags dropped by Scavengers |
| `VoidPotentials` | `true` | Void potentials |
| `VoidSeeds` | `true` | Void seed rewards |
| `Lockboxes` | `false` | Rusted Key and Encrusted Key lockboxes |
| `TrophyHunterDrops` | `true` | Boss items dropped when Trophy Hunter's Tricorn kills a boss |
| `BookRewards` | `true` | N'tormat book and page rewards from the altar, compulsion quests, shrines and N'kuhana chests. Each player only sees their own book and page |
| `EventRewards` | `true` | Rewards the game drops once per player in a pile (Void Fields, Simulacrum waves, Halcyonite shrines, the Aurelionite heart, False Son, geodes and other encounters). Each player gets one |

Turning off `MonsterDrops` doesn't turn off the drop rate changes below. If monster drops are shared, you may want to set `EnablePlayerBasedDropRateScaling` to `false`.

### General

| Option | Default | Description |
| --- | --- | --- |
| `BaseDropChance` | `5` | Base item drop chance for monster kills. It is recommended to leave this at the default value. |
| `DropChanceMultiplier` | `1` | Multiplies the drop chance. Use `0.5` to halve or `2` to double. |
| `EnablePlayerBasedDropRateScaling` | `true` | Divides the drop chance by the number of players, since every player gets a copy of each drop. |
| `MinimumDropChance` | `1` | The lowest drop chance player scaling can go down to. |
| `EnableSwarmsScaling` | `true` | Lowers drop chances with Artifact of Swarms to account for the doubled spawns. |
| `EnableBadLuckProtection` | `true` | Raises drop chances during long stretches without drops. |
| `PullItemsHotkey` | `F3` | Hotkey that pulls your cubes to you. This one is per player. |

## How is this different from InstancedLoot Continuation?

[InstancedLoot Continuation](https://thunderstore.io/c/riskofrain2/p/Team7_RotaryCoded/InstancedLoot_continuation/) is a general-purpose instancing mod. It gives every player their own copy of chests, shrines and other objects, with many configurable modes (instance the object, the items, or both, optionally owner-only), and works with or without artifacts.

This mod is narrower and built for one setup: **Artifact of Sacrifice + Artifact of Command**.

- It only does something when Artifact of Command is enabled. Nothing is instanced in a normal run.
- It instances the Command cubes that drop from monster kills, teleporter/boss rewards, chests and event rewards, not the chests or shrines themselves. Everyone opens the same chest, and each player gets their own cube from it.
- It rebalances Sacrifice drop rates for shared loot: it scales the drop chance by player count, compensates for Artifact of Swarms, and adds bad luck protection so low drop rates don't leave you without items for long stretches.
- It adds a hotkey to pull your unclaimed cubes to you, and console commands to set and report drop rates.
- Its config is a simple on/off switch per drop source, not a choice between several instancing modes for each object.

If you want instanced loot in regular runs, or more instancing modes (owner-only pickups, instanced shrines, etc.), use InstancedLoot Continuation. If you play Sacrifice + Command with friends and want balanced, private item choices, use this one. The two mods aren't meant to be installed together.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## Contributors

- [tgrieger](https://github.com/tgrieger)
- Grey
