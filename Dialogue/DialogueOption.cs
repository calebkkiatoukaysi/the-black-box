namespace TheBlackBox;

/// <summary>
/// One thing the player can say, as it appears on the wheel and as it lands.
/// </summary>
/// <remarks>Label and Line are separate because the wheel only has room for three words.</remarks>
/// <param name="Label">The two or three words on the wheel. Upper case.</param>
/// <param name="Line">What the player actually says. May contain {name}.</param>
/// <param name="Tone">How it is said, and which corner it sits in.</param>
/// <param name="Next">The node this leads to, or empty to end the discussion here.</param>
/// <param name="Value">Extra warmth this line moves, on top of what its tone costs.</param>
/// <param name="Guard">Extra guard this line moves, on top of what its tone costs.</param>
/// <param name="Flag">A <see cref="SaveData.Flags"/> id to raise if this is taken, or empty.</param>
/// <param name="RequiresCandid">Only offered once the opponent has relaxed. Still drawn in its corner, greyed out, so the player can see there is something to unlock.</param>
public readonly record struct DialogueOption(
    string Label,
    string Line,
    Tone Tone,
    string Next = "",
    int Value = 0,
    int Guard = 0,
    string Flag = "",
    bool RequiresCandid = false)
{
    /// <summary>Whether this option can be taken against <paramref name="disposition"/>.</summary>
    /// <param name="disposition">What the opponent currently thinks of the player.</param>
    public bool IsOpen(Disposition disposition) => !RequiresCandid || disposition.IsCandid;
}
