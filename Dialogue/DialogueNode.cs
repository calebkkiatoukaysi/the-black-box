using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One thing the player can say, as it appears on the wheel and as it lands.
/// </summary>
/// <remarks>
/// <see cref="Label"/> and <see cref="Line"/> are separate because the wheel has room for three
/// words and a sentence needs more than that. See <see cref="Tone"/> for why the pair has to be
/// kept honest.
/// </remarks>
/// <param name="Label">The two or three words on the wheel. Upper case; it is set in the UI font.</param>
/// <param name="Line">What the player actually says. May contain <c>{name}</c>.</param>
/// <param name="Tone">How it is said, and which corner it sits in.</param>
/// <param name="Next">
/// The node this leads to, or empty to end the discussion here. An id that no node answers to
/// also ends it -- see <see cref="DialogueScript.Validate"/>, which is what stops that reaching
/// a player.
/// </param>
/// <param name="Value">Extra warmth this particular line moves, on top of what its tone costs.</param>
/// <param name="Guard">Extra guard this particular line moves, on top of what its tone costs.</param>
/// <param name="Flag">A <see cref="SaveData.Flags"/> id to raise if this is taken, or empty.</param>
/// <param name="RequiresCandid">
/// Whether the option is only offered when the opponent has relaxed. A locked option still
/// occupies its corner -- shown, greyed, unselectable -- because a door the player can see is
/// worth more than one that was never drawn.
/// </param>
public readonly record struct DialogueOption(
    string Label,
    string Line,
    Tone Tone,
    string Next = "",
    int Value = 0,
    int Guard = 0,
    string Flag = "",
    bool RequiresCandid = false)
{
    /// <summary>Whether this option can be taken against <paramref name="disposition"/>.</summary>
    /// <param name="disposition">What the opponent currently thinks of the player.</param>
    public bool IsOpen(Disposition disposition) => !RequiresCandid || disposition.IsCandid;
}

/// <summary>
/// One beat of a discussion: what the opponent says, and the four ways to answer.
/// </summary>
/// <remarks>
/// <para>
/// Exactly four options, always. The wheel has four corners and they do not move, so a node
/// with three would leave a hole the player learns to read as "this is the one that matters".
/// <see cref="DialogueScript.Validate"/> refuses a script that breaks this rather than letting
/// the UI discover it mid-sentence.
/// </para>
/// <para>
/// The opponent's line comes in up to three tempers. Which one plays is
/// <see cref="Disposition.Band"/>, so the same beat of the same script reads differently on a
/// second run -- which is the whole reason the discussion period exists rather than a cutscene.
/// Only <see cref="Even"/> is required; an unwritten temper falls back to it, so a script can
/// be drafted flat and have its edges written later.
/// </para>
/// </remarks>
public sealed class DialogueNode
{
    /// <summary>What this node is called, and what an option's <c>Next</c> points at.</summary>
    public required string Id { get; init; }

    /// <summary>What the opponent says when they are neither warm nor hostile. Required.</summary>
    public required string Even { get; init; }

    /// <summary>What they say instead once they have turned on the player, or null to use <see cref="Even"/>.</summary>
    public string Hostile { get; init; }

    /// <summary>What they say instead once they have warmed, or null to use <see cref="Even"/>.</summary>
    public string Open { get; init; }

    /// <summary>The four replies, in wheel order.</summary>
    public required IReadOnlyList<DialogueOption> Options { get; init; }

    /// <summary>
    /// What they say to a player in this frame of mind.
    /// </summary>
    /// <param name="disposition">What the opponent currently thinks of the player.</param>
    /// <returns>The line to put on screen, still holding any <c>{name}</c> token.</returns>
    public string LineFor(Disposition disposition) => disposition.Band switch
    {
        Warmth.Hostile => string.IsNullOrEmpty(Hostile) ? Even : Hostile,
        Warmth.Open => string.IsNullOrEmpty(Open) ? Even : Open,
        _ => Even,
    };
}
