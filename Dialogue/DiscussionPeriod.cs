using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>How a discussion period ended, or that it has not.</summary>
public enum DiscussionState
{
    /// <summary>Still being played. The clock is running.</summary>
    Running,

    /// <summary>Talked to its end. The player chose the last word.</summary>
    Concluded,

    /// <summary>The box allowed so much time and no more. See <see cref="Tone.Silence"/>.</summary>
    Silenced,
}

/// <summary>
/// One discussion, being played: where it has got to, what the opponent now thinks, and how
/// much of the box's patience is left.
/// </summary>
/// <remarks>
/// <para>
/// The period is the half of a round that is not the box. Both sides talk, and then both feed
/// it a hand -- so everything that happens here is meant to change what the other half is
/// worth. Learning that an opponent is frightened is the same kind of advantage as holding a
/// <see cref="ItemId.Lens"/>, bought with time instead of a hand.
/// </para>
/// <para>
/// Nothing in here draws. It is told how much time has passed and it answers questions about
/// what should be on screen, which keeps the whole system testable without a window open, and
/// means the wheel can be rebuilt without touching a line of the dialogue logic.
/// </para>
/// <para>
/// The disposition it finishes with is the value that outlives it. It belongs to the opponent
/// and not to this period, so it is handed in at the start and read back out at the end --
/// that is what lets an opponent remember, three discussions later, that the player was cruel
/// on the first night.
/// </para>
/// </remarks>
public sealed class DiscussionPeriod
{
    private readonly DialogueScript _script;
    private readonly string _playerName;
    private readonly List<string> _transcript = new();
    private readonly List<string> _flags = new();

    /// <summary>
    /// Opens a discussion.
    /// </summary>
    /// <param name="script">The opponent's lines. Validated here rather than on first use.</param>
    /// <param name="playerName">What the player called themselves, substituted into every <c>{name}</c>.</param>
    /// <param name="round">Which round of the run this is, from 0. Picks where the script opens.</param>
    /// <param name="carriedOver">
    /// What the opponent already thought of the player, from an earlier discussion, or null on
    /// a first meeting to use the script's own opening.
    /// </param>
    public DiscussionPeriod(DialogueScript script, string playerName, int round = 0, Disposition? carriedOver = null)
    {
        ArgumentNullException.ThrowIfNull(script);

        script.Validate();

        _script = script;
        _playerName = string.IsNullOrWhiteSpace(playerName) ? "you" : playerName.Trim();

        Disposition = carriedOver ?? script.Opens;
        Remaining = script.Seconds;
        Current = script.Find(script.OpeningFor(round));
    }

    /// <summary>The beat on screen, or null once the discussion is over.</summary>
    public DialogueNode Current { get; private set; }

    /// <summary>What the opponent thinks of the player right now.</summary>
    public Disposition Disposition { get; private set; }

    /// <summary>Seconds of the box's patience left.</summary>
    public float Remaining { get; private set; }

    /// <summary>Whether the discussion is still being played, and if not, how it ended.</summary>
    public DiscussionState State { get; private set; } = DiscussionState.Running;

    /// <summary>How many times the player has answered.</summary>
    public int Exchanges { get; private set; }

    /// <summary>What the opponent is called on screen.</summary>
    public string OpponentName => _script.OpponentName;

    /// <summary>How much of the allowed time is left, from 1 to 0, for drawing a bar.</summary>
    public float Patience => Math.Clamp(Remaining / _script.Seconds, 0f, 1f);

    /// <summary>Everything said so far, in order, for a scrollback or a log.</summary>
    public IReadOnlyList<string> Transcript => _transcript;

    /// <summary>
    /// The <see cref="SaveData.Flags"/> ids the choices have raised.
    /// </summary>
    /// <remarks>
    /// Collected rather than written. This class never touches a <see cref="SaveData"/> -- the
    /// caller drains this when the period ends, which keeps a discussion playable in a test
    /// with no save anywhere near it.
    /// </remarks>
    public IReadOnlyList<string> FlagsRaised => _flags;

    /// <summary>What the opponent is saying, in the temper they are in and with the player's name in it.</summary>
    public string Line => Current is null ? string.Empty : Fill(Current.LineFor(Disposition));

    /// <summary>
    /// The last thing the player said, with their name in it, or empty before they have
    /// answered anything.
    /// </summary>
    /// <remarks>
    /// The other half of the conversation. <see cref="Line"/> is the opponent's side and is
    /// the only side a wheel usually shows, which leaves a player choosing a three-word label
    /// and never finding out what came out of their own mouth -- the exact complaint
    /// <see cref="Tone"/> is written against, arrived at from the other direction. Held here
    /// rather than handed back from <see cref="Choose"/> so that whatever is drawing can ask
    /// for it on any frame rather than having to catch it on the one it was said.
    /// </remarks>
    public string Said { get; private set; } = string.Empty;

    /// <summary>
    /// Moves the clock.
    /// </summary>
    /// <param name="seconds">How long since the last frame.</param>
    public void Update(float seconds)
    {
        if (State != DiscussionState.Running) return;

        Remaining -= seconds;
        if (Remaining > 0f) return;

        // Out of time. The opponent hears the pause, and the box moves things along.
        Remaining = 0f;
        Disposition = Disposition.Hear(Tone.Silence);
        _transcript.Add($"{_playerName} says nothing.");
        Current = null;
        State = DiscussionState.Silenced;
    }

    /// <summary>
    /// Whether the option in <paramref name="corner"/> can be taken right now.
    /// </summary>
    /// <param name="corner">Which corner of the wheel, from 0 to <see cref="DialogueScript.WheelSize"/> - 1.</param>
    public bool IsOpen(int corner) =>
        State == DiscussionState.Running
        && Current is not null
        && corner >= 0 && corner < Current.Options.Count
        && Current.Options[corner].IsOpen(Disposition);

    /// <summary>
    /// Says the option in <paramref name="corner"/>.
    /// </summary>
    /// <remarks>
    /// The opponent hears the tone first and the line second, so a written shift is applied on
    /// top of what the tone costs rather than instead of it. See <see cref="Disposition.Hear"/>.
    /// </remarks>
    /// <param name="corner">Which corner of the wheel was picked.</param>
    /// <returns>True if the line was said. False means it was locked, out of range, or the discussion is over.</returns>
    public bool Choose(int corner)
    {
        if (!IsOpen(corner)) return false;

        DialogueOption option = Current.Options[corner];

        Said = Fill(option.Line);

        _transcript.Add(Fill(Current.LineFor(Disposition)));
        _transcript.Add($"{_playerName}: {Said}");

        Disposition = Disposition.Hear(option.Tone).Shift(option.Value, option.Guard);
        Exchanges++;

        if (!string.IsNullOrEmpty(option.Flag) && !_flags.Contains(option.Flag))
            _flags.Add(option.Flag);

        Current = _script.Find(option.Next);

        if (Current is null) State = DiscussionState.Concluded;

        return true;
    }

    /// <summary>
    /// Ends the discussion early, without the clock having run out.
    /// </summary>
    /// <remarks>
    /// For the round loop, not the player -- something else in the round may need the table
    /// back. It counts as concluded rather than silenced, because the opponent was not left
    /// waiting on an answer.
    /// </remarks>
    public void Close()
    {
        if (State != DiscussionState.Running) return;

        Current = null;
        State = DiscussionState.Concluded;
    }

    /// <summary>Puts the player's name into a written line.</summary>
    /// <param name="text">The line as written, possibly holding <c>{name}</c>.</param>
    private string Fill(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("{name}", _playerName);
}
