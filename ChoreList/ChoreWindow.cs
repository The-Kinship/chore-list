using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ChoreList;

/// <summary>The Chore List window: the shared list on top, this character's list underneath.</summary>
public sealed class ChoreWindow : Window
{
    private static readonly Vector4 Gold = new(0.92f, 0.78f, 0.35f, 1f);
    private static readonly Vector4 Dim = new(0.6f, 0.6f, 0.65f, 1f);

    /// <summary>What's typed in each list's "add" box, by list.</summary>
    private readonly Dictionary<string, string> typing = new();
    /// <summary>List whose add box should get the keyboard back next frame (after pressing Enter).</summary>
    private string refocus = "";
    /// <summary>Reset chosen in each list's add box, by list.</summary>
    private readonly Dictionary<string, ResetKind> newReset = new();
    /// <summary>Heading chosen in each list's add box, by list ("" = no heading).</summary>
    private readonly Dictionary<string, string> newHeading = new();
    /// <summary>What's typed in a "new heading" box.</summary>
    private string headingTyping = "";
    /// <summary>What's typed in a heading's "rename" box.</summary>
    private string renameTyping = "";

    /// <summary>A change to make once the list has finished drawing (lists can't change while being drawn).</summary>
    private Action? pending;

    private static readonly ResetKind[] Kinds = { ResetKind.Never, ResetKind.Daily, ResetKind.Weekly };
    private static string KindName(ResetKind k) => k switch { ResetKind.Daily => "Daily", ResetKind.Weekly => "Weekly", _ => "Never" };

