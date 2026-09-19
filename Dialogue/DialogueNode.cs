using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One thing the player can say, as it appears on the wheel and as it lands.
/// </summary>
/// <remarks>Label and Line are separate because the wheel only has room for three words.</remarks>
/// <param name="Label">The two or three words on the wheel. Upper case.</param>
/// <param name="Line">What the player actually says. May contain {name}.</param>
/// <param name="Tone">How it is said, and which corner it sits in.</param>
/// <param name="Next">The node this leads to, or empty to end the discussion here.</param>
/// <param name="Value">Extra warmth this line moves, on top of what its tone costs.</param>
/// <param name="Guard">Extra guard this line moves, on top of what its tone costs.</param>
/// <param name="Flag">A <see cref="SaveData.Flags"/> id to raise if this is taken, or empty.</param>
/// <param name="RequiresCandid">Only offered once the opponent has relaxed. Still drawn in its corner, greyed out, so the player can see there is something to unlock.</param>
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
/// Always exactly four options, because the wheel has four fixed corners; Validate enforces
/// it. The line comes in up to three tempers and only Even is required, so a script can be
/// drafted flat and get its hostile and open versions later.
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

    /// <summary>What they say to a player in this frame of mind.</summary>
    /// <param name="disposition">What the opponent currently thinks of the player.</param>
    /// <returns>The line to put on screen, still holding any {name}.</returns>
    public string LineFor(Disposition disposition) => disposition.Band switch
    {
        Warmth.Hostile => string.IsNullOrEmpty(Hostile) ? Even : Hostile,
        Warmth.Open => string.IsNullOrEmpty(Open) ? Even : Open,
        _ => Even,
    };
}
