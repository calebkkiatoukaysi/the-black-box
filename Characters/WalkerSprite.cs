using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Characters;

/// <summary>
/// One figure walking around the lobby: a frame cut from a walking sheet, animated while it moves,
/// standing on a soft shadow.
/// </summary>
/// <remarks>
/// A sheet is five columns (stand, then four walking frames) and four rows, one per direction.
/// The player's sheets have three of those blocks stacked, one per accessory. Columns 1 and 3
/// are the frames a foot lands on, which is when <see cref="Stepped"/> goes off, so footsteps
/// are tied to the animation and can never come faster than the feet.
/// </remarks>
public class WalkerSprite
{
    /// <summary>One frame of a walking sheet, in art pixels.</summary>
    public const int FrameWidth = 32;
    public const int FrameHeight = 48;

    /// <summary>The walking frames that follow the standing one.</summary>
    private const int WalkFrames = 4;

    /// <summary>Rows per block of a sheet: one per direction.</summary>
    private const int RowsPerBlock = 4;

    /// <summary>How big the lobby draws everything. A whole number so the pixels stay square.</summary>
    public const float Scale = 3f;

    /// <summary>Where the feet are in a frame: the middle, at the bottom of the shoes. The figure is drawn from here.</summary>
    private static readonly Vector2 FeetInFrame = new(16f, 46f);

    /// <summary>Seconds per walking frame. Four of them is one stride.</summary>
    private const float StepTime = 0.14f;

    /// <summary>The shadow under the feet: how wide and how tall, in art pixels, and how dark.</summary>
    private const float ShadowWidth = 18f;
    private const float ShadowHeight = 5f;
    private static readonly Color ShadowColor = new Color(0, 0, 0) * 0.55f;

    private Texture2D _shadow;
    private float _clock;
    private int _frame;

    /// <summary>The sheet to cut frames from.</summary>
    public Texture2D Sheet { get; set; }

    /// <summary>Where the feet are, in world pixels.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Which way they are facing.</summary>
    public Direction Facing { get; set; } = Direction.Down;

    /// <summary>Which block of rows to draw from. Only the player's sheets have more than one.</summary>
    public Accessory Wearing { get; set; } = Accessory.None;

    /// <summary>Whether they are walking. Standing still shows the first column.</summary>
    public bool IsWalking { get; set; }

    /// <summary>Raised on the frames a foot comes down.</summary>
    public event Action Stepped;

    /// <summary>Loads the shadow, which is the box's glow squashed flat, same as the box's own shadow on the table.</summary>
    /// <param name="content">The content manager to load with.</param>
    public void LoadContent(ContentManager content) => _shadow = content.Load<Texture2D>("glow");

    /// <summary>Runs the walk cycle, or stands still.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsWalking)
        {
            _clock = 0f;
            _frame = 0;
            return;
        }

        _clock += (float)gameTime.ElapsedGameTime.TotalSeconds;
        int frame = 1 + (int)(_clock / StepTime) % WalkFrames;

        // Starting to walk lands the first foot straight away, so a single tap still makes a sound.
        if (frame != _frame && (frame == 1 || frame == 3)) Stepped?.Invoke();
        _frame = frame;
    }

    /// <summary>Draws the shadow and the figure, standing at <see cref="Position"/>.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="layerDepth">Where it sorts. The lobby works it out from how far down the room the feet are.</param>
    /// <param name="scale">How big. The lobby uses <see cref="Scale"/>; the customization preview is bigger.</param>
    public void Draw(SpriteBatch spriteBatch, float layerDepth, float scale = Scale)
    {
        if (Sheet is null) return;

        Vector2 feet = new(MathF.Round(Position.X), MathF.Round(Position.Y));

        if (_shadow is not null)
        {
            var shadow = new Rectangle(
                (int)MathF.Round(feet.X - ShadowWidth * scale / 2f),
                (int)MathF.Round(feet.Y - ShadowHeight * scale / 2f),
                (int)MathF.Round(ShadowWidth * scale),
                (int)MathF.Round(ShadowHeight * scale));
            spriteBatch.Draw(_shadow, shadow, null, ShadowColor, 0f, Vector2.Zero, SpriteEffects.None,
                MathF.Min(1f, layerDepth + 0.0001f));
        }

        int row = (int)Wearing * RowsPerBlock + (int)Facing;
        if ((row + 1) * FrameHeight > Sheet.Height) row = (int)Facing;

        var source = new Rectangle(_frame * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
        spriteBatch.Draw(Sheet, feet, source, Color.White, 0f, FeetInFrame, scale, SpriteEffects.None, layerDepth);
    }

    /// <summary>Turns to face a point, along whichever axis it is further along.</summary>
    /// <param name="target">What to face, in world pixels.</param>
    public void Face(Vector2 target)
    {
        Vector2 to = target - Position;
        Facing = MathF.Abs(to.X) > MathF.Abs(to.Y)
            ? (to.X < 0 ? Direction.Left : Direction.Right)
            : (to.Y < 0 ? Direction.Up : Direction.Down);
    }
}