    public ChoreWindow() : base("Rafter Cat's Chore List##ChoreListMain")
    {
        Size = new Vector2(340, 420);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(240, 160), MaximumSize = new Vector2(2000, 2000) };
    }

    public override void Draw()
    {
        var cfg = Plugin.Config;

        DrawList("All characters", "shared", cfg.Shared);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var id = Plugin.ContentId();
        var name = Plugin.CharacterName();
        if (id == 0 || name.Length == 0)
        {
            ImGui.TextColored(Dim, "Log in to see this character's own list.");
        }
        else
        {
            var mine = cfg.ForCharacter(id, name);
            DrawList(name, "char" + id, mine.Chores);
        }

        if (pending != null)
        {
            pending();
            pending = null;
            cfg.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        var open = cfg.OpenOnLogin;
        if (ImGui.Checkbox("Open when I log in", ref open)) { cfg.OpenOnLogin = open; cfg.Save(); }
        var now = DateTime.UtcNow;
        ImGui.TextColored(Dim, $"Daily reset in {Resets.Until(Resets.NextDaily(now), now)}, weekly in {Resets.Until(Resets.NextWeekly(now), now)}");
        ImGui.TextColored(Dim, "Right-click a chore or heading for more options.");
    }

    /// <summary>Headings in the order they first appear in the list.</summary>
    private static List<string> HeadingsOf(List<Chore> chores) =>
        chores.Where(c => c.Group.Length > 0).Select(c => c.Group).Distinct().ToList();

    /// <summary>One list: its title, chores with no heading, then each heading (folds open and shut), then the add box.</summary>
    private void DrawList(string title, string key, List<Chore> chores)
    {
        var done = chores.Count(c => c.Done);
        ImGui.TextColored(Gold, chores.Count > 0 ? $"{title}  ({done}/{chores.Count})" : title);

        var headings = HeadingsOf(chores);
        ImGui.PushID(key);

        foreach (var c in chores.Where(c => c.Group.Length == 0).ToList())
            DrawChore(c, chores, headings);

        for (var h = 0; h < headings.Count; h++)
        {
            var heading = headings[h];
            var inIt = chores.Where(c => c.Group == heading).ToList();
            var hd = inIt.Count(c => c.Done);
            ImGui.PushID("h:" + heading);
            var isOpen = ImGui.CollapsingHeader($"{heading.Replace("##", "# #")}  ({hd}/{inIt.Count})###head", ImGuiTreeNodeFlags.DefaultOpen);
            DrawHeadingMenu(heading, h, headings, chores);
            if (isOpen)
            {
                ImGui.Indent(8f);
                foreach (var c in inIt) DrawChore(c, chores, headings);
                ImGui.Unindent(8f);
            }
            ImGui.PopID();
        }

        if (chores.Count == 0) ImGui.TextColored(Dim, "Nothing here yet.");

        DrawAddBox(key, chores, headings);
        ImGui.PopID();
    }

    /// <summary>One chore: its checkbox, and the right-click menu (edit, reset, heading, move, delete).</summary>
    private void DrawChore(Chore c, List<Chore> chores, List<string> headings)
    {
        ImGui.PushID(c.Id);
        var ticked = c.Done;
        // "##" in a label hides what follows, so break it up if someone types it.
        var label = c.Text.Replace("##", "# #");
        if (c.Reset == ResetKind.Daily) label += "  (daily)";
        else if (c.Reset == ResetKind.Weekly) label += "  (weekly)";
        if (c.Done) ImGui.PushStyleColor(ImGuiCol.Text, Dim);
        var changed = ImGui.Checkbox(label + "##tick", ref ticked);
        if (c.Done) ImGui.PopStyleColor();
        if (changed)
        {
            c.Done = ticked;
            c.DoneAt = ticked ? DateTime.UtcNow : null;
            Plugin.Config.Save();
        }

        if (ImGui.BeginPopupContextItem("menu"))
        {
            ImGui.TextColored(Dim, "Chore");
            var text = c.Text;
            ImGui.SetNextItemWidth(220f);
            if (ImGui.InputText("##edit", ref text, 120) && text.Trim().Length > 0)
            {
                c.Text = text;
                Plugin.Config.Save();
            }

            ImGui.Spacing();
            ImGui.TextColored(Dim, "Clears itself:");
            foreach (var k in Kinds)
            {
                ImGui.SameLine();
                if (ImGui.RadioButton(KindName(k) + "##r" + (int)k, c.Reset == k))
                {
                    c.Reset = k;
                    Plugin.Config.Save();
                }
            }

            ImGui.Spacing();
            ImGui.TextColored(Dim, "Heading:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150f);
            if (HeadingPicker("##hp", c.Group, headings, out var chosen))
            {
                var chore = c;
                pending = () => MoveToHeading(chores, chore, chosen);
            }

            ImGui.Separator();
            if (ImGui.Selectable("Move up")) { var chore = c; pending = () => MoveChore(chores, chore, -1); }
            if (ImGui.Selectable("Move down")) { var chore = c; pending = () => MoveChore(chores, chore, +1); }
            ImGui.Separator();
            if (ImGui.Selectable("Delete")) { var chore = c; pending = () => chores.Remove(chore); }
            ImGui.EndPopup();
        }
        ImGui.PopID();
    }

    /// <summary>Right-click menu on a heading: rename it, move it up or down, or remove the heading (keeping its chores).</summary>
    private void DrawHeadingMenu(string heading, int index, List<string> headings, List<Chore> chores)
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) renameTyping = heading;
        if (!ImGui.BeginPopupContextItem("hmenu")) return;

        ImGui.TextColored(Dim, "Rename (press Enter):");
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("##rename", ref renameTyping, 40, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            var to = renameTyping.Trim();
            if (to.Length > 0 && to != heading)
            {
                pending = () => { foreach (var c in chores.Where(c => c.Group == heading)) c.Group = to; };
                ImGui.CloseCurrentPopup();
            }
        }
        ImGui.Separator();
        if (index > 0 && ImGui.Selectable("Move heading up")) pending = () => MoveHeading(chores, heading, -1);
        if (index < headings.Count - 1 && ImGui.Selectable("Move heading down")) pending = () => MoveHeading(chores, heading, +1);
        if (ImGui.Selectable("Remove heading (keeps its chores)"))
            pending = () => { foreach (var c in chores.Where(c => c.Group == heading)) c.Group = ""; };
        ImGui.EndPopup();
    }

    /// <summary>A drop-down of headings: "No heading", the existing ones, and a box to type a new one. True when one is picked.</summary>
    private bool HeadingPicker(string id, string current, List<string> headings, out string chosen)
    {
        chosen = current;
        var picked = false;
        if (!ImGui.BeginCombo(id, current.Length == 0 ? "No heading" : current)) return false;

        if (ImGui.Selectable("No heading", current.Length == 0)) { chosen = ""; picked = true; }
        foreach (var h in headings)
            if (ImGui.Selectable(h + "##pick", h == current)) { chosen = h; picked = true; }

        ImGui.Separator();
        ImGui.TextColored(Dim, "New heading (press Enter):");
        ImGui.SetNextItemWidth(160f);
        if (ImGui.InputText("##newheading", ref headingTyping, 40, ImGuiInputTextFlags.EnterReturnsTrue) && headingTyping.Trim().Length > 0)
        {
            chosen = headingTyping.Trim();
            headingTyping = "";
            picked = true;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndCombo();
        return picked;
    }

    /// <summary>Add box: type a chore, pick its reset and heading, press Enter (or Add).</summary>
    private void DrawAddBox(string key, List<Chore> chores, List<string> headings)
    {
        typing.TryGetValue(key, out var text);
        text ??= "";
        newReset.TryGetValue(key, out var kind);
        newHeading.TryGetValue(key, out var heading);
        heading ??= "";
        // A heading that no longer has any chores (and isn't brand new) falls back to none.
        if (heading.Length > 0 && !headings.Contains(heading) && !freshHeadings.Contains(heading)) heading = "";

        ImGui.Spacing();
        if (refocus == key) { ImGui.SetKeyboardFocusHere(); refocus = ""; }
        ImGui.SetNextItemWidth(Math.Max(60f, ImGui.GetContentRegionAvail().X - 50f));
        var enter = ImGui.InputText("##add", ref text, 120, ImGuiInputTextFlags.EnterReturnsTrue);
        typing[key] = text;
        ImGui.SameLine();
        var clicked = ImGui.Button("Add");

        ImGui.SetNextItemWidth(80f);
        if (ImGui.BeginCombo("##kind", KindName(kind)))
        {
            foreach (var k in Kinds)
                if (ImGui.Selectable(KindName(k), k == kind)) { kind = k; newReset[key] = k; }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("When a ticked chore clears itself:\nDaily = at the daily reset\nWeekly = at the Tuesday reset\nNever = stays ticked until you untick it");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(80f, ImGui.GetContentRegionAvail().X));
        if (HeadingPicker("##addheading", heading, headings, out var chosen))
        {
            heading = chosen;
            newHeading[key] = chosen;
            if (chosen.Length > 0) freshHeadings.Add(chosen);
        }

        if ((enter || clicked) && text.Trim().Length > 0)
        {
            chores.Add(new Chore { Text = text.Trim(), Reset = kind, Group = heading });
            typing[key] = "";
            Plugin.Config.Save();
            if (enter) refocus = key;   // keep typing the next one
        }
    }

    /// <summary>Headings picked for the add box that don't have any chores yet (so they aren't dropped straight away).</summary>
    private readonly HashSet<string> freshHeadings = new();

    /// <summary>Swaps a chore with the next one up or down under the same heading.</summary>
    private static void MoveChore(List<Chore> chores, Chore c, int dir)
    {
        var i = chores.IndexOf(c);
        if (i < 0) return;
        var j = i + dir;
        while (j >= 0 && j < chores.Count && chores[j].Group != c.Group) j += dir;
        if (j < 0 || j >= chores.Count) return;
        (chores[i], chores[j]) = (chores[j], chores[i]);
    }

    /// <summary>Puts a chore under another heading, at the bottom of it.</summary>
    private static void MoveToHeading(List<Chore> chores, Chore c, string heading)
    {
        if (c.Group == heading) return;
        chores.Remove(c);
        c.Group = heading;
        var last = chores.FindLastIndex(x => x.Group == heading);
        chores.Insert(last < 0 ? chores.Count : last + 1, c);
    }

    /// <summary>Moves a whole heading (and its chores) above or below its neighbour.</summary>
    private static void MoveHeading(List<Chore> chores, string heading, int dir)
    {
        var order = HeadingsOf(chores);
        var i = order.IndexOf(heading);
        var j = i + dir;
        if (i < 0 || j < 0 || j >= order.Count) return;
        (order[i], order[j]) = (order[j], order[i]);
        var rebuilt = chores.Where(c => c.Group.Length == 0).ToList();
        foreach (var h in order) rebuilt.AddRange(chores.Where(c => c.Group == h));
        chores.Clear();
        chores.AddRange(rebuilt);
    }
}
