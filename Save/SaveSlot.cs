using System;
using System.Globalization;

namespace TheBlackBox;

/// <summary>
/// One slot as the save form sees it: which slot, what is in it, and the line to print on it.
/// </summary>
/// <remarks>The form never touches the disk. It asks SaveSystem for three of these and draws them.</remarks>
public class SaveSlot
{
    /// <summary>Which slot this is, from 0 to <see cref="SaveSystem.SlotCount"/> - 1.</summary>
    public int Index { get; init; }

    /// <summary>What the slot turned out to be.</summary>
    public SaveSlotState State { get; init; }

    /// <summary>The run in the slot, or null unless <see cref="State"/> is Occupied.</summary>
    public SaveData Data { get; init; }

    /// <summary>Why the slot could not be read, or null if nothing went wrong.</summary>
    public string Error { get; init; }

    /// <summary>Whether there is a run in here to load or erase.</summary>
    public bool HasRun => State == SaveSlotState.Occupied;

    /// <summary>The slot's name, in the numbering the game uses for everything else.</summary>
    public string Name => "SLOT " + Numeral(Index + 1);

    /// <summary>The line printed under the slot's name: what is in it, or why there is nothing to load.</summary>
    public string Summary => State switch
    {
        SaveSlotState.Occupied =>
            string.Format(CultureInfo.InvariantCulture, "CHAPTER {0}  ·  {1}  ·  {2}",
                Data.Chapter, Clock(Data.Playtime), Data.SavedUtc.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture)),

        // Said plainly. A corrupt slot is the one place to drop the atmosphere and just tell the player.
        SaveSlotState.Unreadable => "UNREADABLE  ·  ERASE TO REUSE",

        _ => "EMPTY  ·  BEGIN",
    };

    /// <summary>Formats a playtime as hours and minutes, which is all a slot has room for.</summary>
    /// <param name="time">The time to format.</param>
    private static string Clock(TimeSpan time) =>
        string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", (int)time.TotalHours, time.Minutes);

    /// <summary>Roman numerals for 1 to 3, which is as far as the slots go.</summary>
    /// <param name="value">The number to spell.</param>
    private static string Numeral(int value) => value switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => value.ToString(CultureInfo.InvariantCulture),
    };
}
