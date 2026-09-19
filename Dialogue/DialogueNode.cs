using System.Collections.Generic;

namespace TheBlackBox;

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
