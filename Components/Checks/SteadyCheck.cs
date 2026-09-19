using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Checks;

/// <summary>
/// The steady check: keep an ember inside a lit band while a wind shoves it around.
/// </summary>
/// <remarks>
/// Efficiency is how much of the time the ember was in the band, with some grace so holding
/// it about four fifths of the time counts as a full hold. Ember and band are both rectangles
/// for the collision test.
/// </remarks>
public class SteadyCheck : SkillCheckGame
{
    /// <summary>Width and height of one frame in ember-sheet.png.</summary>
    private const int FrameSize = 16;

    /// <summary>Frames in the sheet: the ember breathing.</summary>
    private const int Frames = 4;

    /// <summary>Width and height of steady-bar.png, in sheet pixels.</summary>
    private const int BarWidth = 145;
    private const int BarHeight = 8;

    /// <summary>Where the lit band is inside the bar, in sheet pixels.</summary>
    private const int BandStart = 54;
    private const int BandWidth = 37;

    /// <summary>How far the art is blown up. Same as the table.</summary>
    private const float Scale = 4f;

    /// <summary>How fast the ember breathes, in frames a second.</summary>
    private const float BreatheRate = 9f;

    /// <summary>How long the ember has to be held, in seconds.</summary>
    private const float Duration = 3f;

    /// <summary>Held this fraction of the time and the guard holds all the way.</summary>
    private const float Grace = 0.8f;

    /// <summary>How hard the player's push accelerates the ember.</summary>
    private const float PushForce = 2400f;

    /// <summary>How hard the wind pushes at most, and how often it changes, in seconds.</summary>
    private const float WindForce = 1500f;
    private const float WindTurn = 0.45f;

    /// <summary>The first gust's strength as a fraction of WindForce, and the weakest any later gust can be.</summary>
    private const float WindStart = 0.6f;
    private const float WindLeast = 0.4f;

    /// <summary>One gust in this many keeps blowing the same way.</summary>
    private const int WindHoldOdds = 4;

    /// <summary>Mouse dead zone around the ember, and the distance at which it pulls at full strength.</summary>
    private const float MouseDeadZone = 8f;
    private const float MouseReach = 120f;

    /// <summary>How far in from each end of the bar the ember stops, in sheet pixels.</summary>
    private const float GrooveEnd = 6f;

    /// <summary>The ember while it is out of the light.</summary>
    private static readonly Color DimTint = new(150, 130, 130);

    /// <summary>How much of the ember's speed is left after a second of nothing pushing it.</summary>
    private const float Drag = 0.12f;

    /// <summary>The ember's collision box, in sheet pixels. Just the glowing part.</summary>
    private const float EmberBox = 10f;

    private Texture2D _emberSheet;
    private Texture2D _bar;
    private Vector2 _barOrigin;
    private float _x;
    private float _velocity;
    private float _wind;
    private float _windTimer;
    private float _time;
    private float _held;
    private float _frameTimer;
    private int _frame;
    private Random _random;

    /// <summary>Makes a steady check along a bar.</summary>
    /// <param name="barOrigin">The top-left corner of the bar on screen, in screen pixels.</param>
    public SteadyCheck(Vector2 barOrigin)
    {
        _barOrigin = barOrigin;
    }

    /// <inheritdoc/>
    public override SkillCheck Kind => SkillCheck.Steady;

    /// <inheritdoc/>
    public override string Prompt => "HOLD THE EMBER IN THE LIGHT.  KEYS, STICK, OR POINT.";

    /// <summary>The lit band, as a rectangle on screen.</summary>
    public BoundingRectangle Band => new(
        _barOrigin.X + BandStart * Scale, _barOrigin.Y, BandWidth * Scale, BarHeight * Scale);

    /// <summary>The ember, as a rectangle on screen.</summary>
    public BoundingRectangle Ember => new(
        _x - EmberBox * Scale / 2f, _barOrigin.Y + BarHeight * Scale / 2f - EmberBox * Scale / 2f,
        EmberBox * Scale, EmberBox * Scale);

    /// <inheritdoc/>
    public override void LoadContent(ContentManager content)
    {
        _emberSheet = content.Load<Texture2D>("ember-sheet");
        _bar = content.Load<Texture2D>("steady-bar");
    }

    /// <inheritdoc/>
    public override void Begin(Random random)
    {
        base.Begin(random);
        _random = random;

        // It starts in the band, and the wind starts blowing at once.
        _x = _barOrigin.X + (BandStart + BandWidth / 2f) * Scale;
        _velocity = 0f;
        _wind = (random.Next(2) == 0 ? -1f : 1f) * WindForce * WindStart;
        _windTimer = 0f;
        _time = 0f;
        _held = 0f;
        _frame = 0;
        _frameTimer = 0f;
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        if (IsDone) return;

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Input.Update();

        _frameTimer += elapsed;
        while (_frameTimer >= 1f / BreatheRate)
        {
            _frameTimer -= 1f / BreatheRate;
            _frame = (_frame + 1) % Frames;
        }

        // Every so often the wind picks a new strength and usually flips direction.
        _windTimer += elapsed;
        if (_windTimer >= WindTurn)
        {
            _windTimer -= WindTurn;
            float sign = _random.Next(WindHoldOdds) == 0 ? MathF.Sign(_wind) : -MathF.Sign(_wind);
            _wind = sign * WindForce * (WindLeast + (1f - WindLeast) * (float)_random.NextDouble());
        }

        // Keys and stick push; the mouse pulls the ember toward itself.
        float push = Input.Push.X;
        if (Input.MouseMoved || Math.Abs(push) < 0.01f)
        {
            float toward = Input.MousePosition.X - _x;
            if (Input.MouseMoved && Math.Abs(toward) > MouseDeadZone) push += Math.Clamp(toward / MouseReach, -1f, 1f);
        }

        _velocity += (_wind + Math.Clamp(push, -1f, 1f) * PushForce) * elapsed;
        _velocity *= MathF.Pow(Drag, elapsed);
        _x += _velocity * elapsed;

        // The ends of the groove stop it dead.
        float left = _barOrigin.X + GrooveEnd * Scale;
        float right = _barOrigin.X + (BarWidth - GrooveEnd) * Scale;
        if (_x < left) { _x = left; _velocity = 0f; }
        if (_x > right) { _x = right; _velocity = 0f; }

        if (CollisionHelper.Collides(Ember, Band)) _held += elapsed;

        _time += elapsed;
        if (_time >= Duration)
            Finish(_held / Duration / Grace);
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (_bar is null || _emberSheet is null) return;

        var barAt = new Vector2(MathF.Round(_barOrigin.X), MathF.Round(_barOrigin.Y));
        spriteBatch.Draw(_bar, barAt, null, Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, Layers.Bar);

        var source = new Rectangle(_frame * FrameSize, 0, FrameSize, FrameSize);
        var origin = new Vector2(FrameSize / 2f, FrameSize / 2f);
        var emberAt = new Vector2(MathF.Round(_x), MathF.Round(_barOrigin.Y + BarHeight * Scale / 2f));

        // Brighter while it is in the light. That is the only feedback the check gives.
        Color tint = CollisionHelper.Collides(Ember, Band) ? Color.White : DimTint;

        spriteBatch.Draw(_emberSheet, emberAt, source, tint, 0f, origin, Scale, SpriteEffects.None, Layers.Marker);
    }
}
