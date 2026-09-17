namespace TheBlackBox;

/// <summary>
/// How a line is said, rather than what it says.
/// </summary>
/// <remarks>
/// <para>
/// Four of these are offered at once, in fixed screen positions, so that the tone of a reply is
/// readable before the words under it are. Fallout 4's wheel is the model and also the warning:
/// it showed a two-word paraphrase and then said something else, and the complaint was always
/// that the player could not tell what they were agreeing to. Keeping the tone constant per
/// corner is what makes that honest -- the paraphrase may surprise you, the temperature never
/// should.
/// </para>
/// <para>
/// <see cref="Silence"/> is not offered. It is what the player gets when the clock runs out,
/// which is why it is in this enum at all: saying nothing is a way of speaking to somebody, the
/// opponent reads it, and <see cref="Disposition"/> has to be told about it like any other
/// choice.
/// </para>
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

    /// <summary>The clock ran out. Never authored on an option; only ever arrived at.</summary>
    Silence,
}
