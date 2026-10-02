namespace TheBlackBox;

/// <summary>
/// The run being played and the slot it saves to. Gets passed from screen to screen.
/// </summary>
/// <remarks>
/// Customization, the lobby and the table all save the same run to the same slot, so they share
/// this instead of each keeping a slot number. A scratch run has no slot and never saves (the
/// proof run uses one).
/// </remarks>
public class RunSession
{
    /// <summary>Which slot the run saves to, or -1 for a scratch run.</summary>
    public int Slot { get; }

    /// <summary>The run.</summary>
    public SaveData Run { get; }

    /// <summary>Why the last save failed, or null if it didn't.</summary>
    public string LastError { get; private set; }

    /// <summary>Whether this run never gets saved.</summary>
    public bool IsScratch => Slot < 0;

    /// <summary>Whether the run is in the middle of a table rather than between tables.</summary>
    /// <remarks>
    /// If you quit mid round, loading it puts you back at the table. If you haven't fed the box yet
    /// it opens in the lobby. Restart and Advance reset all of these, so a finished table always
    /// goes back to the lobby.
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

    /// <summary>A run that never gets saved.</summary>
    /// <param name="run">The run.</param>
    public static RunSession Scratch(SaveData run) => new(-1, run);

    /// <summary>Saves the run to its slot. A scratch run always "saves" without writing anything.</summary>
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
