using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The stages of a single blink
/// <see cref="Open"/> and only passes through the other three on the way back to it.
/// </summary>
public enum EyeState
{
    Open = 0,
    Closing = 1,
    Closed = 2,
    Opening = 3,
}

/// <summary>
/// One red eye hanging in the void inside the black box.
/// </summary>
/// <remarks>
/// The eye runs two behaviours that deliberately do not know about each other: a blink
/// state machine that drives openness, and a pupil that chases whatever gaze
/// point the box hands it. Keeping them separate is what makes the box look alive -- an eye
/// can be halfway through a blink while still turning to follow the mouse.
/// </remarks>
public class EyeSprite
{
    /// <summary>Width and height of one frame in eye-sheet.png.</summary>
    private const int FrameSize = 16;

    // Blink timings, in seconds. Closing faster than opening is what reads as a real blink. (Adjusted for more natural timing, or can be adjusted later.)
    private const double ClosingDuration = 0.09;
    private const double ClosedDuration = 0.03;
    private const double OpeningDuration = 0.17;
    private const double MinBlinkDelay = 1.6;
    private const double MaxBlinkDelay = 6.4;

    /// <summary>How far the pupil may slide inside the eye, in unscaled texture pixels.</summary>
    private const float PupilRangeX = 3.0f;
    private const float PupilRangeY = 1.5f;

    /// <summary>Below this openness the lids have swallowed the pupil, so it stops drawing.</summary>
    private const float PupilVisibleThreshold = 0.14f;

    /// <summary>A gaze point this far away deflects the pupil fully; closer ones deflect less.</summary>
    private const float FullDeflectionDistance = 475f;

    /// <summary>glow.png is 64px across, so this converts the eye's scale into a wider halo.</summary>
    private const float GlowSizeRatio = 0.62f;

    /// <summary>What the red has faded to for an eye hanging at the back of the void.</summary>
    private static readonly Color DeepTint = new(86, 58, 68);

    private readonly Random _random;

    /// <summary>Per-eye tracking rate, so seventeen eyes never swing in lockstep.</summary>
    private readonly float _trackingSpeed;

    /// <summary>Per-eye phase for the brightness shimmer.</summary>
    private readonly float _shimmerPhase;

    private Texture2D _sheet;
    private Texture2D _pupil;
    private Texture2D _glow;

    private EyeState _state = EyeState.Open;
    private double _stateTimer;
    private double _blinkAfter;
    private float _openness = 1f;
    private Vector2 _look;
    private double _totalTime;

    /// <summary>Centre of the eye in screen space. The box rewrites this every frame.</summary>
    public Vector2 Position;

    /// <summary>Final draw scale for the 16x16 frame, already folded in with the box's scale.</summary>
    public float Scale = 1f;

    /// <summary>
    /// How far back inside the void this eye hangs: 0 is just behind the mouth, 1 is barely
    /// there at all. Deeper eyes are dimmer and cast almost no light, which is what gives
    /// the opening the sense that it keeps going.
    /// </summary>
    public float Depth;

    /// <summary>
    /// Creates an eye with a randomised blink schedule and tracking rate. This allows for a less robotic
    /// </summary>
    /// <param name="random">Shared source of randomness, owned by the box.</param>
    public EyeSprite(Random random)
    {
        _random = random;
        _trackingSpeed = 4.5f + (float)random.NextDouble() * 4.5f;
        _shimmerPhase = (float)random.NextDouble() * MathHelper.TwoPi;

        // Stagger the very first blink so the box does not open with all eyes in sync.
        _blinkAfter = random.NextDouble() * MaxBlinkDelay; // AI helped me with this, staggering the initial blink for natural variation.
    }

