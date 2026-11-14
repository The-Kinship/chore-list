using System;
using System.Collections.Generic;

namespace ChoreList;

/// <summary>
/// Clears ticked daily and weekly chores at the game's own reset times:
/// daily at 15:00 UTC (8 AM Pacific in winter, 7 AM in summer), weekly on Tuesday at 08:00 UTC.
/// </summary>
public static class Resets
{
    private static DateTime lastCheck = DateTime.MinValue;

    /// <summary>The most recent daily reset at or before this time (UTC).</summary>
    public static DateTime LastDaily(DateTime nowUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 15, 0, 0, DateTimeKind.Utc);
        return nowUtc >= today ? today : today.AddDays(-1);
    }

    /// <summary>The most recent weekly reset (Tuesday 08:00 UTC) at or before this time.</summary>
    public static DateTime LastWeekly(DateTime nowUtc)
    {
        var day = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 8, 0, 0, DateTimeKind.Utc);
        var back = ((int)day.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        var tuesday = day.AddDays(-back);
        return nowUtc >= tuesday ? tuesday : tuesday.AddDays(-7);
    }

    public static DateTime NextDaily(DateTime nowUtc) => LastDaily(nowUtc).AddDays(1);
    public static DateTime NextWeekly(DateTime nowUtc) => LastWeekly(nowUtc).AddDays(7);

    /// <summary>Runs every frame but only looks every few seconds. Unticks anything whose reset has passed since it was ticked.</summary>
    public static void Tick()
    {
        var now = DateTime.UtcNow;
        if ((now - lastCheck).TotalSeconds < 5) return;
        lastCheck = now;

        var cfg = Plugin.Config;
        var changed = Clear(cfg.Shared, now);
        foreach (var list in cfg.Characters.Values) changed |= Clear(list.Chores, now);
        if (changed) cfg.Save();
    }

    private static bool Clear(List<Chore> chores, DateTime now)
    {
        var changed = false;
        foreach (var c in chores)
        {
            if (!c.Done || c.Reset == ResetKind.Never) continue;
            var since = c.Reset == ResetKind.Daily ? LastDaily(now) : LastWeekly(now);
            if (c.DoneAt == null || c.DoneAt.Value < since)
            {
                c.Done = false;
                c.DoneAt = null;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>"5h 12m" or "2d 4h": how long until a reset.</summary>
    public static string Until(DateTime when, DateTime nowUtc)
    {
        var t = when - nowUtc;
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
        return $"{Math.Max(1, (int)Math.Ceiling(t.TotalMinutes))}m";
    }
}
