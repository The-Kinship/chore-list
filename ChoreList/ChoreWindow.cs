using System;
using System.Collections.Generic;
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

    private static readonly ResetKind[] Kinds = { ResetKind.Never, ResetKind.Daily, ResetKind.Weekly };
    private static string KindName(ResetKind k) => k switch { ResetKind.Daily => "Daily", ResetKind.Weekly => "Weekly", _ => "Never" };

    public ChoreWindow() : base("Rafter Cat's Chore List##ChoreListMain")
    {
        Size = new Vector2(320, 380);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(220, 160), MaximumSize = new Vector2(2000, 2000) };
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

        ImGui.Spacing();
        ImGui.Separator();
        var open = cfg.OpenOnLogin;
        if (ImGui.Checkbox("Open when I log in", ref open)) { cfg.OpenOnLogin = open; cfg.Save(); }
        var now = DateTime.UtcNow;
        ImGui.TextColored(Dim, $"Daily reset in {Resets.Until(Resets.NextDaily(now), now)}, weekly in {Resets.Until(Resets.NextWeekly(now), now)}");
        ImGui.TextColored(Dim, "Right-click a chore to change its reset or delete it.");
    }

    /// <summary>One list: a heading, its chores with checkboxes, and a box to add more.</summary>
    private void DrawList(string title, string key, List<Chore> chores)
    {
        var done = 0;
        foreach (var c in chores) if (c.Done) done++;
        ImGui.TextColored(Gold, chores.Count > 0 ? $"{title}  ({done}/{chores.Count})" : title);

        Chore? remove = null;
        ImGui.PushID(key);
        for (var i = 0; i < chores.Count; i++)
        {
            var c = chores[i];
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
            var menuOpen = ImGui.BeginPopupContextItem("menu");
            if (menuOpen)
            {
                ImGui.TextColored(Dim, c.Text);
                ImGui.Separator();
                ImGui.TextColored(Dim, "Clears itself:");
                foreach (var k in Kinds)
                {
                    if (ImGui.RadioButton(KindName(k) + "##r" + (int)k, c.Reset == k))
                    {
                        c.Reset = k;
                        Plugin.Config.Save();
                    }
                }
                ImGui.Separator();
                if (ImGui.Selectable("Delete")) remove = c;
                ImGui.EndPopup();
            }

            ImGui.PopID();
        }

        if (chores.Count == 0) ImGui.TextColored(Dim, "Nothing here yet.");

        // Add box: type a chore and press Enter (or Add).
        typing.TryGetValue(key, out var text);
        text ??= "";
        if (refocus == key) { ImGui.SetKeyboardFocusHere(); refocus = ""; }
        newReset.TryGetValue(key, out var kind);
        ImGui.SetNextItemWidth(Math.Max(60f, ImGui.GetContentRegionAvail().X - 140f));
        var enter = ImGui.InputText("##add", ref text, 120, ImGuiInputTextFlags.EnterReturnsTrue);
        typing[key] = text;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        if (ImGui.BeginCombo("##kind", KindName(kind)))
        {
            foreach (var k in Kinds)
                if (ImGui.Selectable(KindName(k), k == kind)) { kind = k; newReset[key] = k; }
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("When a ticked chore clears itself:\nDaily = at the daily reset\nWeekly = at the Tuesday reset\nNever = stays ticked until you untick it");
        ImGui.SameLine();
        var clicked = ImGui.Button("Add");
        if ((enter || clicked) && text.Trim().Length > 0)
        {
            chores.Add(new Chore { Text = text.Trim(), Reset = kind });
            typing[key] = "";
            Plugin.Config.Save();
            if (enter) refocus = key;   // keep typing the next one
        }
        ImGui.PopID();

        if (remove != null)
        {
            chores.Remove(remove);
            Plugin.Config.Save();
        }
    }
}
