using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;

namespace InstanceLootPlugin;

public static class DropRateTracker
{
    public static long totalItemDrops { get; private set; } = 0;
    public static float cumulativeDropChance { get; private set; } = 0;
    public static float expectedItems { get; private set; } = 0;

    private static Dictionary<float, long> DropRateReport = new Dictionary<float, long>();

    public static void ResetTracker()
    {
        DropRateReport = new Dictionary<float, long>();
        totalItemDrops = 0;
        cumulativeDropChance = 0;
        expectedItems = 0;
    }

    public static void RegisterDropOpportunity(float dropChance)
    {
        cumulativeDropChance += dropChance;
        expectedItems += (float)Math.Round(dropChance / 100, 2);

        long value;
        if (DropRateReport.TryGetValue(dropChance, out value))
        {
            DropRateReport[dropChance] += 1;
        }
        else
        {
            DropRateReport.Add(dropChance, 1);
        }
    }

    /// <summary>
    /// Called when a monster kill creates a droplet, so bad luck protection sees the drop before the next kill is rolled.
    /// </summary>
    public static void RegisterItemDrop(UniquePickup pickup)
    {
        var def = pickup.pickupIndex.pickupDef;
        if (def is not null)
        {
            bool isItem = def.coinValue == 0 && def.nameToken != "PICKUP_LUNAR_COIN";
            if (isItem)
            {
                cumulativeDropChance = 0;
                totalItemDrops += 1;
            }
        }
    }

    public static void LogReport()
    {
        if (Run.instance is not null && Run.instance.isActiveAndEnabled)
        {
            var totalKills = DropRateReport.Values.Aggregate(0L, ((i, l) => i + l));
            if (totalKills == 0)
                return;

            var dropRateBreakdown = DropRateReport.Keys
                .OrderByDescending(f => f)
                .Aggregate("Kills by Drop Rate", (s, f) => $"{s}\n  {f}%: {DropRateReport[f]} kills");

            var timer = Run.instance.GetRunStopwatch();
            var minutes = timer / 60;
            var averageDropRate = DropRateReport.Keys.Aggregate(0f,
                (f, f1) => f + (DropRateReport[f1] * f1)) / totalKills;

            string report = "------ DROP RATE RUN REPORT -----\n";
            report += $"Stage: {Run.instance.stageClearCount + 1}\n";
            report += $"Run Duration: {timer}s\n";
            report += $"Total kills: {totalKills}\n";
            report += $"Total items: {totalItemDrops}\n";
            report += $"Theoretical Average Drop Rate: {averageDropRate}%\n";
            report += $"Actual Average Drop Rate: {((float)totalItemDrops / (float)totalKills) * 100}%\n";
            report += $"Drops per Minute: {Math.Round(totalItemDrops / minutes, 2)}\n";
            report += dropRateBreakdown + "\n";
            report += "------ END OF REPORT -----";
            Log.Info(report);
        }
    }
}