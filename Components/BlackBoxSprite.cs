using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox;

/// <summary>
/// The black box: a shell of dead concrete around an opening, and the eyes hanging in it.
/// </summary>
/// <remarks>
/// The box owns the one gaze point all the eyes watch and the box-wide blink; each EyeSprite only
/// runs its own animation. Sharing one target is what makes eighteen eyes read as one thing looking at you.
/// </remarks>
public class BlackBoxSprite
{
    /// <summary>Width and height of black-box.png.</summary>
    private const int TextureSize = 128;

    /// <summary>The contact shadow: how much wider than the box, how tall, how far it sinks under the bottom edge, and its colour.</summary>
    private const float ShadowWidth = 1.30f;
    private const float ShadowHeight = 20f;
    private const float ShadowSink = 0.34f;
    private static readonly Color ShadowColor = new Color(4, 3, 6) * 0.95f;

    /// <summary>The eyes' drift: how the phase and speed come off an eye's index, and how much slower it moves up and down.</summary>
    private const float DriftPhaseStep = 1.37f;
    private const float DriftSpeedBase = 0.35f;
    private const float DriftSpeedStep = 0.07f;
    private const int DriftSpeedSteps = 5;
    private const float DriftVerticalRate = 0.7f;

    /// <summary>Where the opening is inside black-box.png, and how big: the generator's aperture.</summary>
    private const int ApertureX = 26;
    private const int ApertureY = 29;
    private const int ApertureSize = 76;

    /// <summary>What the box is blown up by on the title screen.</summary>
    public const float DrawScale = 4f;

    /// <summary>What it is blown up by on the table.</summary>
    /// <remarks>
    /// Smaller, because the opponent sits behind it. At the title scale the box covered them
    /// from the collar up. The only fractional scale in the game: 2 left the box too small to matter.
    /// </remarks>
    public const float TableScale = 2.5f;

    // The box hangs in the air rather than sitting still.
    private const float BobAmplitude = 7f;
    private const float BobSpeed = 1.15f;

    /// <summary>How far a deep eye wanders, in texture pixels. Near ones barely move at all.</summary>
    private const float DriftAmplitude = 1.6f;

    // Wandering gaze for eyes.
    private const double MinGazeHold = 1.3;
    private const double MaxGazeHold = 2.9;
    private const float GazeEase = 3.2f;

    /// <summary>How long the eyes stay locked on the cursor after it last moved.</summary>
    private const double MouseAttentionDuration = 2.5;

    // Box-wide blink.
    private const double MinBlinkWaveDelay = 7.0;
    private const double MaxBlinkWaveDelay = 13.0;

    /// <summary>Seconds the box-wide blink takes to sweep from the left edge to the right.</summary>
    private const double BlinkWaveSweep = 0.40;

    /// <summary>Where one eye hangs in the 128x128 texture, how big it is, and how deep in the void it sits.</summary>
    /// <remarks>Texture space, so the eyes follow the box when it bobs or gets rescaled.</remarks>
    private readonly record struct EyeSlot(float X, float Y, float Size, float Depth);

    /// <summary>Hand-placed, in three depth bands, to give the void some depth.</summary>
    private static readonly EyeSlot[] EyeSlots =
    {
        // Near the mouth: big, bright, wide of centre.
        new(36, 50, 0.70f, 0.12f), new(90, 54, 0.62f, 0.18f), new(42, 92, 0.58f, 0.22f),
        new(88, 90, 0.54f, 0.25f), new(63, 36, 0.56f, 0.20f), new(64, 98, 0.50f, 0.24f),

        // Mid depth.
        new(46, 60, 0.42f, 0.42f), new(82, 72, 0.38f, 0.48f), new(58, 86, 0.36f, 0.52f),
        new(72, 48, 0.40f, 0.45f), new(44, 76, 0.34f, 0.55f), new(50, 48, 0.32f, 0.58f),

        // Far down: small, barely lit, huddled around the centre.
        new(57, 62, 0.24f, 0.75f), new(70, 70, 0.22f, 0.80f), new(63, 55, 0.20f, 0.85f),
        new(66, 76, 0.19f, 0.88f), new(54, 72, 0.18f, 0.92f), new(74, 62, 0.17f, 0.90f),
    };


