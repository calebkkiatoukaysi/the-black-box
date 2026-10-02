namespace TheBlackBox.Lobby;

/// <summary>
/// Everything anyone says in the lobby, all in one place so it's easy to rewrite.
/// </summary>
/// <remarks>
/// Kept short and vague on purpose since the lobby isn't where the story gets told (lore is still
/// being written!). Same voice as the table scripts, no contractions. Serenity's lines are the
/// ones that challenge you, the other conscript is just passing the time.
/// </remarks>
public static class LobbyLines
{
    /// <summary>The id for the other conscript, since they're not an opponent and don't have a script.</summary>
    public const string ConscriptId = "conscript";

    /// <summary>What the other conscript is called on the dialogue box.</summary>
    public const string ConscriptName = "ANOTHER CONSCRIPT";

    /// <summary>What the box says if you try the door before you've been challenged.</summary>
    public static readonly string[] DoorLocked =
    {
        "NOT YET.",
        "SOMEONE IN THIS ROOM IS WAITING TO SIT ACROSS FROM YOU.",
    };

    /// <summary>The line on the black screen while the door shuts behind you.</summary>
    public const string ThroughTheDoor = "THE DOOR CLOSES BEHIND YOU.";

    /// <summary>The pages someone says when you talk to them.</summary>
    /// <param name="id">Serenity's id, or ConscriptId.</param>
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
