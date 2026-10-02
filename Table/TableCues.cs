using System;
using System.Collections.Generic;
using TheBlackBox.Audio;

namespace TheBlackBox;

/// <summary>
/// Plays the sound that goes with a line of the round when it shows up.
/// </summary>
/// <remarks>
/// The round gets worked out all at once and then shown one line at a time, so the sounds go with
/// the lines. That way the opponent's revolver goes off when THEY USED REVOLVER shows up, not when
/// you clicked the button before it. The phrases it looks for are constants in ItemResolver.
/// </remarks>
internal static class TableCues
{
    /// <summary>Every item by the name it shows up as.</summary>
    private static readonly Dictionary<string, ItemId> ByPrintedName = new();

    static TableCues()
    {
        foreach (ItemId item in Enum.GetValues<ItemId>())
            ByPrintedName[ItemCatalog.NameOf(item).ToUpperInvariant()] = item;
    }

    /// <summary>Plays the sound for a line, if it has one.</summary>
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
    /// <remarks>Uses the last " USED " in case the player put USED in their name.</remarks>
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
