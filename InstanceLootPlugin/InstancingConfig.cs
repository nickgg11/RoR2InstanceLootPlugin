using BepInEx.Configuration;
using RoR2;
using UnityEngine;

namespace InstanceLootPlugin
{
    /// <summary>
    /// Config options that control which drop sources give each player their own Command cube.
    /// Sources that are turned off drop one shared cube like vanilla.
    /// </summary>
    internal static class InstancingConfig
    {
        private const string Section = "Instancing";

        public static ConfigEntry<bool> MonsterDrops { get; private set; }
        public static ConfigEntry<bool> BossRewards { get; private set; }
        public static ConfigEntry<bool> Chests { get; private set; }
        public static ConfigEntry<bool> EquipmentBarrels { get; private set; }
        public static ConfigEntry<bool> LunarPods { get; private set; }
        public static ConfigEntry<bool> VoidCradles { get; private set; }
        public static ConfigEntry<bool> AdaptiveChests { get; private set; }
        public static ConfigEntry<bool> ScavengerBags { get; private set; }
        public static ConfigEntry<bool> VoidPotentials { get; private set; }
        public static ConfigEntry<bool> VoidSeeds { get; private set; }
        public static ConfigEntry<bool> Lockboxes { get; private set; }
        public static ConfigEntry<bool> TrophyHunterDrops { get; private set; }
        public static ConfigEntry<bool> BookRewards { get; private set; }
        public static ConfigEntry<bool> EventRewards { get; private set; }

        public static void Bind(ConfigFile config)
        {
            MonsterDrops = config.Bind(Section, "MonsterDrops", true,
                "Instance items dropped by monsters killed with Artifact of Sacrifice.");

            BossRewards = config.Bind(Section, "BossRewards", true,
                "Instance teleporter and boss rewards (teleporter bosses, Mithrix, Voidling, Aurelionite, etc.). " +
                "When instanced with EnablePlayerBasedDropRateScaling on, the game drops one set of rewards and every player gets their own copy. " +
                "When off, the game drops one shared reward per player like vanilla.");

            Chests = config.Bind(Section, "Chests", true,
                "Instance regular chests (small, large, legendary and category chests).");

            EquipmentBarrels = config.Bind(Section, "EquipmentBarrels", true,
                "Instance equipment barrels.");

            LunarPods = config.Bind(Section, "LunarPods", true,
                "Instance lunar pods.");

            VoidCradles = config.Bind(Section, "VoidCradles", true,
                "Instance void cradles.");

            AdaptiveChests = config.Bind(Section, "AdaptiveChests", true,
                "Instance adaptive chests.");

            ScavengerBags = config.Bind(Section, "ScavengerBags", true,
                "Instance the bags dropped by Scavengers.");

            VoidPotentials = config.Bind(Section, "VoidPotentials", true,
                "Instance void potentials.");

            VoidSeeds = config.Bind(Section, "VoidSeeds", true,
                "Instance void seed rewards. When instanced, each player gets one reward. When off, the game drops one shared reward per player like vanilla.");

            Lockboxes = config.Bind(Section, "Lockboxes", false,
                "Instance Rusted Key and Encrusted Key lockboxes.");

            TrophyHunterDrops = config.Bind(Section, "TrophyHunterDrops", true,
                "Instance the boss item dropped when Trophy Hunter's Tricorn kills a boss.");

            BookRewards = config.Bind(Section, "BookRewards", true,
                "Instance N'tormat book and page rewards. When instanced, each player only sees their own book and page. " +
                "When off, they drop as shared pickups like vanilla.");

            EventRewards = config.Bind(Section, "EventRewards", true,
                "Instance rewards that the game drops once per player in a pile: Void Fields, Simulacrum waves, " +
                "Halcyonite shrines, the Aurelionite heart, False Son, geodes and the newer encounter and shrine rewards. " +
                "When instanced, each player gets one reward. When off, the game drops one shared reward per player like vanilla.");
        }

        // Most chests share the same ChestBehavior component, so we tell them apart by what they cost and what they drop.
        public static bool ShouldInstance(ChestBehavior chest)
        {
            if (IsLockbox(chest))
            {
                return Lockboxes.Value;
            }

            if (CostType(chest) == CostTypeIndex.LunarCoin)
            {
                return LunarPods.Value;
            }

            PickupDef pickupDef = PickupCatalog.GetPickupDef(chest.currentPickup.pickupIndex);
            if (pickupDef != null && IsVoidTier(pickupDef.itemTier))
            {
                return VoidCradles.Value;
            }

            if (pickupDef != null && pickupDef.equipmentIndex != EquipmentIndex.None)
            {
                return EquipmentBarrels.Value;
            }

            return Chests.Value;
        }

        public static bool ShouldInstance(OptionChestBehavior chest)
        {
            return IsLockbox(chest) ? Lockboxes.Value : VoidPotentials.Value;
        }

        // Rusted Key and Encrusted Key lockboxes are paid for with their key.
        private static bool IsLockbox(Component chest)
        {
            CostTypeIndex costType = CostType(chest);
            return costType == CostTypeIndex.TreasureCacheItem || costType == CostTypeIndex.TreasureCacheVoidItem;
        }

        private static CostTypeIndex CostType(Component chest)
        {
            var purchaseInteraction = chest.GetComponent<PurchaseInteraction>();
            return purchaseInteraction ? purchaseInteraction.costType : CostTypeIndex.None;
        }

        private static bool IsVoidTier(ItemTier tier)
        {
            return tier == ItemTier.VoidTier1
                   || tier == ItemTier.VoidTier2
                   || tier == ItemTier.VoidTier3
                   || tier == ItemTier.VoidBoss;
        }
    }
}
