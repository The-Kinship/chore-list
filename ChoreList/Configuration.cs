using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace ChoreList;

/// <summary>How often a ticked chore clears itself. (Used from piece 2 on; everything is "Never" for now.)</summary>
public enum ResetKind
{
    Never = 0,
    Daily = 1,
    Weekly = 2,
}

[Serializable]
public class Chore
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    /// <summary>When it was ticked (UTC), so daily and weekly chores know when to clear.</summary>
    public DateTime? DoneAt { get; set; }
    public ResetKind Reset { get; set; } = ResetKind.Never;
    /// <summary>Heading this chore sits under (empty = no heading). Used from piece 3 on.</summary>
    public string Group { get; set; } = "";
}

[Serializable]
public class CharacterChores
{
    /// <summary>The character's name, only for showing. The list is matched by the character's permanent ID.</summary>
    public string Name { get; set; } = "";
    public List<Chore> Chores { get; set; } = new();
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>Open the Chore List window every time you log in.</summary>
    public bool OpenOnLogin { get; set; } = true;

    /// <summary>Show the "Chores 3/7" counter in the server info bar at the top of the screen.</summary>
    public bool ShowTopBar { get; set; } = true;

    /// <summary>Chores every character sees.</summary>
    public List<Chore> Shared { get; set; } = new();

    /// <summary>Each character's own chores, by character ID.</summary>
    public Dictionary<ulong, CharacterChores> Characters { get; set; } = new();

    /// <summary>The list for this character (made empty the first time), keeping its shown name up to date.</summary>
    public CharacterChores ForCharacter(ulong id, string name)
    {
        if (!Characters.TryGetValue(id, out var list))
        {
            list = new CharacterChores { Name = name };
            Characters[id] = list;
            Save();
        }
        else if (name.Length > 0 && list.Name != name)
        {
            list.Name = name;
            Save();
        }
        return list;
    }

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
