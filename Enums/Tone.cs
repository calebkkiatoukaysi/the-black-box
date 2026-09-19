namespace TheBlackBox;

/// <summary>
/// How a line is said, not what it says.
/// </summary>
/// <remarks>
/// Each tone always sits in the same corner of the wheel, so the player can tell what they are
/// agreeing to before reading the words (Fallout 4's wheel got complaints for not doing this).
/// Silence is never offered, it is what you get when the clock runs out, but the opponent still
/// reacts to it so it needs to be here.
/// </remarks>
public enum Tone
{
    /// <summary>Placating, familiar, kind. Buys warmth and gives up ground.</summary>
    Warm,

    /// <summary>Plain and unhurried. Slowly convinces them you are not working an angle.</summary>
    Level,

    /// <summary>A question, and they can hear that it is one. Costs you their guard.</summary>
    Probing,

    /// <summary>Contempt. Fast, satisfying, and expensive.</summary>
    Cutting,

    /// <summary>The clock ran out. Never put on an option.</summary>
    Silence,
}
