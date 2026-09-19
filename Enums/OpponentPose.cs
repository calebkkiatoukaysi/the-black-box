namespace TheBlackBox;

/// <summary>
/// Which frame of the opponent's sheet is showing. The first three match the Warmth bands,
/// the last two are things they are doing and override the temper while they do them.
/// </summary>
/// <remarks>There is no talking pose. A line landing does not move them; the idle is the talking.</remarks>
public enum OpponentPose
{
    /// <summary>Done being civil. On the character-sheet opponent that is a held smile, not a scowl.</summary>
    Hostile = 0,

    /// <summary>Giving nothing away either direction.</summary>
    Even = 1,

    /// <summary>They have decided you are a person.</summary>
    Open = 2,

    /// <summary>Leant in over the table with an arm in the box.</summary>
    Reaching = 3,

    /// <summary>Something just landed on them.</summary>
    Hurt = 4,
}