    private readonly EyeSprite[] _eyes;

    private Texture2D _texture;
    private double _totalTime;

    private Vector2 _gazePoint;
    private Vector2 _gazeTarget;
    private double _gazeHold;


    // How the box tracks the mouse and decides where to look.
    private Point _lastMousePosition;

    // Whether the box has a recent mouse position sample.
    private bool _hasMouseSample;

    // How much attention the box is paying to the mouse, decays over time.
    private double _mouseAttention;

    private double _blinkWaveTimer = MinBlinkWaveDelay;

    /// <summary>Centre of the box in screen space, before the idle bob is applied.</summary>
    public Vector2 Position;

    /// <summary>The radial falloff the contact shadow is drawn from.</summary>
    private Texture2D _glow;

    /// <summary>How far the art is blown up. A field, because the box is two sizes in two scenes.</summary>
    public float Scale = DrawScale;

    /// <summary>Builds the box and one eye per slot.</summary>
    public BlackBoxSprite()
    {
        _eyes = new EyeSprite[EyeSlots.Length];
        for (int i = 0; i < _eyes.Length; i++)
        {
            // Eye scale is set again in Update, so rescaling the box mid-game brings the eyes with it.
            _eyes[i] = new EyeSprite()
            {
                Scale = DrawScale * EyeSlots[i].Size,
                Depth = EyeSlots[i].Depth,
            };
        }
    }

