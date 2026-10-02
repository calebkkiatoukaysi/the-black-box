using System;
using System.Collections.Generic;
using TheBlackBox.Audio;

namespace TheBlackBox;

/// <summary>
/// Plays the sound that goes with a line of the round as it is read out.
/// </summary>
/// <remarks>
/// The round is decided all at once and then read one line at a time, so the sounds go with the
/// reading, not with the rules: the opponent's revolver is heard when THEY USED REVOLVER comes
/// up, not when the player clicked the button before it. The phrases are ItemResolver's constants.
/// </remarks>
internal static class TableCues
{
    /// <summary>Every item by the name it is printed under.</summary>
    private static readonly Dictionary<string, ItemId> ByPrintedName = new();

    static TableCues()
    {
        foreach (ItemId item in Enum.GetValues<ItemId>())
            ByPrintedName[ItemCatalog.NameOf(item).ToUpperInvariant()] = item;
    }

    /// <summary>Plays whatever goes with a line, if anything does.</summary>
    /// <param name="audio">The sounds.</param>
    /// <param name="line">The line being shown.</param>
    /// <returns>True if the line had a sound.</returns>
    public static bool Play(AudioManager audio, string line)
    {
        if (audio is null || string.IsNullOrEmpty(line)) return false;

        if (TryReadUse(line, out ItemId used))
        {
            audio.PlayItem(used);
            return true;
        }

        Sfx? sound =
            line.StartsWith(ItemResolver.CostsYou, StringComparison.Ordinal) ? Sfx.Damage :
            line.StartsWith(ItemResolver.CostsThem, StringComparison.Ordinal) ? Sfx.Hit :
            line.StartsWith(ItemResolver.GivesYou, StringComparison.Ordinal)
                || line.StartsWith(ItemResolver.GivesThem, StringComparison.Ordinal) ? Sfx.Heal :
            line.StartsWith(ItemResolver.VeilTakes, StringComparison.Ordinal)
                || line.StartsWith(ItemResolver.MirrorSends, StringComparison.Ordinal) ? Sfx.Guard :
            null;

        if (sound is not Sfx found) return false;

        audio.Play(found);
        return true;
    }

    /// <summary>Reads the item out of a "SOMEBODY USED THING." line.</summary>
    /// <remarks>From the last " USED ", so a player who called themselves something with USED in it still works.</remarks>
    /// <param name="line">The line.</param>
    /// <param name="item">The item, if it was one.</param>
    private static bool TryReadUse(string line, out ItemId item)
    {
        item = default;

        int at = line.LastIndexOf(ItemResolver.UsedWord, StringComparison.Ordinal);
        if (at < 0 || !line.EndsWith('.')) return false;

        int start = at + ItemResolver.UsedWord.Length;
        return ByPrintedName.TryGetValue(line[start..^1], out item);
    }
}
