namespace TheBlackBox;

/// <summary>
/// Which frame of the opponent's sheet is showing. The first three match the Warmth bands,
/// the last three are things they are doing and override the temper while they do them.
/// </summary>
public enum OpponentPose
{
    /// <summary>Done being civil. On the first opponent that is a held smile, not a scowl.</summary>
    Hostile = 0,

    /// <summary>Giving nothing away either direction.</summary>
    Even = 1,

    /// <summary>They have decided you are a person.</summary>
    Open = 2,

    /// <summary>Mid-word. Held for a beat when a line lands, not the whole time it is on screen.</summary>
    Talking = 3,

    /// <summary>Leant in over the table with an arm in the box.</summary>
    Reaching = 4,

    /// <summary>Something just landed on them.</summary>
    Hurt = 5,
}
