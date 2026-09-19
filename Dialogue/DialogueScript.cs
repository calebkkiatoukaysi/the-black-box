using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One opponent's discussion: every beat of it, how long the box allows, and where it opens.
/// </summary>
/// <remarks>
/// Data only, no state. Everything that changes while it is played lives in
/// <see cref="DiscussionPeriod"/>. Seconds is per script so a later opponent can be given less time.
/// </remarks>
public sealed class DialogueScript
{
    /// <summary>The opponent this belongs to. Matches <see cref="SaveData.OpponentId"/>.</summary>
    public required string OpponentId { get; init; }

    /// <summary>What the opponent is called on screen.</summary>
    public required string OpponentName { get; init; }

    /// <summary>Which node each round opens on, in order. The last one repeats once the list runs out.</summary>
    /// <remarks>The past is carried by the Disposition, not here, so each beat only has to sound like it remembers.</remarks>
    public required IReadOnlyList<string> Openings { get; init; }

    /// <summary>How long the box allows the whole discussion, in seconds.</summary>
    /// <remarks>One budget for the period, not a timer per reply. The clock keeps going while the player reads. Running out is not a fail, see <see cref="Tone.Silence"/>.</remarks>
    public required float Seconds { get; init; }

    /// <summary>What the opponent thinks of the player before a word is said.</summary>
    public Disposition Opens { get; init; } = Disposition.Neutral;

    /// <summary>Every beat, in no particular order. <see cref="Openings"/> picks where each round starts.</summary>
    public required IReadOnlyList<DialogueNode> Nodes { get; init; }

    /// <summary>Nodes by <see cref="DialogueNode.Id"/>, built once by <see cref="Validate"/>.</summary>
    private Dictionary<string, DialogueNode> _byId;

    /// <summary>Where the discussion for <paramref name="round"/> opens.</summary>
    /// <param name="round">Which round of the run, from 0.</param>
    public string OpeningFor(int round) => Openings[Math.Clamp(round, 0, Openings.Count - 1)];

    /// <summary>Finds one beat.</summary>
    /// <param name="id">The node to look up.</param>
    /// <returns>The node, or null if the id names nothing, which ends the discussion.</returns>
    public DialogueNode Find(string id)
    {
        if (_byId is null) Validate();
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out DialogueNode node) ? node : null;
    }

    /// <summary>Checks the script is playable, and indexes it.</summary>
    /// <remarks>
    /// Throws on purpose. Every fault here is a typo in the dialogue that would otherwise only
    /// show up mid-conversation in front of a player. An empty Next is fine (that ends the
    /// discussion); a Next that names a node that does not exist is not.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the script could not be played as written.</exception>
    public void Validate()
    {
        var byId = new Dictionary<string, DialogueNode>();

        foreach (DialogueNode node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
                throw new InvalidOperationException($"{OpponentId}: a node has no id.");

            if (!byId.TryAdd(node.Id, node))
                throw new InvalidOperationException($"{OpponentId}: two nodes are called '{node.Id}'.");

            if (string.IsNullOrWhiteSpace(node.Even))
                throw new InvalidOperationException($"{OpponentId}/{node.Id}: has no even-tempered line, which is the one every other temper falls back to.");

            if (node.Options.Count != WheelSize)
                throw new InvalidOperationException($"{OpponentId}/{node.Id}: has {node.Options.Count} options and the wheel has {WheelSize} corners.");

            for (int i = 0; i < node.Options.Count; i++)
            {
                DialogueOption option = node.Options[i];

                if (string.IsNullOrWhiteSpace(option.Label))
                    throw new InvalidOperationException($"{OpponentId}/{node.Id}: an option has no label, and would be an empty corner.");

                if (option.Tone == Tone.Silence)
                    throw new InvalidOperationException($"{OpponentId}/{node.Id}/{option.Label}: silence is what the clock does, not something to offer.");

                // The wheel reads position straight off this order, so each corner always has the same tone.
                if ((int)option.Tone != i)
                    throw new InvalidOperationException($"{OpponentId}/{node.Id}/{option.Label}: is {option.Tone} in corner {i}, and corner {i} is {(Tone)i}. Options run in tone order.");
            }
        }

        if (Openings is null || Openings.Count == 0)
            throw new InvalidOperationException($"{OpponentId}: has nowhere to open. Openings needs at least one node.");

        foreach (string opening in Openings)
        {
            if (!byId.ContainsKey(opening))
                throw new InvalidOperationException($"{OpponentId}: opens a round on '{opening}', which is not a node.");
        }

        if (Seconds <= 0f)
            throw new InvalidOperationException($"{OpponentId}: is allowed {Seconds} seconds, so it would time out before it was read.");

        foreach (DialogueNode node in Nodes)
        {
            foreach (DialogueOption option in node.Options)
            {
                // Empty means the discussion ends here. A name nobody answers to is a typo.
                if (!string.IsNullOrEmpty(option.Next) && !byId.ContainsKey(option.Next))
                    throw new InvalidOperationException($"{OpponentId}/{node.Id}/{option.Label}: leads to '{option.Next}', which is not a node.");
            }
        }

        _byId = byId;
    }

    /// <summary>How many options a node offers, which is how many corners the wheel has.</summary>
    public const int WheelSize = 4;
}
