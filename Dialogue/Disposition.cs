using System;

namespace TheBlackBox;

/// <summary>How the opponent sounds, which is a different question from what they will tell you.</summary>
public enum Warmth
{
    /// <summary>They are done being civil.</summary>
    Hostile,

    /// <summary>Nothing given away either direction.</summary>
    Even,

    /// <summary>They have decided you are a person.</summary>
    Open,
}

/// <summary>
/// What one opponent currently thinks of the player, on two axes that do different jobs.
/// </summary>
/// <remarks>
/// <para>
/// Two numbers rather than one, because "do they like you" and "will they tell you anything"
/// are not the same question and a single affection meter cannot express the opponent who is
/// perfectly friendly and says nothing. <see cref="Warmth"/> picks which version of a line
/// they say. <see cref="Guard"/> decides whether the line has anything in it.
/// </para>
/// <para>
/// That division is the whole point of the discussion period. Items like
/// <see cref="ItemId.Lens"/> buy information with a hand; talking buys it with time and with
/// the temperature of the conversation, and <see cref="Tone.Probing"/> is the trade made
/// explicit -- ask a direct question and you learn something now at the cost of them being
/// harder to read for the rest of the night.
/// </para>
/// <para>
/// A struct with no setters: shifting a disposition returns a new one. A conversation is a
/// list of things that happened in order, and being able to hand the old value to the next
/// line and compare is worth more than saving an allocation that is not happening anyway.
/// </para>
/// </remarks>
/// <param name="Value">How warmly they speak, from -100 to 100.</param>
/// <param name="Guard">How little they give away, from 0 (candid) to 100 (shut).</param>
public readonly record struct Disposition(int Value, int Guard)
{
    /// <summary>Where an opponent starts before a word has been said, unless their script says otherwise.</summary>
    public static readonly Disposition Neutral = new(0, 50);

    /// <summary>The lowest and highest either axis can reach.</summary>
    public const int Floor = -100;

    /// <summary>The top of <see cref="Value"/>, and of <see cref="Guard"/>.</summary>
    public const int Ceiling = 100;

    /// <summary>Which band <see cref="Value"/> falls in, which is what picks a line.</summary>
    /// <remarks>
    /// Bands rather than the raw number, because no line should be written for a warmth of 31.
    /// The thresholds are wide on purpose: an opponent who flips register every time the player
    /// is slightly rude reads as unstable rather than as a person being worn down.
    /// </remarks>
    /// <seealso cref="Register"/>
    public Warmth Band => Value <= -Register ? Warmth.Hostile : Value >= Register ? Warmth.Open : Warmth.Even;

    /// <summary>
    /// How far warmth has to move before they speak differently.
    /// </summary>
    /// <remarks>
    /// Tuned against the length of a discussion rather than picked: a player who holds one tone
    /// for a whole period moves warmth a little over thirty points, so this is what makes an
    /// evening of unbroken cruelty -- or unbroken kindness -- worth exactly one band and no
    /// more. Lengthen the scripts and this has to come up with them.
    /// </remarks>
    public const int Register = 32;

    /// <summary>Whether they are guarded enough to refuse a question outright.</summary>
    public bool IsShut => Guard >= 75;

    /// <summary>Whether they have relaxed enough to volunteer something unasked.</summary>
    public bool IsCandid => Guard <= 25;

    /// <summary>
    /// Moves both axes and clamps them.
    /// </summary>
    /// <param name="value">How far warmth moves, positive or negative.</param>
    /// <param name="guard">How far guard moves, positive or negative.</param>
    /// <returns>The disposition they hold after it.</returns>
    public Disposition Shift(int value, int guard) => new(
        Math.Clamp(Value + value, Floor, Ceiling),
        Math.Clamp(Guard + guard, 0, Ceiling));

    /// <summary>
    /// Applies what a tone costs regardless of which line carried it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the floor under every option, not the whole effect. An individual
    /// <see cref="DialogueOption"/> adds its own shift on top, so a particularly well judged
    /// cruelty can be written to cost less than cruelty usually does -- but nothing can be
    /// cutting for free, and a writer cannot forget to charge for it.
    /// </para>
    /// <para>
    /// Warmth is cheap to lose and slow to earn: <see cref="Tone.Cutting"/> takes more than
    /// <see cref="Tone.Warm"/> gives. Guard only comes down under <see cref="Tone.Level"/>,
    /// and only a point at a time, so an opponent has to be talked open across a whole
    /// discussion rather than unlocked with one correct answer.
    /// </para>
    /// </remarks>
    /// <param name="tone">The tone of the line that was said.</param>
    /// <returns>The disposition they hold after hearing it.</returns>
    public Disposition Hear(Tone tone) => tone switch
    {
        Tone.Warm => Shift(6, -1),
        Tone.Level => Shift(2, -4),
        Tone.Probing => Shift(-1, 7),
        Tone.Cutting => Shift(-11, 5),

        // Saying nothing is colder than being plain and warier than being warm. It is not
        // neutral, and an opponent left waiting is the one place the clock has teeth.
        Tone.Silence => Shift(-4, 6),

        _ => this,
    };
}
