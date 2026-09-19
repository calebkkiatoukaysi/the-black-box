using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheBlackBox.Checks;

namespace TheBlackBox;

/// <summary>
/// The player's turn: the three plates, the pockets, the check an item asks for, and reading out what happened.
/// </summary>
public partial class TableScreen
{
    /// <summary>What KEEP IT says under itself when it cannot be pressed.</summary>
    private const string NoRoomNote = "NO ROOM";
    private const string UnseenNote = "NOT UNSEEN";

    /// <summary>What the round has done so far, read one line at a time, and the line on screen now.</summary>
    private readonly Queue<string> _log = new();
    private string _logLine;

    /// <summary>Whether the log being read leads back to the player's turn instead of on to the next round.</summary>
    /// <remarks>A pocket played mid-turn and a decision about the hand both go through the same reading, so this tells them apart.</remarks>
    private bool _resumeTurn;

    /// <summary>What the opponent had the last time their face was checked, so a hit can be shown.</summary>
    private int _opponentLivesShown;

    /// <summary>The check being played, or null unless one is, and what to do with its result once it is in.</summary>
    private SkillCheckGame _check;
    private Action<float> _afterCheck;

    /// <summary>Hands the table to the player, with the three plates set for what is in their hand.</summary>
    /// <remarks>KEEP IT is the one plate that can be refused, and it says why on itself rather than vanishing.</remarks>
    private void BeginTurn()
    {
        _phase = RoundPhase.PlayerTurn;

        _keepButton.Enabled = RoundEngine.CanPocketDealt(_run);
        _keepButton.Sublabel = _keepButton.Enabled ? null : _run.DealtBlind ? UnseenNote : NoRoomNote;

        _useButton.Reset();
        _keepButton.Reset();
        _leaveButton.Reset();
    }

    /// <summary>Takes the player's decision about the item in their hand.</summary>
    /// <param name="choice">What they decided.</param>
    private void Decide(DealtChoice choice)
    {
        if (_phase != RoundPhase.PlayerTurn) return;
        if (choice == DealtChoice.Pocket && !RoundEngine.CanPocketDealt(_run)) return;

        // Items that need a check go through it first. A blind item skips it, since the check would give it away.
        if (choice == DealtChoice.Use && !_run.DealtBlind
            && ItemCatalog.TryParse(_run.Dealt, out ItemId dealt)
            && BeginCheck(ItemCatalog.CheckFor(dealt),
                efficiency => AfterPlayerAction(RoundEngine.DecideDealt(_run, DealtChoice.Use, _random, efficiency))))
            return;

        AfterPlayerAction(RoundEngine.DecideDealt(_run, choice, _random));
    }

    /// <summary>Plays something out of the player's pockets, mid-turn.</summary>
    /// <param name="slot">Which pocket was clicked.</param>
    private void PlayPocket(int slot)
    {
        if (_phase != RoundPhase.PlayerTurn) return;

        if (slot >= 0 && slot < _run.Banked.Count
            && ItemCatalog.TryParse(_run.Banked[slot], out ItemId pocketed)
            && BeginCheck(ItemCatalog.CheckFor(pocketed),
                efficiency => AfterPlayerAction(RoundEngine.PlayFromPocket(_run, slot, _random, efficiency))))
            return;

        AfterPlayerAction(RoundEngine.PlayFromPocket(_run, slot, _random));
    }

    /// <summary>Starts a check, if the item asks for one, and remembers what to do with the result.</summary>
    /// <remarks>The item stays put until the check is over, so leaving mid-check just asks it again next time.</remarks>
    /// <param name="kind">Which check the item asks for.</param>
    /// <param name="then">What to do with the efficiency the check produces.</param>
    /// <returns>True if a check began, false if the item asks nothing.</returns>
    private bool BeginCheck(SkillCheck kind, Action<float> then)
    {
        _check = kind switch
        {
            SkillCheck.Aim => _aimCheck,
            SkillCheck.Steady => _steadyCheck,
            SkillCheck.Read => _readCheck,
            _ => null,
        };
        if (_check is null) return false;

        _afterCheck = then;
        _check.Begin(_random);
        _phase = RoundPhase.SkillCheck;
        return true;
    }

    /// <summary>Runs the check, and when it is over, does what was waiting on it.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateCheck(GameTime gameTime)
    {
        if (_check is null)
        {
            _phase = RoundPhase.PlayerTurn;
            return;
        }

        _check.Update(gameTime);
        if (!_check.IsDone) return;

        float efficiency = _check.Efficiency;
        Action<float> then = _afterCheck;
        _check = null;
        _afterCheck = null;

        then?.Invoke(efficiency);
    }

    /// <summary>Reads out what the player just did, and works out what comes after it.</summary>
    /// <remarks>
    /// Three cases: somebody is out of lives and the round closes now; the player is still holding
    /// something and gets the turn back; or the hand is decided and the opponent goes.
    /// </remarks>
    /// <param name="lines">What the player's action did.</param>
    private void AfterPlayerAction(List<string> lines)
    {
        foreach (string line in lines) _log.Enqueue(line);

        if (RoundEngine.IsOver(_run))
        {
            _resumeTurn = false;
            foreach (string line in RoundEngine.CloseRound(_run)) _log.Enqueue(line);
        }
        else if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            _resumeTurn = true;
        }
        else
        {
            _resumeTurn = false;
            foreach (string line in RoundEngine.OpponentTurn(_run, _random)) _log.Enqueue(line);
            foreach (string line in RoundEngine.CloseRound(_run)) _log.Enqueue(line);
        }

        ShowWounds();

        _phase = RoundPhase.Resolving;
        _continueButton.Reset();
        StepLog();
    }

    /// <summary>Puts whatever has just been taken off the opponent onto their face.</summary>
    /// <remarks>Compared to what was last shown, so a second hit in the same turn is a second flinch.</remarks>
    private void ShowWounds()
    {
        if (_run.OpponentLives < _opponentLivesShown) _opponent.Pose = OpponentPose.Hurt;
        _opponentLivesShown = _run.OpponentLives;
    }

    /// <summary>Shows the next line of the log, or moves on if there are none left.</summary>
    private void StepLog()
    {
        if (_log.Count > 0)
        {
            _logLine = _log.Dequeue();
            return;
        }

        _logLine = null;

        // Out of lives on either side means no next round.
        if (RoundEngine.IsOver(_run))
        {
            _phase = RoundPhase.Over;
            _returnButton.Reset();
            return;
        }

        if (_resumeTurn)
        {
            _resumeTurn = false;
            ResumeTurn();
            return;
        }

        StartDiscussion();
    }

    /// <summary>Gives the table back to the player after a mid-turn reading.</summary>
    /// <remarks>If the hand somehow emptied, the turn ends instead of stranding the round on three plates.</remarks>
    private void ResumeTurn()
    {
        if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            BeginTurn();
            return;
        }

        AfterPlayerAction(new List<string>());
    }
}
