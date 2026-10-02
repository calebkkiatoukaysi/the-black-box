namespace TheBlackBox.Lobby;

/// <summary>
/// Everything anybody says in the lobby, in one place so it can be rewritten in one place.
/// </summary>
/// <remarks>
/// Kept short and kept vague on purpose: the lobby is where people wait, not where the story is
/// told. Same voice as the table scripts, no contractions. Whoever the chapter seats at the
/// table is the one who asks the player to it; everyone else is just passing the time.
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
    /// <param name="id">Who: an opponent's id, or <see cref="ConscriptId"/>.</param>
    /// <param name="challenger">Whether they are the one the box has seated across from the player this chapter.</param>
    /// <param name="spokenBefore">Whether the player has already talked to them this chapter.</param>
    /// <returns>One page per press.</returns>
    public static string[] For(string id, bool challenger, bool spokenBefore) => (id, challenger, spokenBefore) switch
    {
        ("serenity", true, false) => new[]
        {
            "Oh -- sorry. I did not hear you come in.",
            "They put the list on the door this morning. It is the two of us, at the first table.",
            "I will not make it worse than it has to be. I do not think I could if I tried.",
            "Whenever you are ready. The door will open for you now.",
        },
        ("serenity", true, true) => new[]
        {
            "I will be at the table. Take your time. It will not give you much.",
        },
        ("serenity", false, false) => new[]
        {
            "You got up from the table. Good. I mean that.",
            "Get some sleep, if they let you. I never can in here.",
        },
        ("serenity", false, true) => new[]
        {
            "The light over the door only comes on for one of us at a time.",
        },

        (_, true, false) when id != ConscriptId => new[]
        {
            "There you are.",
            "She went easy on you. I will not. That is not a threat. It is only the rules.",
            "The door is open. I will be across from you.",
        },
        (_, true, true) when id != ConscriptId => new[]
        {
            "Do not keep me waiting. I keep count.",
        },
        (_, false, false) when id != ConscriptId => new[]
        {
            "First night. It shows. You keep looking at that door.",
            "Not tonight. Tonight you belong to her. I can wait.",
        },
        (_, false, true) when id != ConscriptId => new[]
        {
            "Go on. The box hates waiting more than I do.",
        },

        (_, _, false) => new[]
        {
            "Do not stand under the cameras for long. They look back.",
            "If you find anything on the floor, keep it. The box does not care where it came from.",
        },
        _ => new[]
        {
            "Three nights I have been on the list. It has not called me once.",
        },
    };
}
