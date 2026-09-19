using System;
using Microsoft.Xna.Framework;

namespace TheBlackBox;

/// <summary>
/// The discussion period: the opponent's line, the wheel, and the beat the player's own reply is held for.
/// </summary>
public partial class TableScreen
{
    // How long the player's own line is held: a base, a bit per character, and a floor and
    // ceiling. The clock keeps running through it, so a reply costs the time it takes to say.
    private const float SayingBase = 0.9f;
    private const float SayingPerCharacter = 0.032f;
    private const float SayingMin = 1.3f;
    private const float SayingMax = 3.4f;

    /// <summary>The discussion being played, or null unless one is.</summary>
    private DiscussionPeriod _discussion;

    /// <summary>What is on screen in place of a line once the discussion has ended.</summary>
    private string _discussionEnd;

    /// <summary>Seconds left of the player's own line being on screen, or 0 if it is not.</summary>
    private float _saidHold;

    /// <summary>Whether what is on screen is the player's own last line rather than the opponent's.</summary>
    private bool IsPlayerSpeaking =>
        _phase == RoundPhase.Discussion && _saidHold > 0f && _discussionEnd is null;

    /// <summary>Opens the discussion period for this round with whoever is across the table.</summary>
    /// <remarks>A first meeting opens on the script's disposition. Every one after opens on what the save carries.</remarks>
    private void StartDiscussion()
    {
        _discussionEnd = null;

        Disposition? carried = _run.HasMetOpponent ? _run.OpponentDisposition : null;

        _discussion = new DiscussionPeriod(Opponents.ById(_run.OpponentId).Script, _run.PlayerName, _run.Round, carried);

        _saidHold = 0f;
        _opponent.SetDisposition(_discussion.Disposition);
        _opponentLivesShown = _run.OpponentLives;

        _phase = RoundPhase.Discussion;
        ShowBeat();
    }

    /// <summary>Runs the discussion period and the wheel over it.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateDiscussion(GameTime gameTime)
    {
        if (_discussion is null) return;

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_saidHold > 0f)
        {
            _saidHold -= elapsed;
            if (_saidHold <= 0f) FinishSaying();
        }

        DiscussionState before = _discussion.State;
        _discussion.Update(elapsed);

        // The clock ran out between frames.
        if (before == DiscussionState.Running && _discussion.State != DiscussionState.Running)
            EndDiscussion();

        if (_wheel.IsOpen) _wheel.Update(gameTime);
    }

    /// <summary>Says one of the four replies.</summary>
    /// <param name="corner">Which corner of the wheel was clicked.</param>
    private void Answer(int corner)
    {
        // Nothing is taken while the last answer is still being said.
        if (_discussion is null || _saidHold > 0f) return;
        if (!_discussion.Choose(corner)) return;

        // Everything the choice changed, written back to the run in one place.
        _run.OpponentDisposition = _discussion.Disposition;
        foreach (string flag in _discussion.FlagsRaised) _run.SetFlag(flag);

        // The wheel comes down while the player's line is up, and the new temper lands on the
        // face now so they see it arrive while they are still speaking.
        _wheel.Hide();
        _opponent.SetDisposition(_discussion.Disposition);

        _saidHold = SayingSeconds(_discussion.Said);
    }

    /// <summary>How long a line of the player's own stays up before it is answered.</summary>
    /// <remarks>Off the length of the line. One fixed beat was too short for long replies and dead air after short ones.</remarks>
    /// <param name="line">The line being held on screen.</param>
    /// <returns>How long to hold it, in seconds.</returns>
    private static float SayingSeconds(string line) => Math.Clamp(
        SayingBase + (line?.Length ?? 0) * SayingPerCharacter, SayingMin, SayingMax);

    /// <summary>Ends the beat the player's line was held for, and gives the table back.</summary>
    /// <remarks>
    /// The one place a discussion can end on the player's own words. It used to close on the
    /// click, which threw the last line away before it was ever shown.
    /// </remarks>
    private void FinishSaying()
    {
        _saidHold = 0f;

        if (_discussion.State == DiscussionState.Running) ShowBeat();
        else EndDiscussion();
    }

    /// <summary>Puts the current beat on the wheel.</summary>
    private void ShowBeat() => _wheel.Show(_discussion.Current, _discussion.Disposition);

    /// <summary>Closes the discussion down and lets the box ask for what it is owed.</summary>
    private void EndDiscussion()
    {
        _wheel.Hide();
        _saidHold = 0f;

        _run.OpponentDisposition = _discussion.Disposition;
        foreach (string flag in _discussion.FlagsRaised) _run.SetFlag(flag);
        _run.RecordMeeting(_run.OpponentId);

        _opponent.SetDisposition(_discussion.Disposition);

        _discussionEnd = _discussion.State == DiscussionState.Silenced
            ? "YOU SAID NOTHING, AND THE BOX STOPPED WAITING."
            : "THE BOX HAS HEARD ENOUGH OF YOU BOTH.";

        _phase = RoundPhase.Offer;
        _feedButton.Reset();
    }
}
