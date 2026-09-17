using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Which picture of the opponent is on the table.
/// </summary>
/// <remarks>
/// The first three are the <see cref="Warmth"/> bands and are what they look like while
/// there is talking to do. The last three are things they are doing, and override the temper
/// for as long as they are doing them.
/// </remarks>
public enum OpponentPose
{
    /// <summary>Done being civil.</summary>
    Hostile = 0,

    /// <summary>Giving nothing away either direction.</summary>
    Even = 1,

    /// <summary>They have decided you are a person.</summary>
    Open = 2,

    /// <summary>Mid-word. Held for a beat when a line lands, not for as long as it is on screen.</summary>
    Talking = 3,

    /// <summary>Leant in over the table with an arm in the box.</summary>
    Reaching = 4,

    /// <summary>Something just landed on them.</summary>
    Hurt = 5,
}

/// <summary>
/// The figure across the table: head, shoulders, what they think of you, and what has
/// already been taken off them.
/// </summary>
/// <remarks>
/// <para>
/// Static. There is no timer in this class and nothing in it moves -- every cell of the
/// sheet is one finished picture, and the game switches between them. A jaw flapping through
/// a written line it has no audio for reads as a puppet, and a blink on a loop reads as a
/// screensaver; what the player is meant to notice is that the face is not the one that was
/// there a moment ago. <see cref="OpponentPose.Talking"/> is one picture for the same reason:
/// it is struck when a line arrives and dropped a moment later by whoever set it, so the
/// mouth is open on the beat the words land and shut again while they are still being read.
/// </para>
/// <para>
/// Two axes. <see cref="Pose"/> is the column and is either a <see cref="Warmth"/> band or
/// something they are doing; <see cref="Injury"/> is the row and is how many lives they have
/// lost. The two are independent, so a man who is losing can still be pleased to see you.
/// </para>
/// </remarks>
public class OpponentSprite
{
    /// <summary>Width of one frame in opponent-sheet.png.</summary>
    private const int FrameWidth = 160;

    /// <summary>
    /// Height of one frame. See <see cref="FrameWidth"/>.
    /// </summary>
    /// <remarks>
    /// The bottom row of the frame is the far edge of the table and there is no more of them
    /// to draw below it. Above it the frame is tall: at 4x it runs from the table lip to the
    /// top of the wall, which is what makes them the thing the room is about rather than a
    /// head behind a box. The box in front covers one shoulder and nothing else.
    /// </remarks>
    private const int FrameHeight = 150;

    /// <summary>Rows in the sheet, which is how many times they can be hurt and stay up.</summary>
    private const int InjuryRows = 3;

    private Texture2D _sheet;

    /// <summary>Which picture is on the table.</summary>
    public OpponentPose Pose { get; set; } = OpponentPose.Even;

    /// <summary>How many lives they have lost, from 0 to <see cref="InjuryRows"/> - 1.</summary>
    public int Injury { get; private set; }

    /// <summary>Loads opponent-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("opponent-sheet");

    /// <summary>
    /// Puts them in the temper their disposition says they are in.
    /// </summary>
    /// <remarks>
    /// Ignored while they are doing something. A man with his arm inside the box is not
    /// showing you what he thinks of you, and overwriting <see cref="OpponentPose.Reaching"/>
    /// with a mood would snap him upright in the middle of paying. The caller that set the
    /// pose is the one that clears it, by putting them back on a temper frame first -- which
    /// is also how the disposition a line changed reaches the face, a beat after it is said.
    /// </remarks>
    /// <param name="disposition">What they currently think of the player.</param>
    public void SetDisposition(Disposition disposition)
    {
        if (Pose is OpponentPose.Reaching or OpponentPose.Hurt or OpponentPose.Talking) return;

        Pose = disposition.Band switch
        {
            Warmth.Hostile => OpponentPose.Hostile,
            Warmth.Open => OpponentPose.Open,
            _ => OpponentPose.Even,
        };
    }

    /// <summary>
    /// Records how much has been taken off them.
    /// </summary>
    /// <param name="livesLeft">What they have left to lose.</param>
    public void SetLives(int livesLeft) =>
        Injury = Math.Clamp(SaveData.StartingLives - livesLeft, 0, InjuryRows - 1);

    /// <summary>
    /// Draws whichever picture of them is current.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="bottomCentre">
    /// Where the bottom middle of the frame lands, which is where the table cuts them off.
    /// Anchored there rather than at the centre so that moving the table does not mean
    /// recomputing where their middle has to be.
    /// </param>
    /// <param name="scale">How far the art is blown up. Whole numbers keep it crisp.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 bottomCentre, float scale)
    {
        if (_sheet is null) return;

        var source = new Rectangle(
            (int)Pose * FrameWidth, Injury * FrameHeight, FrameWidth, FrameHeight);

        var position = new Vector2(
            MathF.Round(bottomCentre.X - FrameWidth * scale / 2f),
            MathF.Round(bottomCentre.Y - FrameHeight * scale));

        spriteBatch.Draw(_sheet, position, source, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Opponent);
    }
}
