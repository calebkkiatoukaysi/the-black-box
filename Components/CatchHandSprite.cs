using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Collisions;

namespace TheBlackBox;

/// <summary>
/// The player's other hand, open along the near edge of the table, waiting to catch what the box throws.
/// </summary>
/// <remarks>
/// Same sheet as <see cref="HandSprite"/>, open frame only. Mouse, keyboard and gamepad all work at
/// once: the mouse only moves it when the mouse itself moves, and the keys and stick push it from
/// where it is. Only the palm can catch, and it's a bit smaller than the hand looks.
/// </remarks>
public class CatchHandSprite
{
    /// <summary>Width and height of one frame in hand-sheet.png.</summary>
    private const int FrameWidth = 96;

    /// <summary>The other dimension. See <see cref="FrameWidth"/>.</summary>
    private const int FrameHeight = 96;

    /// <summary>The frame with the hand open and fanned: the last one in the sheet.</summary>
    private const int OpenFrame = 4;

    /// <summary>How far the art is blown up. Four on the table, six in the close-up to match the reaching arm.</summary>
    public float Scale { get; set; } = 4f;

    /// <summary>Where the tip of the middle finger is inside a frame, in sheet pixels.</summary>
    private static readonly Vector2 Fingertip = new(17f, 25f);

    /// <summary>Where the middle of the palm is, from the fingertip, in sheet pixels. Read off the generator's skeleton.</summary>
    private static readonly Vector2 PalmFromTip = new(40f, 10f);

    /// <summary>The palm's catching area, in sheet pixels: a little smaller than the hand looks.</summary>
    private const float PalmWidth = 34f;
    private const float PalmHeight = 30f;

    /// <summary>How fast the keys or the stick move the hand, in screen pixels a second.</summary>
    private const float Speed = 1150f;

    /// <summary>Below this the stick is resting, not being pushed.</summary>
    private const float Deadzone = 0.2f;

    private Texture2D _sheet;
    private float _tipX;
    private float _tipY;
    private float _minX;
    private float _maxX;
    private MouseState _priorMouse;

    /// <summary>Whether the hand is out on the table.</summary>
    public bool IsVisible { get; set; }

    /// <summary>The middle of the palm, in screen pixels.</summary>
    public Vector2 PalmCenter => new(_tipX + PalmFromTip.X * Scale, _tipY + PalmFromTip.Y * Scale);

    /// <summary>The rectangle the tag has to land in.</summary>
    public BoundingRectangle Palm
    {
        get
        {
            Vector2 center = PalmCenter;
            return new BoundingRectangle(
                center.X - PalmWidth * Scale / 2f,
                center.Y - PalmHeight * Scale / 2f,
                PalmWidth * Scale,
                PalmHeight * Scale);
        }
    }

    /// <summary>Loads hand-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("hand-sheet");

    /// <summary>Puts the hand out on the table, under the mouse if it's on screen, else in the middle.</summary>
    /// <param name="tipY">How far down the screen the fingertips sit, in screen pixels.</param>
    /// <param name="minX">The furthest left the palm can go, in screen pixels.</param>
    /// <param name="maxX">The furthest right the palm can go, in screen pixels.</param>
    public void Show(float tipY, float minX, float maxX)
    {
        _tipY = tipY;
        _minX = minX;
        _maxX = maxX;
        _priorMouse = Mouse.GetState();

        float palmX = (minX + maxX) / 2f;
        if (_priorMouse.X > 0 && _priorMouse.X < BlackBoxGame.ScreenWidth)
            palmX = _priorMouse.X;

        SetPalmX(palmX);
        IsVisible = true;
    }

    /// <summary>Moves the hand by whatever the player is moving it with.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsVisible) return;

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float palmX = PalmCenter.X;

        // The mouse only sets the hand on frames it actually moved, so a mouse left on the desk doesn't pin it.
        MouseState mouse = Mouse.GetState();
        if (mouse.X != _priorMouse.X || mouse.Y != _priorMouse.Y)
            palmX = mouse.X;
        _priorMouse = mouse;

        // Keys and stick push, and they add up.
        float push = 0f;
        KeyboardState keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A)) push -= 1f;
        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D)) push += 1f;

        GamePadState pad = GamePad.GetState(PlayerIndex.One);
        if (pad.IsConnected)
        {
            float stick = pad.ThumbSticks.Left.X;
            if (Math.Abs(stick) > Deadzone) push += stick;
            if (pad.DPad.Left == ButtonState.Pressed) push -= 1f;
            if (pad.DPad.Right == ButtonState.Pressed) push += 1f;
        }

        palmX += Math.Clamp(push, -1f, 1f) * Speed * elapsed;
        SetPalmX(palmX);
    }

    /// <summary>Draws the open hand along the near edge of the table.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(SpriteBatch spriteBatch)
    {
        if (!IsVisible || _sheet is null) return;

        var source = new Rectangle(OpenFrame * FrameWidth, 0, FrameWidth, FrameHeight);
        var position = new Vector2(
            MathF.Round(_tipX - Fingertip.X * Scale),
            MathF.Round(_tipY - Fingertip.Y * Scale));

        spriteBatch.Draw(_sheet, position, source, Color.White, 0f,
            Vector2.Zero, Scale, SpriteEffects.None, Layers.Hand);
    }

    /// <summary>Puts the middle of the palm at an x, inside the table.</summary>
    /// <param name="palmX">Where the palm should be, in screen pixels.</param>
    private void SetPalmX(float palmX)
    {
        palmX = Math.Clamp(palmX, _minX, _maxX);
        _tipX = palmX - PalmFromTip.X * Scale;
    }
}
