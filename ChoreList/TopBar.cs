using System;
using System.Linq;
using System.Linq.Expressions;

namespace ChoreList;

/// <summary>
/// A small "Chores 3/7" entry in the game's server info bar at the top of the screen. Click it to open or close the list.
/// The bar's text and click types change between Dalamud versions, so those are set up by reflection and fail quietly.
/// (Same way the RP Toolbox's top bar button works.)
/// </summary>
public sealed class TopBar : IDisposable
{
    private readonly Dalamud.Game.Gui.Dtr.IDtrBarEntry? entry;
    private readonly Action toggle;
    private string lastText = "";

    public TopBar(Action toggle)
    {
        this.toggle = toggle;
        try
        {
            entry = Plugin.DtrBar.Get("Rafter Cat's Chore List");
            SetText("Tooltip", "Rafter Cat's Chore List: click to open or close");
            Hook();
            entry.Shown = Plugin.Config.ShowTopBar;
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "Could not add the top bar counter."); }
    }

    /// <summary>Called every frame: keeps the count up to date and shows or hides the entry.</summary>
    public void Update()
    {
        if (entry == null) return;
        var cfg = Plugin.Config;
        if (entry.Shown != cfg.ShowTopBar) entry.Shown = cfg.ShowTopBar;
        if (!cfg.ShowTopBar) return;

        var chores = cfg.Shared.AsEnumerable();
        var id = Plugin.ContentId();
        if (id != 0 && cfg.Characters.TryGetValue(id, out var mine)) chores = chores.Concat(mine.Chores);
        var list = chores.ToList();
        var done = list.Count(c => c.Done);
        var want = list.Count == 0 ? "Chores" : done == list.Count ? $"Chores {done}/{list.Count} done!" : $"Chores {done}/{list.Count}";
        var due = list.Count(Dates.Urgent);
        if (due > 0) want += $" - {due} due!";
        if (want != lastText) { lastText = want; SetText("Text", want); }
    }

    private static object? ToValue(Type type, string text)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(string)) return text;
        try { return Expression.Lambda(Expression.Convert(Expression.Constant(text), t)).Compile().DynamicInvoke(); } catch (Exception) { /* try the next way */ }
        try { return Activator.CreateInstance(t, new object[] { text }); } catch (Exception) { return null; }
    }

    private void SetText(string property, string text)
    {
        try
        {
            var p = entry!.GetType().GetProperty(property);
            if (p == null) return;
            var v = ToValue(p.PropertyType, text);
            if (v != null) p.SetValue(entry, v);
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "Top bar " + property + " not set."); }
    }

    private void Hook()
    {
        try
        {
            var p = entry!.GetType().GetProperty("OnClick");
            if (p == null) return;
            var delegateType = p.PropertyType;
            var parameters = delegateType.GetMethod("Invoke")!.GetParameters().Select(x => Expression.Parameter(x.ParameterType)).ToArray();
            Expression arg = parameters.Length > 0 ? Expression.Convert(parameters[0], typeof(object)) : Expression.Constant(null, typeof(object));
            var call = Expression.Call(Expression.Constant(this), typeof(TopBar).GetMethod(nameof(Clicked))!, arg);
            p.SetValue(entry, Expression.Lambda(delegateType, call, parameters).Compile());
        }
        catch (Exception ex) { Plugin.Log.Warning(ex, "Top bar click not hooked."); }
    }

    public void Clicked(object? evt) => toggle();

    public void Dispose()
    {
        try { entry?.Remove(); } catch (Exception) { /* already gone */ }
    }
}
