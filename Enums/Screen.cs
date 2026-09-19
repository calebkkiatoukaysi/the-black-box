namespace TheBlackBox;

/// <summary>
/// Which screen the player is on. One field instead of separate screen classes, because they
/// are all drawn over the same box and it should keep moving through every one of them.
/// </summary>
internal enum Screen
{
    /// <summary>The title, with START and EXIT under it.</summary>
    Title,

    /// <summary>The save form, over a veiled title screen.</summary>
    SlotSelect,

    /// <summary>Naming a run that has not been named, over a veiled title screen.</summary>
    NameEntry,

    /// <summary>A run: the table, the opponent across it, and the round being played.</summary>
    Run,
}