    /// <summary>Loads the box texture and every eye's textures.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _glow = content.Load<Texture2D>("glow");
        _texture = content.Load<Texture2D>("black-box");
        foreach (var eye in _eyes) eye.LoadContent(content);
    }

    /// <summary>Bobs the box, decides where it is looking, and updates every eye.</summary>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="viewport">The viewport, used to keep the wandering gaze on screen.</param>
    public void Update(GameTime gameTime, Viewport viewport)
    {
        double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        _totalTime += elapsed;

        UpdateGaze(elapsed, viewport);
        UpdateBlinkWave(elapsed);

        // Anchor eyes to the box's current position before updating them.
        Vector2 topLeft = DrawPosition - new Vector2(TextureSize * Scale / 2f);

        for (int i = 0; i < _eyes.Length; i++)
        {
            EyeSlot slot = EyeSlots[i];
            _eyes[i].Scale = Scale * slot.Size;
            _eyes[i].Position = topLeft + (new Vector2(slot.X, slot.Y) + Drift(i, slot)) * Scale;
            _eyes[i].Update(gameTime, _gazePoint);
        }
    }

    /// <summary>Draws the shell and the void inside it, with no eyes in it yet. (The Box's Body)</summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawBody(GameTime gameTime, SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(
            _texture,
            DrawPosition,
            null,
            Color.White,
            0f,
            new Vector2(TextureSize / 2f),
            Scale,
            SpriteEffects.None,
            Layers.Box);
    }

    /// <summary>Draws the contact shadow under the box, so it sits on the table instead of floating.</summary>
    /// <remarks>
    /// It's glow.png squashed flat and drawn near-black. It sits just below the bottom edge:
    /// centred on it, the top half of the ellipse stuck out either side like feet.
    /// </remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawContactShadow(SpriteBatch spriteBatch)
    {
        if (_glow is null) return;

        float width = TextureSize * Scale * ShadowWidth;
        float height = Scale * ShadowHeight;
        float bottom = DrawPosition.Y + TextureSize * Scale / 2f;

        var destination = new Rectangle(
            (int)MathF.Round(Position.X - width / 2f),
            (int)MathF.Round(bottom - height * ShadowSink),
            (int)MathF.Round(width),
            (int)MathF.Round(height));

        spriteBatch.Draw(_glow, destination, null, ShadowColor,
            0f, Vector2.Zero, SpriteEffects.None, Layers.BoxShadow);
    }

    /// <summary>Draws the red light every open eye spills out of the opening. Needs an additive batch.</summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawEyeGlow(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (var eye in _eyes) eye.DrawGlow(gameTime, spriteBatch);
    }

    /// <summary>Draws the eyes and their pupils, over the glow.</summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawEyes(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (var eye in _eyes) eye.Draw(gameTime, spriteBatch);
    }

    /// <summary>The box's centre this frame, including the idle bob.</summary>
    /// <remarks>Rounded to whole pixels. A fractional bob made the edges shimmer as it moved.</remarks>
    public Vector2 DrawPosition =>
        Position + new Vector2(0f, MathF.Round(MathF.Sin((float)_totalTime * BobSpeed) * BobAmplitude));

    /// <summary>Where the opening is on screen this frame. The lid in the close-up is placed from this.</summary>
    public Rectangle Aperture
    {
        get
        {
            Vector2 topLeft = DrawPosition - new Vector2(TextureSize * Scale / 2f);
            return new Rectangle(
                (int)MathF.Round(topLeft.X + ApertureX * Scale),
                (int)MathF.Round(topLeft.Y + ApertureY * Scale),
                (int)MathF.Round(ApertureSize * Scale),
                (int)MathF.Round(ApertureSize * Scale));
        }
    }

    /// <summary>A slow wander for one eye. Deeper eyes drift more; near ones barely move.</summary>
    private Vector2 Drift(int index, EyeSlot slot)
    {
        // AI computed the phase, speed, and reach for the eye's drift based on its index and depth.
        float phase = index * DriftPhaseStep;
        float speed = DriftSpeedBase + index % DriftSpeedSteps * DriftSpeedStep;
        float reach = DriftAmplitude * slot.Depth;

        // why Sin and Cos? I find that this creates a more natural wandering motion for the eyes.
        return new Vector2(
            MathF.Sin((float)_totalTime * speed + phase) * reach,
            MathF.Cos((float)_totalTime * speed * DriftVerticalRate + phase) * reach);
    }

    /// <summary>
    /// Moves the point every eye is watching. Cursor movement takes priority, then after 2.5s it moves back.
    /// Similar to the input tutorial
    /// </summary>
    private void UpdateGaze(double elapsed, Viewport viewport)
    {
        Point mousePosition = Mouse.GetState().Position;

        // First frame: seed the sample and centre the gaze, or every eye would yank toward the cursor.
        if (!_hasMouseSample)
        {
            _hasMouseSample = true;
            _lastMousePosition = mousePosition;
            _gazePoint = _gazeTarget = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        }
        else if (mousePosition != _lastMousePosition)
        {
            _lastMousePosition = mousePosition;
            _mouseAttention = MouseAttentionDuration;
        }
        if (_mouseAttention > 0)
        {
            _mouseAttention -= elapsed;
            _gazeTarget = mousePosition.ToVector2();
        }
        // Once it loses interest in the mouse, the box picks random points to look at.
        else
        {
            _gazeHold -= elapsed;
            if (_gazeHold <= 0)
            {
                _gazeHold = MinGazeHold + Random.Shared.NextDouble() * (MaxGazeHold - MinGazeHold);
                _gazeTarget = new Vector2(
                    (float)Random.Shared.NextDouble() * viewport.Width,
                    (float)Random.Shared.NextDouble() * viewport.Height);
            }
        }

        // Ease the shared point, not each pupil, so the box turns its attention as one.
        _gazePoint = Vector2.Lerp(_gazePoint, _gazeTarget, 1f - MathF.Exp(-GazeEase * (float)elapsed));
    }

    /// <summary>Every so often, blinks every eye in a wave that sweeps across the opening.</summary>
    private void UpdateBlinkWave(double elapsed)
    {
        _blinkWaveTimer -= elapsed;
        if (_blinkWaveTimer > 0) return;

        _blinkWaveTimer = MinBlinkWaveDelay + Random.Shared.NextDouble() * (MaxBlinkWaveDelay - MinBlinkWaveDelay);

        // Delay each eye by its horizontal position, so the blink rolls left to right.
        for (int i = 0; i < _eyes.Length; i++)
        {
            _eyes[i].RequestBlink(EyeSlots[i].X / TextureSize * BlinkWaveSweep);
        }
    }
}
