using System;

namespace TheBlackBox;

/// <summary>
/// What one opponent currently thinks of the player, on two axes that do different jobs.
/// </summary>
/// <remarks>
/// Two numbers because "do they like you" and "will they tell you anything" are different
/// questions. Warmth picks which version of a line they say, Guard decides whether it has
/// anything in it. Immutable: a shift returns a new one, so the old value can be compared.
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
    /// <remarks>Bands are wide on purpose. An opponent who flips every time the player is slightly rude reads as unstable.</remarks>
    public Warmth Band => Value <= -Register ? Warmth.Hostile : Value >= Register ? Warmth.Open : Warmth.Even;

    /// <summary>How far warmth has to move before they speak differently.</summary>
    /// <remarks>Holding one tone for a whole discussion moves warmth a bit over 30, so this makes that worth exactly one band. Lengthen the scripts and this has to go up with them.</remarks>
    public const int Register = 32;

    /// <summary>The guard above which they refuse a question, and below which they volunteer things.</summary>
    public const int ShutGuard = 75;
    public const int CandidGuard = 25;

    /// <summary>Whether they are guarded enough to refuse a question outright.</summary>
    public bool IsShut => Guard >= ShutGuard;

    /// <summary>Whether they have relaxed enough to volunteer something unasked.</summary>
    public bool IsCandid => Guard <= CandidGuard;

    /// <summary>Moves both axes and clamps them.</summary>
    /// <param name="value">How far warmth moves, positive or negative.</param>
    /// <param name="guard">How far guard moves, positive or negative.</param>
    /// <returns>The disposition they hold after it.</returns>
    public Disposition Shift(int value, int guard) => new(
        Math.Clamp(Value + value, Floor, Ceiling),
        Math.Clamp(Guard + guard, 0, Ceiling));

    /// <summary>Applies what a tone costs, whichever line carried it.</summary>
    /// <remarks>
    /// The floor under every option; the option's own shift goes on top. Warmth is cheap to
    /// lose and slow to earn, and guard only really comes down under Level, so an opponent has
    /// to be talked open over a whole discussion.
    /// </remarks>
    /// <param name="tone">The tone of the line that was said.</param>
    /// <returns>The disposition they hold after hearing it.</returns>
    public Disposition Hear(Tone tone) => tone switch
    {
        Tone.Warm => Shift(6, -1),
        Tone.Level => Shift(2, -4),
        Tone.Probing => Shift(-1, 7),
        Tone.Cutting => Shift(-11, 5),

        // Saying nothing is not neutral. It is colder than Level and makes them warier.
        Tone.Silence => Shift(-4, 6),

        _ => this,
    };
}
