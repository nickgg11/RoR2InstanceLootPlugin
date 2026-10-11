# Changelog

## 2.3.0

- Update for Risk of Rain 2 1.5 and update all libraries.
- Add an `Instancing` config section to choose which drop sources are instanced.
- Instance boss items dropped by Trophy Hunter's Tricorn.
- Instance N'tormat book and page rewards, including N'kuhana chests. Each player only sees the book and page dropped for them.
- Add an `EventRewards` option that instances rewards the game drops once per player in a pile, like Void Fields, Simulacrum waves, Halcyonite shrines, the Aurelionite heart, False Son and geodes.
- Only instance the drops listed in the README. Shops, printers, scrappers, key lockboxes and other non-chest drops are no longer duplicated for every player.
- Lunar coins drop as normal coins again instead of Command cubes.
- Fix bad luck protection never resetting, which made almost every kill drop an item after the first few minutes.
- Fix temporary and rental items becoming permanent when picked from an instanced cube.
- Fix scrappers and other item pickers giving a free copy of the selected item.
- Command cubes from monster kills now fall and land like vanilla droplets instead of appearing where the monster died.
- The pull hotkey pulls both your private cubes and shared cubes, places them on the ground and in front of walls, spaces them evenly, and does nothing when you have no cubes.
- `instance_loot_hotkey` now changes your own hotkey (it used to change the host's) and works from the main menu.
- `DropChanceMultiplier` now also applies when player-based scaling is disabled, and manual `drop_rate` values reset between runs.
- Loot is now interactable right away for players who join a run late. Alt-tabbing is no longer needed.
- Removed the unused R2API Items, Language and CommandHelper dependencies.
- Published as `Instance_Based_Loot_Fixed`. The changelog now lives in its own file.

## 2.2.0

- Add configurable hotkey management for pulling items to you through a new console command `instance_loot_hotkey`
- Update packages

## 2.1.0

- Fix compatibility after Devotion update

## 2.0.1

- Fix compatibility with CommandQueue mod

## 2.0.0

### Fixes

- No longer despawns all item drops if Artifact of Command is disabled.
- Mod will disable itself during Bulwark's Ambry to avoid a bug causing Artifacts to not be rewarded on successful clears.
- [DropInMultiplayer] Focusing (alt-tab) the Application will fix loot being non-interactable if a player joins after a level has loaded. This issue will resolve automatically at the beginning of the next stage if no action is taken.
- Resolve NRE log spam from F3 keybind when no items can be pulled.

### Configuration Options

- Base drop rate
- Disable player-based drop rates
- Disable swarm drop rate modifier
- Minimum drop rate
- Drop rate mulitplier

### Dynamic Drop Rates

There are a lot of details here, so it can be hard to understand how the game actually _feels_ with these changes.

Here is what we aimed for:

- Roughly 1 item per minute (this inevitably speeds up later in the game).
- More consistent experiences with varying group sizes. (Roughly the same item rate as solo play.)
- More consistent experiences with and without Artifact of Swarms.

**Balance Changes**

1. Compensate drop rate for Artifact of Swarms (the game doubles spawns, but reduces drop rates by as little as 15%).
2. Ensures drop rates are appropriate for the number of players, since all drops are shared now.
3. Account for additional factors used by the game when calculating drop rates.
   - Artifact of Swarms lowers drop chances. (Lemurian w/ Swarms: 5%) vs (Lemurian w/o Swarms: 7.9%)
   - Elites have higher drop rates than normal enemies. (Normal Lemurian: 7.9%) vs (Elite Lemurian: 19%)
   - Some enemies have a higher base drop rate than others. (Normal Beetle: 5%) vs (Normal Scavenger: 43.2%)
   - Some entities have a native drop rate of 0%. (Mending healing orbs, TheBackup drones, Void Infestors)
4. Bad Luck Protection: A 4 player lobby will have a base drop rate of 1.25, so it is possible for drops to feel scarce. We prevent unlucky streaks by adding all drop chances to a pool, and increasing drop rates after it reaches 100%. Bad luck protection only kicks in if the survivor's item count is falling behind (fewer item drops than expected or low item drops per minute).
   - Example: Survivors have killed 80 enemies with a drop rate of 1.25%, adding up to 100%.
     - next enemy has a 1.25% drop rate, so the drop rate will be bumped to 2.5%. (cumulative chance = 101.25%)
     - next enemy has a 3% drop rate, so the drop rate will be bumped to 7.25%. (cumulative chance = 104.25%)
     - next enemy has a 1.25% drop rate, so the drop rate will be bumped to 6.75%. (cumulative chance = 105.5%)
     - next enemy has a 5% drop rate, so the drop rate will be bumped to 10.5%. (cumulative chance = 110.5%)
5. Do not scale teleporter loot drops by player count
