using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using RoR2.Artifacts;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using static HG.Reflection.SearchableAttribute;

[assembly: OptIn]


namespace InstanceLootPlugin
{
    /// <summary>
    /// This plugin is used to provide instance based loot when you're running with "Artifact of Sacrifice" and "Artifact of Command" enabled.
    ///
    /// Normally, when a droplet lands with Command enabled, the server spawns a networked CommandCube that anyone can open.
    /// For drops from monster kills, teleporter/boss rewards, chests and void seeds, this mod instead tells every client
    /// to spawn its own local CommandCube. Which of these sources are instanced is set in the "Instancing" config
    /// section (see <see cref="InstancingConfig"/>). Since the object is local, no one but that client can see it or
    /// interact with it. Every other drop (shops, printers, scrappers, lunar coins, etc.) keeps vanilla behavior.
    ///
    /// The server doesn't know about local cubes, so interacting with one and choosing an item is handled on the client.
    /// The chosen item is then spawned by the server, since clients aren't allowed to spawn network objects. Since the item
    /// is spawned on the server it can be picked up by anyone, but it spawns where the player's cube was.
    ///
    /// Other features provided by the plugin:
    /// 1) A hotkey (F3 by default) that pulls all of your CommandCubes to you, so any you might have missed aren't lost
    /// 2) A console command `drop_rate <value>` which you can use to set the drop rate of items from monsters
    /// </summary>
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class InstanceLootPlugin : BaseUnityPlugin
    {
        // The Plugin GUID should be a unique ID for this plugin,
        // which is human readable (as it is used in places like the config).
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "programit";
        public const string PluginName = "InstanceBasedLoot";
        public const string PluginVersion = "2.3.0";
        private static ConfigEntry<float> DropChanceMultiplier { get; set; }
        private static ConfigEntry<float> MinimumDropChance { get; set; }
        private static ConfigEntry<float> BaseDropChance { get; set; }
        private static ConfigEntry<bool> EnablePlayerDropRateScaling { get; set; }
        private static ConfigEntry<bool> EnableSwarmsScaling { get; set; }
        private static ConfigEntry<bool> EnableBadLuckProtection { get; set; }
        private static ConfigEntry<KeyCode> PullItemsHotKey { get; set; }

        // Matches current default drop rate. Differs from BaseDropChance because we override it for player scaling.
        public static float dropChance = 5;

        // We disable the mod in Bulwark's Ambry. The artifact trial has its own key drops and loot rules,
        // so we leave that scene completely vanilla.
        private static bool forceDisabled = false;

        // Persist drop rates that have been manually set via the console. Resets between runs.
        private static bool useManualDropRate = false;

        private static DropRateManager dropRateManager;

        // Which game event is currently creating droplets on the server, and whether its drops should be instanced.
        // These are set by hooking the methods that drop loot, so every other drop stays vanilla.
        private static DropSource currentDropSource = DropSource.None;
        private static bool instanceCurrentDrops;

        // Some sources already drop one reward per player in a pile. Since each instanced cube is seen by every player,
        // we only keep one in every this many droplets from those sources.
        private static int keepOneDropletInEvery = 1;
        private static int dropletsForSource;

        // Some sources drop one reward at each player's feet. Each of those droplets only goes to the player it was dropped on.
        private static bool dropForEachPlayer;

        // Some sources drop one reward for each player in a list. Each droplet goes to the next player in the queue.
        private static Queue<NetworkConnection> dropletOwners;

        private const int KeepOnlyFirst = int.MaxValue;

        private const float PullRadius = 10f;
        private const float CubeHeightAboveGround = 1f;

        private static readonly string[] MenuScenes =
        {
            "loadingbasic",
            "intro",
            "splash",
            "title",
            "eclipseworld", // Eclipse menu
            "infinitetowerworld", // Simulacrum menu
            "lobby"
        };

        private enum DropSource
        {
            None,
            MonsterKill,
            Other
        }

        private static bool IsActive => !forceDisabled && IsCommandEnabled();

        public void Awake()
        {
            Log.Init(Logger);

            // Read configs from file
            DropChanceMultiplier = base.Config.Bind<float>(
                "General",
                "DropChanceMultiplier",
                1f,
                "Manipulate drop rate. Use `0.5` to halve or `2` to double."
            );

            MinimumDropChance = base.Config.Bind<float>(
                "General", "MinimumDropChance",
                1f,
                "The lowest possible drop chance when player count scaling is active."
            );

            EnablePlayerDropRateScaling = base.Config.Bind<bool>(
                "General",
                "EnablePlayerBasedDropRateScaling",
                true,
                "Enabling this will reduce drop rates to account for shared loot."
            );

            EnableSwarmsScaling = base.Config.Bind<bool>(
                "General",
                "EnableSwarmsScaling",
                true,
                "Enabling this will (correctly) reduce drop rates for Artifact of Swarms."
            );

            EnableBadLuckProtection = base.Config.Bind<bool>(
                "General",
                "EnableBadLuckProtection",
                true,
                "Enabling this will increase drop rate consistency with low drop rates."
            );

            PullItemsHotKey = base.Config.Bind<KeyCode>(
                "General",
                "PullItemsHotkey",
                KeyCode.F3,
                "Hotkey to use to pull items to yourself."
            );

            BaseDropChance = base.Config.Bind<float>(
                "General",
                "BaseDropChance",
                5f,
                "Base item drop chance. It is recommended to leave this at the default value."
            );

            InstancingConfig.Bind(base.Config);

            dropRateManager = new DropRateManager(
                EnableBadLuckProtection,
                EnableSwarmsScaling,
                DropChanceMultiplier,
                BaseDropChance,
                MinimumDropChance);
            dropChance = dropRateManager.GetBaseDropChance(false, 1);

            NetworkingAPI.RegisterMessageType<SpawnInstancedCubeMessage>();
            NetworkingAPI.RegisterMessageType<ClaimInstancedPickupMessage>();

            Log.Info("InstanceLootPlugin loaded");
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // The hooks stay registered for the plugin's lifetime and check IsActive themselves. This way they also
            // work for players who join a run late, without waiting for a scene change.
            On.RoR2.Util.GetExpAdjustedDropChancePercent += Util_GetExpAdjustedDropChancePercent;

            // Sources of instanced loot
            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath += SacrificeArtifactManager_OnServerCharacterDeath;
            On.RoR2.BossGroup.DropRewards += BossGroup_DropRewards;
            On.RoR2.ChestBehavior.ItemDrop += ChestBehavior_ItemDrop;
            On.RoR2.OptionChestBehavior.ItemDrop += OptionChestBehavior_ItemDrop;
            On.RoR2.ScavBackpackBehavior.ItemDrop += ScavBackpackBehavior_ItemDrop;
            On.RoR2.RouletteChestController.EjectPickupServer += RouletteChestController_EjectPickupServer;
            On.EntityStates.VoidCamp.Deactivate.OnEnter += VoidCampDeactivate_OnEnter;
            On.RoR2.EquipmentSlot.FireBossHunter += EquipmentSlot_FireBossHunter;
            On.RoR2.DLC4ProgressionController.RewardAllPlayersPages += DLC4ProgressionController_RewardAllPlayersPages;
            On.RoR2.ShrineDeathBehavior.HandleSquadDefeatServer += ShrineDeathBehavior_HandleSquadDefeatServer;
            On.EntityStates.NkuhanaChest.OpenPageReward.OnEnter += NkuhanaChestOpenPageReward_OnEnter;
            On.EntityStates.NkuhanaChest.OpenBookReward.OnEnter += NkuhanaChestOpenBookReward_OnEnter;
            On.EntityStates.AurelioniteHeart.AurelioniteHeartActivationState.FixedUpdate += AurelioniteHeartActivationState_FixedUpdate;
            On.EntityStates.FalseSonBoss.SkyJumpDeathState.GiveColossusItem += SkyJumpDeathState_GiveColossusItem;
            On.EntityStates.Geode.GeodeShatter.OnEnter += GeodeShatter_OnEnter;
            On.EntityStates.Missions.GeodeSecretMission.GeodeSecretMissionRewardState.DropRewards += GeodeSecretMissionRewardState_DropRewards;
            On.RoR2.HalcyoniteShrineInteractable.DropRewards += HalcyoniteShrineInteractable_DropRewards;
            On.RoR2.ArenaMissionController.EndRound += ArenaMissionController_EndRound;
            On.RoR2.InfiniteTowerWaveController.DropRewards += InfiniteTowerWaveController_DropRewards;
            On.EntityStates.Reaper.DeathState.DropCursedItems += ReaperDeathState_DropCursedItems;
            On.EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death.OnEnter += SolusHeartDeath_OnEnter;
            On.EntityStates.Wake.Deactivate.OnEnter += WakeDeactivate_OnEnter;
            On.RoR2.WakeController.DropShamanRewards += WakeController_DropShamanRewards;
            On.RoR2.ShrineDreadBehavior.ShutdownShrine += ShrineDreadBehavior_ShutdownShrine;

            // Tag droplets from those sources, then instance their CommandCube once they land
            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 += PickupDropletController_CreatePickupDroplet;
            On.RoR2.PickupDropletController.CreateCommandCube += PickupDropletController_CreateCommandCube;
            On.RoR2.PickupDropletController.CreatePickup += PickupDropletController_CreatePickup;

            // Interacting with and choosing from local cubes, which the server doesn't know about
            On.RoR2.Interactor.AttemptInteraction += Interactor_AttemptInteraction;
            On.RoR2.PickupPickerController.SubmitChoice += PickupPickerController_SubmitChoice;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            On.RoR2.Util.GetExpAdjustedDropChancePercent -= Util_GetExpAdjustedDropChancePercent;

            On.RoR2.Artifacts.SacrificeArtifactManager.OnServerCharacterDeath -= SacrificeArtifactManager_OnServerCharacterDeath;
            On.RoR2.BossGroup.DropRewards -= BossGroup_DropRewards;
            On.RoR2.ChestBehavior.ItemDrop -= ChestBehavior_ItemDrop;
            On.RoR2.OptionChestBehavior.ItemDrop -= OptionChestBehavior_ItemDrop;
            On.RoR2.ScavBackpackBehavior.ItemDrop -= ScavBackpackBehavior_ItemDrop;
            On.RoR2.RouletteChestController.EjectPickupServer -= RouletteChestController_EjectPickupServer;
            On.EntityStates.VoidCamp.Deactivate.OnEnter -= VoidCampDeactivate_OnEnter;
            On.RoR2.EquipmentSlot.FireBossHunter -= EquipmentSlot_FireBossHunter;
            On.RoR2.DLC4ProgressionController.RewardAllPlayersPages -= DLC4ProgressionController_RewardAllPlayersPages;
            On.RoR2.ShrineDeathBehavior.HandleSquadDefeatServer -= ShrineDeathBehavior_HandleSquadDefeatServer;
            On.EntityStates.NkuhanaChest.OpenPageReward.OnEnter -= NkuhanaChestOpenPageReward_OnEnter;
            On.EntityStates.NkuhanaChest.OpenBookReward.OnEnter -= NkuhanaChestOpenBookReward_OnEnter;
            On.EntityStates.AurelioniteHeart.AurelioniteHeartActivationState.FixedUpdate -= AurelioniteHeartActivationState_FixedUpdate;
            On.EntityStates.FalseSonBoss.SkyJumpDeathState.GiveColossusItem -= SkyJumpDeathState_GiveColossusItem;
            On.EntityStates.Geode.GeodeShatter.OnEnter -= GeodeShatter_OnEnter;
            On.EntityStates.Missions.GeodeSecretMission.GeodeSecretMissionRewardState.DropRewards -= GeodeSecretMissionRewardState_DropRewards;
            On.RoR2.HalcyoniteShrineInteractable.DropRewards -= HalcyoniteShrineInteractable_DropRewards;
            On.RoR2.ArenaMissionController.EndRound -= ArenaMissionController_EndRound;
            On.RoR2.InfiniteTowerWaveController.DropRewards -= InfiniteTowerWaveController_DropRewards;
            On.EntityStates.Reaper.DeathState.DropCursedItems -= ReaperDeathState_DropCursedItems;
            On.EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death.OnEnter -= SolusHeartDeath_OnEnter;
            On.EntityStates.Wake.Deactivate.OnEnter -= WakeDeactivate_OnEnter;
            On.RoR2.WakeController.DropShamanRewards -= WakeController_DropShamanRewards;
            On.RoR2.ShrineDreadBehavior.ShutdownShrine -= ShrineDreadBehavior_ShutdownShrine;

            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 -= PickupDropletController_CreatePickupDroplet;
            On.RoR2.PickupDropletController.CreateCommandCube -= PickupDropletController_CreateCommandCube;
            On.RoR2.PickupDropletController.CreatePickup -= PickupDropletController_CreatePickup;

            On.RoR2.Interactor.AttemptInteraction -= Interactor_AttemptInteraction;
            On.RoR2.PickupPickerController.SubmitChoice -= PickupPickerController_SubmitChoice;
        }

        private void Update()
        {
            // Pulls all of your CommandCubes to you.
            if (!Input.GetKeyDown(PullItemsHotKey.Value))
            {
                return;
            }

            CharacterBody body = LocalUserManager.GetFirstLocalUser()?.cachedBody;
            if (!body)
            {
                return;
            }

            var cubes = FindCommandCubes();
            if (cubes.Count == 0)
            {
                return;
            }

            PullCubesTo(body, cubes);
        }

        /// <summary>
        /// Finds both our local instanced cubes and the networked cubes from drops that aren't instanced
        /// (lockboxes, sources turned off in the config, etc.).
        /// </summary>
        private static List<Transform> FindCommandCubes()
        {
            var cubes = new List<Transform>();
            GameObject prefab = CommandArtifactManager.commandCubePrefab;
            if (!prefab)
            {
                return cubes;
            }

            foreach (var picker in FindObjectsOfType<PickupPickerController>())
            {
                if (picker.GetComponent<InstancedCommandCube>() || picker.name.StartsWith(prefab.name))
                {
                    cubes.Add(picker.transform);
                }
            }

            return cubes;
        }

        /// <summary>
        /// Places the cubes in an even circle around the body, resting on the ground and kept in front of walls.
        /// </summary>
        private static void PullCubesTo(CharacterBody body, List<Transform> cubes)
        {
            Vector3 center = body.corePosition;
            int worldMask = LayerIndex.world.mask;

            for (int i = 0; i < cubes.Count; i++)
            {
                float angle = i * Mathf.PI * 2f / cubes.Count;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                float distance = PullRadius;
                if (Physics.Raycast(center, direction, out RaycastHit wallHit, PullRadius, worldMask))
                {
                    distance = Mathf.Max(0f, wallHit.distance - 1f);
                }

                Vector3 target = center + direction * distance;
                if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 50f, worldMask))
                {
                    target = groundHit.point + Vector3.up * CubeHeightAboveGround;
                }
                else
                {
                    target = body.footPosition + direction * distance + Vector3.up * CubeHeightAboveGround;
                }

                cubes[i].position = target;
            }
        }

        private static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            bool isMenuScene = MenuScenes.Contains(newScene.name);

            // Bulwark's Ambry
            forceDisabled = newScene.name == "artifactworld";

            // Reset manual drop rates and drop rate tracking between runs
            if (isMenuScene)
            {
                useManualDropRate = false;
                DropRateTracker.ResetTracker();
            }

            // We recalculate drop rate each stage to account for any new or missing players
            if (!useManualDropRate)
            {
                float newDropChance = dropRateManager.GetBaseDropChance(
                    EnablePlayerDropRateScaling.Value && IsSacrificeEnabled(),
                    GetPlayerCount());

                if (!newDropChance.Equals(dropChance))
                {
                    dropChance = newDropChance;
                    Log.Debug($"Using dropRate of {dropChance}% (base={BaseDropChance.Value}, multiplier={DropChanceMultiplier.Value}, playerCount={GetPlayerCount()}, minimumDropChance={MinimumDropChance.Value})");
                }
            }
        }

        /// <summary>
        /// Runs game code that drops loot, so we know where the droplets it creates came from and whether to instance them.
        /// </summary>
        private static void RunWithDropSource(DropSource source, bool instance, Action dropLoot, int keepOneInEvery = 1,
            bool forEachPlayer = false, Queue<NetworkConnection> owners = null)
        {
            var previousSource = currentDropSource;
            var previousInstance = instanceCurrentDrops;
            var previousKeepOneInEvery = keepOneDropletInEvery;
            var previousDroplets = dropletsForSource;
            var previousForEachPlayer = dropForEachPlayer;
            var previousOwners = dropletOwners;

            currentDropSource = source;
            instanceCurrentDrops = instance;
            keepOneDropletInEvery = Math.Max(1, keepOneInEvery);
            dropletsForSource = 0;
            dropForEachPlayer = forEachPlayer;
            dropletOwners = owners;
            try
            {
                dropLoot();
            }
            finally
            {
                currentDropSource = previousSource;
                instanceCurrentDrops = previousInstance;
                keepOneDropletInEvery = previousKeepOneInEvery;
                dropletsForSource = previousDroplets;
                dropForEachPlayer = previousForEachPlayer;
                dropletOwners = previousOwners;
            }
        }

        /// <summary>
        /// For sources that drop one reward per player in a pile. Each player gets their own cube, so we keep one reward per player.
        /// </summary>
        private static void RunForPlayerPile(bool instance, Action dropLoot)
        {
            int players = Run.instance ? Run.instance.participatingPlayerCount : 1;
            RunWithDropSource(DropSource.Other, instance, dropLoot, keepOneInEvery: players);
        }

        // Monster kills always set the drop source, even when they aren't instanced, so the drop rate changes still apply.
        private static void SacrificeArtifactManager_OnServerCharacterDeath(On.RoR2.Artifacts.SacrificeArtifactManager.orig_OnServerCharacterDeath orig, DamageReport damageReport)
        {
            RunWithDropSource(DropSource.MonsterKill, InstancingConfig.MonsterDrops.Value, () => orig(damageReport));
        }

        // This event is called:
        // - after the boss dies in teleporter events
        // - the end of each stage of the Voidling and Mithrix fights
        // - after the Gilded Coast and A Moment Whole encounters
        //
        // It considers player count, Shrine of the Mountain activations, boss item drop chance, and other factors
        // and then spawns the necessary items
        private static void BossGroup_DropRewards(On.RoR2.BossGroup.orig_DropRewards orig, BossGroup self)
        {
            bool instance = InstancingConfig.BossRewards.Value;
            if (instance && EnablePlayerDropRateScaling.Value && IsActive)
            {
                self.scaleRewardsByPlayerCount = false;
            }

            RunWithDropSource(DropSource.Other, instance, () => orig(self));
        }

        // Covers regular chests, equipment barrels, lunar pods, void cradles and Rusted Key lockboxes
        private static void ChestBehavior_ItemDrop(On.RoR2.ChestBehavior.orig_ItemDrop orig, ChestBehavior self)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.ShouldInstance(self), () => orig(self));
        }

        // Covers void potentials and Encrusted Key lockboxes
        private static void OptionChestBehavior_ItemDrop(On.RoR2.OptionChestBehavior.orig_ItemDrop orig, OptionChestBehavior self)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.ShouldInstance(self), () => orig(self));
        }

        private static void ScavBackpackBehavior_ItemDrop(On.RoR2.ScavBackpackBehavior.orig_ItemDrop orig, ScavBackpackBehavior self)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.ScavengerBags.Value, () => orig(self));
        }

        // Adaptive chests
        private static void RouletteChestController_EjectPickupServer(On.RoR2.RouletteChestController.orig_EjectPickupServer orig, RouletteChestController self, UniquePickup pickup)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.AdaptiveChests.Value, () => orig(self, pickup));
        }

        // Void seed rewards. The game drops one per player, so we only keep one when instancing.
        private static void VoidCampDeactivate_OnEnter(On.EntityStates.VoidCamp.Deactivate.orig_OnEnter orig, EntityStates.VoidCamp.Deactivate self)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.VoidSeeds.Value, () => orig(self), keepOneInEvery: KeepOnlyFirst);
        }

        // Trophy Hunter's Tricorn drops the boss item of the boss it kills
        private static bool EquipmentSlot_FireBossHunter(On.RoR2.EquipmentSlot.orig_FireBossHunter orig, EquipmentSlot self)
        {
            bool fired = false;
            RunWithDropSource(DropSource.Other, InstancingConfig.TrophyHunterDrops.Value, () => fired = orig(self));
            return fired;
        }

        // The N'tormat altar and compulsion quests drop a page, plus a book for players without one, at each player's feet.
        private static void DLC4ProgressionController_RewardAllPlayersPages(On.RoR2.DLC4ProgressionController.orig_RewardAllPlayersPages orig, DLC4ProgressionController self, TeamIndex team)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.BookRewards.Value, () => orig(self, team), forEachPlayer: true);
        }

        // Drops one page per player in the same spot, so we only keep one when instancing.
        private static void ShrineDeathBehavior_HandleSquadDefeatServer(On.RoR2.ShrineDeathBehavior.orig_HandleSquadDefeatServer orig, ShrineDeathBehavior self)
        {
            RunWithDropSource(DropSource.Other, InstancingConfig.BookRewards.Value, () => orig(self), keepOneInEvery: KeepOnlyFirst);
        }

        // N'kuhana chest pages. Each player gets their own page.
        private static void NkuhanaChestOpenPageReward_OnEnter(On.EntityStates.NkuhanaChest.OpenPageReward.orig_OnEnter orig, EntityStates.NkuhanaChest.OpenPageReward self)
        {
            RunForPlayerPile(InstancingConfig.BookRewards.Value, () => orig(self));
        }

        // N'kuhana chest books. The chest drops one book for each player without one, so each goes to one of those players.
        private static void NkuhanaChestOpenBookReward_OnEnter(On.EntityStates.NkuhanaChest.OpenBookReward.orig_OnEnter orig, EntityStates.NkuhanaChest.OpenBookReward self)
        {
            var owners = new Queue<NetworkConnection>();
            foreach (var teamMember in TeamComponent.GetTeamMembers(TeamIndex.Player))
            {
                CharacterBody body = teamMember.body;
                if (body && body.isPlayerControlled && body.inventory && body.inventory.keyItem != DLC4Content.KeyItems.CursedBook)
                {
                    NetworkConnection connection = GetConnection(body.master);
                    if (connection != null)
                    {
                        owners.Enqueue(connection);
                    }
                }
            }

            RunWithDropSource(DropSource.Other, InstancingConfig.BookRewards.Value, () => orig(self), owners: owners);
        }

        // The rewards below are all dropped once per player in a pile.

        private static void AurelioniteHeartActivationState_FixedUpdate(On.EntityStates.AurelioniteHeart.AurelioniteHeartActivationState.orig_FixedUpdate orig, EntityStates.AurelioniteHeart.AurelioniteHeartActivationState self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void SkyJumpDeathState_GiveColossusItem(On.EntityStates.FalseSonBoss.SkyJumpDeathState.orig_GiveColossusItem orig, EntityStates.FalseSonBoss.SkyJumpDeathState self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void GeodeShatter_OnEnter(On.EntityStates.Geode.GeodeShatter.orig_OnEnter orig, EntityStates.Geode.GeodeShatter self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void HalcyoniteShrineInteractable_DropRewards(On.RoR2.HalcyoniteShrineInteractable.orig_DropRewards orig, HalcyoniteShrineInteractable self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        // Void Fields
        private static void ArenaMissionController_EndRound(On.RoR2.ArenaMissionController.orig_EndRound orig, ArenaMissionController self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        // Simulacrum waves
        private static void InfiniteTowerWaveController_DropRewards(On.RoR2.InfiniteTowerWaveController.orig_DropRewards orig, InfiniteTowerWaveController self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void ReaperDeathState_DropCursedItems(On.EntityStates.Reaper.DeathState.orig_DropCursedItems orig, EntityStates.Reaper.DeathState self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void SolusHeartDeath_OnEnter(On.EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death.orig_OnEnter orig, EntityStates.SolusHeart.Death.SolusHeartFinaleSequence.Death self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void WakeDeactivate_OnEnter(On.EntityStates.Wake.Deactivate.orig_OnEnter orig, EntityStates.Wake.Deactivate self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        private static void WakeController_DropShamanRewards(On.RoR2.WakeController.orig_DropShamanRewards orig, WakeController self, int sacrificesKilled)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self, sacrificesKilled));
        }

        private static void ShrineDreadBehavior_ShutdownShrine(On.RoR2.ShrineDreadBehavior.orig_ShutdownShrine orig, ShrineDreadBehavior self)
        {
            RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
        }

        // Drops some rewards, optionally once per player, all in one pile.
        private static void GeodeSecretMissionRewardState_DropRewards(On.EntityStates.Missions.GeodeSecretMission.GeodeSecretMissionRewardState.orig_DropRewards orig, EntityStates.Missions.GeodeSecretMission.GeodeSecretMissionRewardState self)
        {
            if (self.geodeSecretMissionController && self.geodeSecretMissionController.increaseRewardPerPlayer)
            {
                RunForPlayerPile(InstancingConfig.EventRewards.Value, () => orig(self));
            }
            else
            {
                RunWithDropSource(DropSource.Other, InstancingConfig.EventRewards.Value, () => orig(self));
            }
        }

        private static void PickupDropletController_CreatePickupDroplet(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 orig, GenericPickupController.CreatePickupInfo pickupInfo, Vector3 position, Vector3 velocity)
        {
            if (currentDropSource == DropSource.None || !IsActive)
            {
                orig(pickupInfo, position, velocity);
                return;
            }

            if (currentDropSource == DropSource.MonsterKill)
            {
                DropRateTracker.RegisterItemDrop(pickupInfo.pickup);
            }

            if (!instanceCurrentDrops)
            {
                orig(pickupInfo, position, velocity);
                return;
            }

            int dropletIndex = dropletsForSource++;
            if (keepOneDropletInEvery > 1 && dropletIndex % keepOneDropletInEvery != 0)
            {
                return;
            }

            NetworkConnection owner = null;
            if (dropletOwners != null)
            {
                if (dropletOwners.Count == 0)
                {
                    Log.Warning($"No player left to give {pickupInfo.pickup.pickupIndex} to, it won't be instanced.");
                    orig(pickupInfo, position, velocity);
                    return;
                }
                owner = dropletOwners.Dequeue();
            }
            else if (dropForEachPlayer)
            {
                owner = FindPlayerConnectionAt(position);
                if (owner == null)
                {
                    Log.Warning($"Couldn't find which player {pickupInfo.pickup.pickupIndex} was dropped for, it won't be instanced.");
                    orig(pickupInfo, position, velocity);
                    return;
                }
            }

            orig(pickupInfo, position, velocity);

            // The droplet adds itself to the instance tracker when it is instantiated, so it is the last one in the list.
            var droplets = InstanceTracker.GetInstancesList<PickupDropletController>();
            if (droplets.Count > 0 && droplets[droplets.Count - 1].pickupState == pickupInfo.pickup)
            {
                var marker = droplets[droplets.Count - 1].gameObject.AddComponent<InstancedDropletMarker>();
                marker.owner = owner;
                marker.replacesPlayerPile = keepOneDropletInEvery > 1;
            }
            else
            {
                Log.Warning($"Couldn't find the droplet for {pickupInfo.pickup.pickupIndex}, it won't be instanced.");
            }
        }

        /// <summary>
        /// Only called by the game for pickups that should become a CommandCube, so pickups like lunar coins never get here.
        /// </summary>
        private static void PickupDropletController_CreateCommandCube(On.RoR2.PickupDropletController.orig_CreateCommandCube orig, PickupDropletController self)
        {
            if (!self.GetComponent<InstancedDropletMarker>())
            {
                orig(self);
                return;
            }

            Log.Debug($"Instancing CommandCube for {self.createPickupInfo.pickup.pickupIndex}");
            new SpawnInstancedCubeMessage(self.createPickupInfo.pickup, self.createPickupInfo.position)
                .Send(NetworkDestination.Clients);
        }

        /// <summary>
        /// Rewards for a single player become a cube only that player can see. This also covers pickups that never become
        /// a CommandCube, like the N'tormat book, so another player can't pick them up.
        /// </summary>
        private static void PickupDropletController_CreatePickup(On.RoR2.PickupDropletController.orig_CreatePickup orig, PickupDropletController self)
        {
            var marker = self.GetComponent<InstancedDropletMarker>();
            if (!marker)
            {
                orig(self);
                return;
            }

            var message = new SpawnInstancedCubeMessage(self.createPickupInfo.pickup, self.createPickupInfo.position);
            if (marker.owner != null)
            {
                Log.Debug($"Instancing CommandCube for {self.createPickupInfo.pickup.pickupIndex} for one player");
                message.Send(marker.owner);
            }
            else if (marker.replacesPlayerPile)
            {
                // We removed the other players' copies of this reward, so everyone needs a cube even if it isn't one normally.
                Log.Debug($"Instancing CommandCube for {self.createPickupInfo.pickup.pickupIndex}");
                message.Send(NetworkDestination.Clients);
            }
            else
            {
                orig(self);
            }
        }

        /// <summary>
        /// Finds the player whose body a per-player reward was dropped on.
        /// </summary>
        private static NetworkConnection FindPlayerConnectionAt(Vector3 position)
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                CharacterBody body = player.master ? player.master.GetBody() : null;
                if (body && body.corePosition == position)
                {
                    return GetConnection(player.master);
                }
            }

            return null;
        }

        private static NetworkConnection GetConnection(CharacterMaster master)
        {
            var player = master ? master.playerCharacterMasterController : null;
            return player && player.networkUser ? player.networkUser.connectionToClient : null;
        }

        private static void Interactor_AttemptInteraction(On.RoR2.Interactor.orig_AttemptInteraction orig, Interactor self, GameObject interactableObject)
        {
            if (!interactableObject || !interactableObject.GetComponent<InstancedCommandCube>())
            {
                orig(self, interactableObject);
                return;
            }

            var picker = interactableObject.GetComponent<PickupPickerController>();
            LocalUser localUser = GetLocalUser(self);
            if (!picker || picker.panelInstance || localUser == null || !localUser.cameraRigController)
            {
                return;
            }

            picker.OnDisplayBegin(null, localUser, localUser.cameraRigController);
        }

        private static void PickupPickerController_SubmitChoice(On.RoR2.PickupPickerController.orig_SubmitChoice orig, PickupPickerController self, int choiceIndex)
        {
            if (!self.GetComponent<InstancedCommandCube>())
            {
                orig(self, choiceIndex);
                return;
            }

            if ((uint)choiceIndex >= (uint)self.options.Length)
            {
                return;
            }

            ref PickupPickerController.Option option = ref self.options[choiceIndex];
            if (!option.available)
            {
                return;
            }

            Log.Info($"Claiming pickup {option.pickup.pickupIndex}");
            new ClaimInstancedPickupMessage(option.pickup, self.transform.position)
                .Send(NetworkDestination.Server);

            self.OnDisplayEnd(null, null, null);
            Destroy(self.gameObject);
        }

        /// <summary>
        /// Sets drop rate for Artifact of Sacrifice kills
        /// </summary>
        private static float Util_GetExpAdjustedDropChancePercent(
            On.RoR2.Util.orig_GetExpAdjustedDropChancePercent orig,
            float nativeBaseDropChance, UnityEngine.GameObject characterBodyObject)
        {
            // Drop chance depends on a number of factors.
            // These factors include enemy elite level, teleporter event activity, and Artifact of Swarms enablement.
            // There are also entities with a drop chance of 0%, such as the "HealingAffix" spawned by slain mending elites
            // For more details on drop rates read https://riskofrain2.fandom.com/wiki/Artifacts#Sacrifice
            float nativeDropChance = orig.Invoke(nativeBaseDropChance, characterBodyObject);

            // Other callers, like artifact trial key drops, keep their native chance.
            if (currentDropSource != DropSource.MonsterKill || !IsActive)
            {
                return nativeDropChance;
            }

            // Perform custom drop chance logic
            float finalDropChance = dropRateManager.ComputeDropChance(IsSwarmEnabled(), nativeDropChance, nativeBaseDropChance, dropChance);
            Log.Debug($"DropChance: original_base={nativeBaseDropChance} original_final={nativeDropChance}; plugin_base={dropChance}; plugin_final={finalDropChance} (Character={characterBodyObject.name})");

            DropRateTracker.RegisterDropOpportunity(finalDropChance);

            // Perform Bad Luck Protection, if necessary
            if (dropRateManager.NeedsBadLuckProtection(finalDropChance, Run.instance.GetRunStopwatch()))
            {
                return dropRateManager.GetBadLuckProtectedDropChance(finalDropChance);
            }

            return finalDropChance;
        }

        [ConCommand(commandName = "drop_rate", flags = ConVarFlags.ExecuteOnServer, helpText = "Sets Drop rate for items")]
        private static void CCSetDropRate(ConCommandArgs args)
        {
            dropChance = args.GetArgFloat(0);
            useManualDropRate = true;
            Log.Info("Drop rate set to: " + args.GetArgString(0) + "%");
        }

        [ConCommand(commandName = "drop_rate_report", flags = ConVarFlags.ExecuteOnServer, helpText = "View Drop Rate Report")]
        private static void CCGetDropRateReport(ConCommandArgs args)
        {
            if (IsActive)
            {
                DropRateTracker.LogReport();
            }
        }

        // The hotkey is a local setting, so this runs on the machine of whoever typed it.
        [ConCommand(commandName = "instance_loot_hotkey", flags = ConVarFlags.None, helpText = "Set hotkey for pulling items to you")]
        private static void CCSetHotkey(ConCommandArgs args)
        {
            var key = args.GetArgString(0);

            if (Enum.TryParse(key, true, out KeyCode keyCode))
            {
                PullItemsHotKey.Value = keyCode;
                Log.Info($"Pull items hotkey set to {keyCode}");
            }
            else
            {
                Log.Warning($"Failed to set hotkey to {key}");
            }
        }

        private static LocalUser GetLocalUser(Interactor interactor)
        {
            var body = interactor.GetComponent<CharacterBody>();
            if (body && body.master && body.master.playerCharacterMasterController)
            {
                var networkUser = body.master.playerCharacterMasterController.networkUser;
                if (networkUser && networkUser.localUser != null)
                {
                    return networkUser.localUser;
                }
            }

            return LocalUserManager.GetFirstLocalUser();
        }

        private static bool IsSacrificeEnabled()
        {
            return RunArtifactManager.instance is not null
                   && RunArtifactManager.instance.isActiveAndEnabled
                   && RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.sacrificeArtifactDef);
        }

        private static bool IsCommandEnabled()
        {
            return RunArtifactManager.instance is not null
                   && RunArtifactManager.instance.isActiveAndEnabled
                   && RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.commandArtifactDef);
        }

        private static bool IsSwarmEnabled()
        {
            return RunArtifactManager.instance is not null
                   && RunArtifactManager.instance.isActiveAndEnabled
                   && RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.swarmsArtifactDef);
        }

        private static int GetPlayerCount()
        {
            return PlayerCharacterMasterController.instances.Count(pc => pc.isConnected);
        }
    }
}
