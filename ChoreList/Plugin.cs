using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ChoreList;

public sealed class Plugin : IDalamudPlugin
{
    private static readonly string[] CommandNames = { "/chores", "/chorelist" };

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;

    internal static Configuration Config { get; private set; } = null!;

    /// <summary>The logged in character's permanent ID (0 when nobody is logged in).</summary>
    internal static ulong ContentId()
    {
        try { return ClientState.IsLoggedIn ? PlayerState.ContentId : 0; } catch (Exception) { return 0; }
    }

    /// <summary>The logged in character's name ("" while loading in).</summary>
    internal static string CharacterName()
    {
        try { return ObjectTable.LocalPlayer?.Name.ToString() ?? ""; } catch (Exception) { return ""; }
    }

    private readonly WindowSystem windows = new("ChoreList");
    private readonly ChoreWindow window;

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        window = new ChoreWindow();
        windows.AddWindow(window);

        foreach (var cmd in CommandNames)
            CommandManager.AddHandler(cmd, new CommandInfo(OnCommand) { HelpMessage = "Open or close Rafter Cat's Chore List." });

        PluginInterface.UiBuilder.Draw += Resets.Tick;
        PluginInterface.UiBuilder.Draw += windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi += Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += Toggle;

        ClientState.Login += OnLogin;
        // Just installed or updated while already in game: show it straight away.
        if (ClientState.IsLoggedIn && Config.OpenOnLogin) window.IsOpen = true;
    }

    private void OnLogin()
    {
        if (Config.OpenOnLogin) window.IsOpen = true;
    }

    private void OnCommand(string command, string args) => Toggle();

    private void Toggle() => window.Toggle();

    public void Dispose()
    {
        ClientState.Login -= OnLogin;
        PluginInterface.UiBuilder.Draw -= Resets.Tick;
        PluginInterface.UiBuilder.Draw -= windows.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= Toggle;
        foreach (var cmd in CommandNames) CommandManager.RemoveHandler(cmd);
        windows.RemoveAllWindows();
    }
}