    /// <summary>
    /// Loads the eye, pupil and glow textures using the provided ContentManager.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        // Our PNG sprites
        _sheet = content.Load<Texture2D>("eye-sheet");
        _pupil = content.Load<Texture2D>("pupil");
        _glow = content.Load<Texture2D>("glow");
    }

    /// <summary>
    /// Advances the blink animation and turns the pupil toward the supplied gaze point.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="gazePoint">Screen position every eye on the box is currently watching.</param>
    public void Update(GameTime gameTime, Vector2 gazePoint)
    {
        double elapsed = gameTime.ElapsedGameTime.TotalSeconds;
        _totalTime += elapsed;

        UpdateBlink(elapsed);
        UpdateGaze(elapsed, gazePoint);
    }

    /// <summary>
    /// Asks the eye to blink sooner than it had planned to.
    /// </summary>
    /// <remarks>
    /// The box uses this to sweep a blink across every socket at once. The request can only
    /// pull a blink earlier, never push one later, and an eye already mid-blink ignores it.
    /// </remarks>
    /// <param name="delay">Seconds to wait before blinking.</param>
    public void RequestBlink(double delay)
    {
        if (_state == EyeState.Open) _blinkAfter = Math.Min(_blinkAfter, _stateTimer + delay);
    }

    /// <summary>
    /// Draws the red light this eye spills out of the void.
    /// </summary>
    /// <remarks>Belongs in an additive batch; see <see cref="BlackBoxGame.Draw"/>.</remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawGlow(GameTime gameTime, SpriteBatch spriteBatch)
    {
        //sin wave parameters for the glow shimmer effect
        float basePulse = 0.84f;
        float shimmerAmplitude = 0.16f;
        float angularFrequency = 2.1f;

        // Skip drawing the glow if the eye is almost closed.
        if (_openness <= 0.05f) return;

        // A slow shimmer keeps the glow from looking like a static decal.
        float pulse = basePulse + shimmerAmplitude * MathF.Sin((float)_totalTime * angularFrequency + _shimmerPhase);
        float intensity = _openness * pulse * (1f - Depth * 0.75f);
        var origin = new Vector2(_glow.Width / 2f, _glow.Height / 2f);

        spriteBatch.Draw(
            _glow,
            Position,
            null,
            new Color(255, 58, 40) * intensity,
            0f,
            origin,
            Scale * GlowSizeRatio * (0.95f + 0.10f * pulse),
            SpriteEffects.None,
            Layers.EyeGlow);
    }

    /// <summary>
    /// Draws the eye and its pupil (unless the lid is closed.)
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var source = new Rectangle(FrameIndex * FrameSize, 0, FrameSize, FrameSize);

        // https://gamedev.stackexchange.com/questions/118241/monogame-how-to-make-a-smooth-color-transition-with-color-lerp
        Color tint = Color.Lerp(Color.White, DeepTint, Depth);

        // Eye
        spriteBatch.Draw(
            _sheet,
            Position,
            source,
            tint,
            0f,
            new Vector2(FrameSize / 2f),
            Scale,
            SpriteEffects.None,
            Layers.Eye);

        if (_openness <= PupilVisibleThreshold) return;

        // Adjusts the pupil's position based on the eye's look direction and openness
        var offset = new Vector2(_look.X * PupilRangeX, _look.Y * PupilRangeY * _openness) * Scale;

        // Pupil
        spriteBatch.Draw(
            _pupil,
            Position + offset,
            null,
            tint,
            0f,
            new Vector2(_pupil.Width / 2f, _pupil.Height / 2f),
            new Vector2(Scale, Scale * _openness),
            SpriteEffects.None,
            Layers.Pupil);
    }

    /// <summary>Picks the blink frame that best matches the current openness.</summary>
    private int FrameIndex => _openness switch
    {
        >= 0.85f => 0,
        >= 0.55f => 1,
        >= 0.30f => 2,
        >= 0.10f => 3,
        _ => 4,
    };

    /// <summary>Runs the blink state machine for one frame.</summary>
    private void UpdateBlink(double elapsed)
    {
        _stateTimer += elapsed;

        switch (_state)
        {
            case EyeState.Open:
                _openness = 1f;
                if (_stateTimer >= _blinkAfter) EnterState(EyeState.Closing);
                break;

            case EyeState.Closing:
                _openness = 1f - (float)(_stateTimer / ClosingDuration);
                if (_stateTimer >= ClosingDuration) EnterState(EyeState.Closed);
                break;

            case EyeState.Closed:
                _openness = 0f;
                if (_stateTimer >= ClosedDuration) EnterState(EyeState.Opening);
                break;

            case EyeState.Opening:
                _openness = (float)(_stateTimer / OpeningDuration);
                if (_stateTimer >= OpeningDuration)
                {
                    EnterState(EyeState.Open);
                    _blinkAfter = MinBlinkDelay + _random.NextDouble() * (MaxBlinkDelay - MinBlinkDelay);
                }
                break;
        }

        _openness = MathHelper.Clamp(_openness, 0f, 1f);
    }

    /// <summary>Eases the pupil toward the gaze point.</summary>
    private void UpdateGaze(double elapsed, Vector2 gazePoint)
    {
        Vector2 toGaze = gazePoint - Position;
        float distance = toGaze.Length();

        // An eye staring at something right next to it barely deflects; one staring across
        // the screen deflects all the way. Scaling by distance keeps the pupils from
        // slamming to the edge of the socket every time the gaze passes nearby.
        Vector2 target = distance > float.Epsilon
            ? toGaze / distance * MathHelper.Clamp(distance / FullDeflectionDistance, 0f, 1f)
            : Vector2.Zero;

        // Exponential smoothing, so the tracking rate does not change with frame rate.
        _look = Vector2.Lerp(_look, target, 1f - MathF.Exp(-_trackingSpeed * (float)elapsed));
    }

    /// <summary>Switches state and restarts the state clock.</summary>
    private void EnterState(EyeState state)
    {
        _state = state;
        _stateTimer = 0;
    }
}
