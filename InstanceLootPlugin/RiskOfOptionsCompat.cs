using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace InstanceLootPlugin
{
    /// <summary>
    /// Optional integration with Risk of Options, which adds our settings to the in-game mod options menu.
    ///
    /// Risk of Options is a soft dependency, so its types must only be touched after <see cref="Enabled"/> confirms
    /// it's loaded. The methods that reference them are marked NoInlining so the JIT doesn't try to resolve
    /// RiskOfOptions.dll when it isn't installed.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string PluginGUID = "com.rune580.riskofoptions";

        public static bool Enabled => Chainloader.PluginInfos.ContainsKey(PluginGUID);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void AddPullItemsHotkey(ConfigEntry<KeyboardShortcut> hotkey)
        {
            ModSettingsManager.SetModDescription("Instance-based loot for Artifact of Sacrifice + Artifact of Command.");
            ModSettingsManager.AddOption(new KeyBindOption(hotkey));
        }
    }
}
