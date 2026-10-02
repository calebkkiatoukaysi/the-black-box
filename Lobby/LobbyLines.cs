namespace TheBlackBox.Lobby;

/// <summary>
/// Everything anybody says in the lobby, in one place so it can be rewritten in one place.
/// </summary>
/// <remarks>
/// Kept short and kept vague on purpose: the lobby is where people wait, not where the story is
/// told. Same voice as the table scripts, no contractions. Serenity is the one the box seats
/// across from the player, so hers are the lines that ask them to the table; the other conscript
/// is just passing the time.
/// </remarks>
public static class LobbyLines
{
    /// <summary>The id the other conscript goes by, since they are not an opponent and have no script.</summary>
    public const string ConscriptId = "conscript";

    /// <summary>What the other conscript is called on the dialogue box.</summary>
    public const string ConscriptName = "ANOTHER CONSCRIPT";

    /// <summary>What the box says when the door is tried before anyone has asked for the player.</summary>
    public static readonly string[] DoorLocked =
    {
        "NOT YET.",
        "SOMEONE IN THIS ROOM IS WAITING TO SIT ACROSS FROM YOU.",
    };

    /// <summary>The line on the black while the door shuts behind the player.</summary>
    public const string ThroughTheDoor = "THE DOOR CLOSES BEHIND YOU.";

    /// <summary>The pages someone says when they are talked to.</summary>
    /// <param name="id">Who: Serenity's id, or <see cref="ConscriptId"/>.</param>
    /// <param name="spokenBefore">Whether the player has already talked to them this chapter.</param>
    /// <returns>One page per press.</returns>
    public static string[] For(string id, bool spokenBefore) => (id == ConscriptId, spokenBefore) switch
    {
        (false, false) => new[]
        {
            "Oh -- sorry. I did not hear you come in.",
            "They put the list on the door again. It is the two of us, at the table tonight.",
            "I will not make it worse than it has to be. I do not think I could if I tried.",
            "Whenever you are ready. The door will open for you now.",
        },
        (false, true) => new[]
        {
            "I will be at the table. Take your time. It will not give you much.",
        },
        (true, false) => new[]
        {
            "Do not stand under the cameras for long. They look back.",
            "If you find anything on the floor, keep it. The box does not care where it came from.",
        },
        (true, true) => new[]
        {
            "Three nights I have been on the list. It has not called me once.",
        },
    };
}
