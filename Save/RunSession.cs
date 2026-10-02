namespace TheBlackBox;

/// <summary>
/// The run being played and the slot it goes back into, handed from screen to screen.
/// </summary>
/// <remarks>
/// The customization screen, the lobby and the table all write the same run back to the same
/// slot, so they share this instead of each carrying a slot number around. A scratch run has no
/// slot at all and is never written: that is what the proof run plays on.
/// </remarks>
public class RunSession
{
    /// <summary>Which slot the run goes back into, or -1 for a scratch run.</summary>
    public int Slot { get; }

    /// <summary>The run.</summary>
    public SaveData Run { get; }

    /// <summary>Why the last save failed, or null if it did not.</summary>
    public string LastError { get; private set; }

    /// <summary>Whether this run is never written anywhere.</summary>
    public bool IsScratch => Slot < 0;

    /// <summary>Whether the run is somewhere in the middle of a table, rather than between tables.</summary>
    /// <remarks>
    /// A run left mid-round opens back on the table, mid-decision if that is where it was. One
    /// that has not fed the box yet opens in the lobby. Restart and Advance put all of these back
    /// to zero, so a finished table always comes back out to the lobby.
    /// </remarks>
    public bool IsAtTable =>
        Run.Round > 0 || Run.HandsFed > 0 || ItemCatalog.TryParse(Run.Dealt, out _) || RoundEngine.IsOver(Run);

    /// <summary>Pairs a run with its slot.</summary>
    /// <param name="slot">The slot, or -1 for scratch.</param>
    /// <param name="run">The run.</param>
    public RunSession(int slot, SaveData run)
    {
        Slot = slot;
        Run = run;
    }

    /// <summary>A run that is played and thrown away.</summary>
    /// <param name="run">The run.</param>
    public static RunSession Scratch(SaveData run) => new(-1, run);

    /// <summary>Writes the run back to its slot. A scratch run always "saves".</summary>
    /// <returns>True if the slot now holds the run.</returns>
    public bool Save()
    {
        if (IsScratch) return true;

        if (SaveSystem.Write(Slot, Run))
        {
            LastError = null;
            return true;
        }

        LastError = SaveSystem.LastError;
        return false;
    }
}
