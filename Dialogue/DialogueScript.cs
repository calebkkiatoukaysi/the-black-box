using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One opponent's discussion: every beat of it, how long the box allows, and where it opens.
/// </summary>
/// <remarks>
/// <para>
/// A script is data and holds no state. The same script is played by every run, so everything
/// that changes while it is being played -- which node is current, what the opponent now thinks
/// of the player, how much time is left -- lives in <see cref="DiscussionPeriod"/> instead.
/// One script, many discussions.
/// </para>
/// <para>
/// <see cref="Seconds"/> is on the script rather than being a constant because the pressure is
/// meant to vary: an early meeting can be allowed to breathe, and a discussion late in a run
/// can be cut short enough that reading all four options is itself a decision. It is the
/// cheapest dial in the game for making a scene feel different without writing a word.
/// </para>
/// </remarks>
public sealed class DialogueScript
{
    /// <summary>The opponent this belongs to. Matches <see cref="SaveData.OpponentId"/>.</summary>
    public required string OpponentId { get; init; }

    /// <summary>What the opponent is called on screen.</summary>
    public required string OpponentName { get; init; }

    /// <summary>
    /// Which node each round's discussion opens on: the first entry for the first round, the
    /// second for the second, and the last for every round after it runs out.
    /// </summary>
    /// <remarks>
    /// One conversation across a whole run rather than the same one every round. What the
    /// player said last round is carried in the <see cref="Disposition"/> and not here, so
    /// each entry is a fresh beat that the temper colours -- the writing does not have to
    /// branch on the past, only sound like it remembers it.
    /// </remarks>
    public required IReadOnlyList<string> Openings { get; init; }

    /// <summary>
    /// How long the box allows the whole discussion, in seconds.
    /// </summary>
    /// <remarks>
    /// A budget for the period rather than a timer per reply. The clock does not stop while the
    /// player reads, so working out what all four options mean costs the time it takes, and an
    /// opponent can be given so little of it that the tone of an answer is all there is room to
    /// judge. Running it out is not a failure state -- see <see cref="Tone.Silence"/>.
    /// </remarks>
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

    /// <summary>
    /// Finds one beat.
    /// </summary>
    /// <param name="id">The node to look up.</param>
    /// <returns>The node, or null if the id names nothing, which ends the discussion.</returns>
    public DialogueNode Find(string id)
    {
        if (_byId is null) Validate();
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out DialogueNode node) ? node : null;
    }

    /// <summary>
    /// Checks the script is playable, and indexes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Throws, like <see cref="ItemCatalog"/>'s table check and for the same reason: every
    /// fault it looks for is a typo made while writing dialogue, and all of them are invisible
    /// until the exact beat that contains them is reached. A script with a misspelled
    /// <see cref="DialogueOption.Next"/> would otherwise play perfectly for four exchanges and
    /// then end the conversation early, in front of a player, with nothing in the log to say
    /// why.
    /// </para>
    /// <para>
    /// An option pointing at nothing is deliberately legal -- that is how a discussion ends.
    /// An option pointing at something that does not exist is not.
    /// </para>
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

                // The corner a reply sits in is the promise that its tone is what it looks
                // like, and the wheel reads position straight off this order. A node that
                // put cutting where warm belongs would move the plate under a player who had
                // already learned where to click, which is the exact failure the fixed
                // corners exist to prevent.
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
                // Empty means "this is where the discussion ends" and is how most scripts
                // finish. A name nobody answers to is a typo wearing the same clothes.
                if (!string.IsNullOrEmpty(option.Next) && !byId.ContainsKey(option.Next))
                    throw new InvalidOperationException($"{OpponentId}/{node.Id}/{option.Label}: leads to '{option.Next}', which is not a node.");
            }
        }

        _byId = byId;
    }

    /// <summary>How many options a node offers, which is how many corners the wheel has.</summary>
    public const int WheelSize = 4;
}
