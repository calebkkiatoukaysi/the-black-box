using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheBlackBox.Collisions;

namespace TheBlackBox;

/// <summary>
/// The hand going into the box, the close-up it happens in, and the tag that comes back out to be caught.
/// 
/// (NEXT TO FIX, Hand is a little off, next update!)
/// </summary>
public partial class TableScreen
{
    /// <summary>The close-up of the box: where it sits and how big it is.</summary>
    /// <remarks>
    /// When a hand is offered the table goes away and the box fills the view at 6x. The hands
    /// are drawn at 6x too so the arm going in matches what it is going into.
    /// </remarks>
    private static readonly Vector2 CloseUpCentre = new(610f, 440f);
    private const float CloseUpScale = 6f;
    private const float HandScale = 6f;

    /// <summary>Where the fingertips wait before the hand is offered, and where they end up: inside the mouth.</summary>
    /// <remarks>Both are on the line the arm is drawn along, so it slides out along its own length instead of drifting sideways.</remarks>
    private static readonly Vector2 HandRest = new(1560f, 1120f);
    private static readonly Vector2 HandMouth = new(540f, 470f);

    /// <summary>How long the hand takes to go in, and how long the box holds it before it pays, in seconds.</summary>
    /// <remarks>The hold before paying is the point. Dealing the instant the fingers cross the rim made it a vending machine.</remarks>
    private const float ReachSeconds = 1.35f;
    private const float TakeSeconds = 0.9f;

    /// <summary>Where the tag leaves the box: the mouth, a little below where the fingers went in.</summary>
    /// <remarks>The catch is meant to be short and fair. A hand on the keys can get across the table in time.</remarks>
    private static readonly Vector2 TokenLaunch = new(610f, 520f);

    /// <summary>How fast the tag leaves the mouth, and the most sideways drift it can leave with either way, in screen pixels a second.</summary>
    private const float TokenLaunchSpeed = 90f;
    private const float TokenDrift = 360f;

    /// <summary>How far down the screen the catching hand's fingertips sit, and how far left and right the palm can go.</summary>
    private const float CatchTipY = 640f;
    private const float CatchMinX = 300f;
    private const float CatchMaxX = 1300f;

    /// <summary>The dark past the near edge of the table. A tag that gets there is gone.</summary>
    /// <remarks>Starts a little under the bottom of the screen so the tag is seen to leave, not cut off halfway.</remarks>
    private static readonly BoundingRectangle TheDark =
        new(0f, BlackBoxGame.ScreenHeight + 10f, BlackBoxGame.ScreenWidth, 400f);

    /// <summary>How far the hand has gone in, from 0 to 1, and then how long it is held.</summary>
    private float _reach;
    private float _held;

    /// <summary>Whether the table has given way to the close-up of the box.</summary>
    private bool _closeUp;

    /// <summary>Starts the hand moving. Raised by the one button on the table.</summary>
    private void OfferHand()
    {
        if (_phase != RoundPhase.Offer) return;

        _phase = RoundPhase.Reaching;
        _reach = 0f;
        _held = 0f;

        _hand.Reach = 0f;
        _hand.IsVisible = true;
        EnterCloseUp();

        // Both hands go in together. Theirs is never drawn, so the lean is the whole of it.
        _opponent.Pose = OpponentPose.Reaching;
    }

    /// <summary>Opens the mouth, puts the hand in, holds it there, and then pays.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateReach(GameTime gameTime)
    {
        // The mouth opens first.
        if (!_lid.IsOpen)
        {
            _lid.Update(gameTime);
            return;
        }

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_reach < 1f)
        {
            _reach = MathF.Min(1f, _reach + elapsed / ReachSeconds);
            _hand.Reach = _reach;
            return;
        }

        _held += elapsed;
        if (_held >= TakeSeconds) Deal();
    }

    /// <summary>Takes the hand, and pays for it.</summary>
    /// <remarks>The items go into the run, not fields, so closing the game mid-decision does not lose them.</remarks>
    private void Deal()
    {
        RoundEngine.DealBoth(_run, _random);

        _hand.IsVisible = false;
        _opponent.SetDisposition(_run.OpponentDisposition);

        Payout();
    }

    /// <summary>Throws what the box dealt out of the mouth, and puts the other hand out for it.</summary>
    /// <remarks>Only when there is something to throw. If the box gave nothing the turn just starts.</remarks>
    private void Payout()
    {
        if (!ItemCatalog.TryParse(_run.Dealt, out _))
        {
            BeginTurn();
            return;
        }

        _phase = RoundPhase.Catching;

        float drift = ((float)_random.NextDouble() * 2f - 1f) * TokenDrift;
        _token.Launch(TokenLaunch, new Vector2(drift, TokenLaunchSpeed));
        _catchHand.Show(CatchTipY, CatchMinX, CatchMaxX);
    }

    /// <summary>Takes the table away and brings the box up, shut, to fill the view.</summary>
    private void EnterCloseUp()
    {
        _closeUp = true;
        _lid.Open = 0f;
        _box.Position = CloseUpCentre;
        _box.Scale = CloseUpScale;
    }

    /// <summary>Puts the box back on the table and the table back on screen.</summary>
    private void LeaveCloseUp()
    {
        _closeUp = false;
        _box.Position = new Vector2(BlackBoxGame.ScreenWidth / 2f, BoxCenterY);
        _box.Scale = BlackBoxSprite.TableScale;
    }

    /// <summary>Moves the tag and the hand, and settles which of two things the tag hits first.</summary>
    /// <remarks>The palm is tested before the edge, so a tag caught right on the lip counts. Same collision shapes as the tutorial.</remarks>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateCatch(GameTime gameTime)
    {
        _token.Update(gameTime);
        _catchHand.Update(gameTime);

        if (CollisionHelper.Collides(_token.Bounds, _catchHand.Palm))
            CatchToken();
        else if (CollisionHelper.Collides(_token.Bounds, TheDark))
            DropToken();
    }

    /// <summary>The tag landed in the palm. The item is in the hand, and the turn is the player's.</summary>
    private void CatchToken()
    {
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        LeaveCloseUp();
        BeginTurn();
    }

    /// <summary>The tag went over the edge. Same rule as LEAVE IT, said differently.</summary>
    private void DropToken()
    {
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        LeaveCloseUp();

        bool blind = _run.DealtBlind;
        string name = ItemCatalog.TryParse(_run.Dealt, out ItemId item)
            ? ItemCatalog.NameOf(item).ToUpperInvariant()
            : "IT";

        List<string> lines = RoundEngine.DecideDealt(_run, DealtChoice.Leave, _random);
        if (lines.Count > 0) lines.RemoveAt(0);
        lines.Insert(0, blind
            ? "IT WENT OFF THE EDGE OF THE TABLE BEFORE YOU COULD LOOK AT IT."
            : "THE " + name + " WENT OFF THE EDGE OF THE TABLE. THE BOX DOES NOT DEAL TWICE.");

        AfterPlayerAction(lines);
    }
}
