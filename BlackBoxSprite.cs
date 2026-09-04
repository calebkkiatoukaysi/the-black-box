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
/// The box is the director for its eyes. It owns the single gaze point they all watch and
/// the schedule for the box-wide blink, while each <see cref="EyeSprite"/> owns only its own
/// animation. Pointing every eye at one shared target is what makes eighteen sprites read as
/// one thing looking back at you instead of eighteen independent decorations.
/// </remarks>
public class BlackBoxSprite
{
    /// <summary>Width and height of black-box.png.</summary>
    private const int TextureSize = 128;

    /// <summary>The art is small and blown up by a whole number to stay crisp.</summary>
    public const float DrawScale = 4f;

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

    /// <summary>
    /// Where one eye hangs, in the box texture's own 128x128 pixel space, how big it is
    /// relative to <see cref="DrawScale"/>, and how far back in the void it sits.
    /// </summary>
    /// <remarks>
    /// Authoring in texture space means the eyes follow the box automatically when it bobs,
    /// moves or is rescaled.
    /// </remarks>
    private readonly record struct EyeSlot(float X, float Y, float Size, float Depth);

    /// <summary>
    /// Hand-placed, in three depth bands.
    /// </summary>
    /// <remarks>
    /// Eyes are manually positioned to create a sense of depth and life within the box.
    /// </remarks>
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

    /// <summary>
    /// Builds the box and one eye per slot.
    /// </summary>
    public BlackBoxSprite()
    {
        _eyes = new EyeSprite[EyeSlots.Length];
        for (int i = 0; i < _eyes.Length; i++)
        {
            _eyes[i] = new EyeSprite()
            {
                Scale = DrawScale * EyeSlots[i].Size,
                Depth = EyeSlots[i].Depth,
            };
        }
    }

    /// <summary>
    /// Loads the box texture and every eye's textures using the provided ContentManager.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _texture = content.Load<Texture2D>("black-box");
        foreach (var eye in _eyes) eye.LoadContent(content);
    }

    /// <summary>
    /// Bobs the box, decides where it is looking, and updates every eye.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="viewport">The viewport, used to keep the wandering gaze on screen.</param>
    public void Update(GameTime gameTime, Viewport viewport)
    {
        double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        _totalTime += elapsed;

        // Update the gaze and blink wave based on the elapsed time.
        UpdateGaze(elapsed, viewport);
        // Update the blink wave based on the elapsed time.
        UpdateBlinkWave(elapsed);

        // Anchor eyes to the box's current position before updating them.
        Vector2 topLeft = DrawPosition - new Vector2(TextureSize * DrawScale / 2f);

        for (int i = 0; i < _eyes.Length; i++)
        {
            EyeSlot slot = EyeSlots[i];
            _eyes[i].Position = topLeft + (new Vector2(slot.X, slot.Y) + Drift(i, slot)) * DrawScale;
            _eyes[i].Update(gameTime, _gazePoint);
        }
    }

    /// <summary>
    /// Draws the shell and the void inside it, with no eyes in it yet. (The Box's Body)
    /// </summary>
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
            DrawScale,
            SpriteEffects.None,
            Layers.Box);
    }

    /// <summary>
    /// Draws the red light every open eye spills out of the opening.
    /// </summary>
    /// <remarks>Belongs in an additive batch; see <see cref="BlackBoxGame.Draw"/>.</remarks>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawEyeGlow(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (var eye in _eyes) eye.DrawGlow(gameTime, spriteBatch);
    }

    /// <summary>
    /// Draws the eyes and their pupils, over the glow.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawEyes(GameTime gameTime, SpriteBatch spriteBatch)
    {
        foreach (var eye in _eyes) eye.Draw(gameTime, spriteBatch);
    }

    /// <summary>The box's centre this frame, including the idle bob.</summary>
    /// <remarks>
    /// The bob is rounded to whole screen pixels. At a 3x draw scale a fractional offset
    /// makes the upscaled art shimmer along its edges as it moves, which is exactly the
    /// artefact point sampling is being used to avoid.
    /// </remarks>
    private Vector2 DrawPosition =>
        Position + new Vector2(0f, MathF.Round(MathF.Sin((float)_totalTime * BobSpeed) * BobAmplitude));

    /// <summary>
    /// A slow wander for one eye, so the things in the void are not pinned in place.
    /// </summary>
    /// <remarks>
    /// Scaled by depth: an eye near the mouth is all but fixed, while the ones far down
    /// swim. The rate and phase come from the eye's index rather than from extra slot
    /// fields, which keeps the placement table readable.
    /// </remarks>
    private Vector2 Drift(int index, EyeSlot slot)
    {
        // AI computed the phase, speed, and reach for the eye's drift based on its index and depth.

        // What this does: it calculates a drifting offset for the eye based on its index and depth, creating a natural wandering motion.
        float phase = index * 1.37f;
        float speed = 0.35f + index % 5 * 0.07f;
        float reach = DriftAmplitude * slot.Depth;

        // why Sin and Cos? I find that this creates a more natural wandering motion for the eyes.
        return new Vector2(
            MathF.Sin((float)_totalTime * speed + phase) * reach,
            MathF.Cos((float)_totalTime * speed * 0.7f + phase) * reach);
    }

    /// <summary>
    /// Moves the point every eye is watching. Cursor movement takes priority, then after 2.5s it moves back.
    /// Similar to the input tutorial
    /// </summary>
    private void UpdateGaze(double elapsed, Viewport viewport)
    {
        Point mousePosition = Mouse.GetState().Position;

        // Seed the sample instead of treating the very first frame as a mouse movement, 
        // which would otherwise yank every eye toward wherever the cursor happened to be.

        // If we haven't sampled the mouse yet, treat this as the first frame and center the gaze.
        if (!_hasMouseSample)
        {
            _hasMouseSample = true;
            _lastMousePosition = mousePosition;
            _gazePoint = _gazeTarget = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        }
        // If the mouse has moved, reset the attention timer.
        else if (mousePosition != _lastMousePosition)
        {
            _lastMousePosition = mousePosition;
            _mouseAttention = MouseAttentionDuration;
        }
        // Update the gaze target based on whether the mouse is being attended to.
        if (_mouseAttention > 0)
        {
            _mouseAttention -= elapsed;
            _gazeTarget = mousePosition.ToVector2();
        }
        // If the mouse is not being attended to, the box eventually picks a random point to look at.
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

        // Ease the shared point rather than the individual pupils, so the box turns its
        // attention as one and the per-eye tracking rates only add a little lag on top.
        _gazePoint = Vector2.Lerp(_gazePoint, _gazeTarget, 1f - MathF.Exp(-GazeEase * (float)elapsed));
    }

    /// <summary>
    /// Every so often, blinks every eye in a wave that sweeps across the opening.
    /// </summary>
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
