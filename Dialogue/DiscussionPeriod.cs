using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// One discussion, being played: where it has got to, what the opponent now thinks, and how
/// much of the box's patience is left.
/// </summary>
/// <remarks>
/// Nothing in here draws; the wheel asks it what to show. The disposition is handed in at the
/// start and read back out at the end, which is how an opponent remembers the last discussion.
/// </remarks>
public sealed class DiscussionPeriod
{
    private readonly DialogueScript _script;
    private readonly string _playerName;
    private readonly List<string> _transcript = new();
    private readonly List<string> _flags = new();

    /// <summary>Opens a discussion.</summary>
    /// <param name="script">The opponent's lines. Validated here rather than on first use.</param>
    /// <param name="playerName">What the player called themselves, swapped into every {name}.</param>
    /// <param name="round">Which round of the run this is, from 0. Picks where the script opens.</param>
    /// <param name="carriedOver">What the opponent already thought of the player, or null on a first meeting.</param>
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

    /// <summary>The <see cref="SaveData.Flags"/> ids the choices have raised.</summary>
    /// <remarks>Collected here, not written. This class never touches a save; the caller drains this at the end.</remarks>
    public IReadOnlyList<string> FlagsRaised => _flags;

    /// <summary>What the opponent is saying, in the temper they are in and with the player's name in it.</summary>
    public string Line => Current is null ? string.Empty : Fill(Current.LineFor(Disposition));

    /// <summary>The last thing the player said, with their name in it, or empty before they have answered anything.</summary>
    /// <remarks>Kept as a property rather than returned from Choose so the plate can read it on any frame, not just the one it was said on.</remarks>
    public string Said { get; private set; } = string.Empty;

    /// <summary>Moves the clock.</summary>
    /// <param name="seconds">How long since the last frame.</param>
    public void Update(float seconds)
    {
        if (State != DiscussionState.Running) return;

        Remaining -= seconds;
        if (Remaining > 0f) return;

        // Out of time. The opponent hears the silence, and the box moves things along.
        Remaining = 0f;
        Disposition = Disposition.Hear(Tone.Silence);
        _transcript.Add($"{_playerName} says nothing.");
        Current = null;
        State = DiscussionState.Silenced;
    }

    /// <summary>Whether the option in this corner can be taken right now.</summary>
    /// <param name="corner">Which corner of the wheel, from 0 to WheelSize - 1.</param>
    public bool IsOpen(int corner) =>
        State == DiscussionState.Running
        && Current is not null
        && corner >= 0 && corner < Current.Options.Count
        && Current.Options[corner].IsOpen(Disposition);

    /// <summary>Says the option in this corner.</summary>
    /// <remarks>The tone's cost is applied first and the option's own shift on top of it. See <see cref="Disposition.Hear"/>.</remarks>
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

    /// <summary>Ends the discussion early, without the clock having run out.</summary>
    /// <remarks>For the round loop, not the player. Counts as concluded, not silenced, since nobody was left waiting.</remarks>
    public void Close()
    {
        if (State != DiscussionState.Running) return;

        Current = null;
        State = DiscussionState.Concluded;
    }

    /// <summary>Puts the player's name into a written line.</summary>
    /// <param name="text">The line as written, maybe with {name} in it.</param>
    private string Fill(string text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace("{name}", _playerName);
}
