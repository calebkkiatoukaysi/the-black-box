namespace TheBlackBox;

/// <summary>How a discussion period ended, or that it has not.</summary>
public enum DiscussionState
{
    /// <summary>Still being played. The clock is running.</summary>
    Running,

    /// <summary>Talked to its end. The player chose the last word.</summary>
    Concluded,

    /// <summary>The clock ran out. See Tone.Silence.</summary>
    Silenced,
}
