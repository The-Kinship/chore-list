using System;
using System.Globalization;
using System.Numerics;

namespace ChoreList;

/// <summary>Due dates, start dates and repeating chores: what each chore should say, in words (not just colors).</summary>
public static class Dates
{
    public static readonly Vector4 Red = new(1f, 0.4f, 0.35f, 1f);
    public static readonly Vector4 Orange = new(1f, 0.7f, 0.25f, 1f);
    public static readonly Vector4 Dim = new(0.6f, 0.6f, 0.65f, 1f);

    /// <summary>How many days before a repeating chore's next due date it unticks itself.</summary>
    public const int RepeatHeadsUpDays = 3;

    public static string Short(DateTime d) => d.ToString("MMM d", CultureInfo.InvariantCulture);

    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    /// <summary>True once the chore's start date (if any) has arrived.</summary>
    public static bool Started(Chore c) => c.Start == null || DateTime.Today >= c.Start.Value.Date;

    /// <summary>Not ticked, started, and due today or earlier.</summary>
    public static bool Urgent(Chore c) => !c.Done && Started(c) && c.Due != null && c.Due.Value.Date <= DateTime.Today;

    /// <summary>The line shown next to the chore ("" for none), and its color.</summary>
    public static string Status(Chore c, out Vector4 color)
    {
        color = Dim;
        var today = DateTime.Today;
        if (c.Done)
            return c.RepeatDays > 0 && c.Due != null ? $"next due {Short(c.Due.Value)}" : "";
        if (!Started(c))
            return $"starts {Short(c.Start!.Value)} (in {Days((c.Start.Value.Date - today).Days)})";
        if (c.Due == null) return "";

        var left = (c.Due.Value.Date - today).Days;
        if (left < 0) { color = Red; return $"OVERDUE (was due {Short(c.Due.Value)})"; }
        if (left == 0) { color = Orange; return "DUE TODAY"; }
        if (left <= 3) { color = Orange; return $"due {Short(c.Due.Value)}, in {Days(left)}"; }
        return $"due {Short(c.Due.Value)} (in {Days(left)})";
    }

    /// <summary>Called when a chore is ticked: a repeating chore's next due date moves ahead.</summary>
    public static void OnTicked(Chore c)
    {
        if (c.RepeatDays > 0) c.Due = DateTime.Today.AddDays(c.RepeatDays);
    }

    /// <summary>A ticked repeating chore comes back a few days before its next due date. True if it changed.</summary>
    public static bool RepeatCheck(Chore c)
    {
        if (!c.Done || c.RepeatDays <= 0 || c.Due == null) return false;
        if (DateTime.Today < c.Due.Value.Date.AddDays(-RepeatHeadsUpDays)) return false;
        c.Done = false;
        c.DoneAt = null;
        return true;
    }
}
