using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Checks;

/// <summary>
/// The aim check: a sight over the opponent, steer it on and fire before the time runs out.
/// </summary>
/// <remarks>
/// The sight drifts more the longer you hold the shot, so waiting for it to settle does not
/// work. Efficiency is 1 inside the inner circle, 0 outside the outer, and a line between.
/// </remarks>
public class AimCheck : SkillCheckGame
{
    /// <summary>Where the sight starts: off to one side of the target by this much plus some spread, and a bit up or down.</summary>
    private const float StartAcross = 220f;
    private const float StartAcrossSpread = 120f;
    private const float StartRise = 60f;

    /// <summary>The two sine waves the tremor is made of, per axis. Rates that do not divide evenly, so it never loops.</summary>
    private const float TremorSlowX = 3.1f, TremorFastX = 7.3f;
    private const float TremorSlowY = 2.6f, TremorFastY = 8.9f;
    private const float TremorSlowWeight = 0.7f, TremorFastWeight = 0.3f;

    /// <summary>Width and height of one frame in sight-sheet.png.</summary>
    private const int FrameSize = 24;

    /// <summary>Frames in the sheet. A quarter turn of the ticks looks like a full one.</summary>
    private const int Frames = 4;

    /// <summary>How far the art is blown up. Same as the table.</summary>
    private const float Scale = 4f;

    /// <summary>How fast the ticks turn, in frames a second.</summary>
    private const float SpinRate = 10f;

    /// <summary>How fast the keys or the stick move the sight, in screen pixels a second.</summary>
    private const float Speed = 720f;

    /// <summary>How long the player has before the shot goes on its own, in seconds.</summary>
    private const float Limit = 4f;

    /// <summary>How long the sight stays where the shot went, in seconds.</summary>
    private const float Linger = 0.35f;

    /// <summary>How far the sight wanders at the start and at the end, in screen pixels.</summary>
    private const float TremorAtStart = 22f;
    private const float TremorAtEnd = 70f;

    /// <summary>The radius of the sight's own collision circle, in screen pixels.</summary>
    private const float SightRadius = 6f;

    private Texture2D _sheet;
    private Vector2 _target;
    private float _inner;
    private float _outer;
    private Vector2 _aim;
    private Vector2 _tremor;
    private float _phaseA;
    private float _phaseB;
    private float _time;
    private float _lingering = -1f;
    private float _frameTimer;
    private int _frame;

    /// <summary>Makes an aim check over a target.</summary>
    /// <param name="target">The middle of what is being aimed at, in screen pixels.</param>
    /// <param name="inner">Inside this radius the shot is clean, in screen pixels.</param>
    /// <param name="outer">Outside this radius the shot is nothing, in screen pixels.</param>
    public AimCheck(Vector2 target, float inner, float outer)
    {
        _target = target;
        _inner = inner;
        _outer = outer;
    }

    /// <summary>The middle of what is being aimed at, in screen pixels.</summary>
    /// <remarks>Settable because the check is built once and the opponent changes with the chapter.</remarks>
    public Vector2 Target
    {
        get => _target;
        set => _target = value;
    }

    /// <inheritdoc/>
    public override SkillCheck Kind => SkillCheck.Aim;

    /// <inheritdoc/>
    public override string Prompt => "HOLD IT ON THEM AND FIRE.  CLICK, SPACE, OR A.";

    /// <summary>Where the sight is on screen, tremor included.</summary>
    public Vector2 Sight => _aim + _tremor;

    /// <summary>The sight's circle, for the test against the target.</summary>
    public BoundingCircle Bounds => new(Sight, SightRadius);

    /// <inheritdoc/>
    public override void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("sight-sheet");

    /// <inheritdoc/>
    public override void Begin(Random random)
    {
        base.Begin(random);

        // Start off to one side so the player has to bring the sight across.
        float side = random.Next(2) == 0 ? -1f : 1f;
        _aim = _target + new Vector2(
            side * (StartAcross + (float)random.NextDouble() * StartAcrossSpread),
            (float)random.NextDouble() * 2f * StartRise - StartRise);
        _phaseA = (float)random.NextDouble() * MathHelper.TwoPi;
        _phaseB = (float)random.NextDouble() * MathHelper.TwoPi;
        _tremor = Vector2.Zero;
        _time = 0f;
        _lingering = -1f;
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
        while (_frameTimer >= 1f / SpinRate)
        {
            _frameTimer -= 1f / SpinRate;
            _frame = (_frame + 1) % Frames;
        }

        // Shot fired. Linger a moment so the player sees where it landed, then finish.
        if (_lingering >= 0f)
        {
            _lingering += elapsed;
            if (_lingering >= Linger) Finish(Score());
            return;
        }

        _time += elapsed;

        if (Input.MouseMoved) _aim = Input.MousePosition;
        _aim += Input.Push * Speed * elapsed;
        _aim.X = Math.Clamp(_aim.X, 0f, BlackBoxGame.ScreenWidth);
        _aim.Y = Math.Clamp(_aim.Y, 0f, BlackBoxGame.ScreenHeight);

        // The tremor grows the longer the shot is held.
        float amount = MathHelper.Lerp(TremorAtStart, TremorAtEnd, Math.Min(1f, _time / Limit));
        _tremor = new Vector2(
            MathF.Sin(_time * TremorSlowX + _phaseA) * TremorSlowWeight + MathF.Sin(_time * TremorFastX + _phaseB) * TremorFastWeight,
            MathF.Cos(_time * TremorSlowY + _phaseB) * TremorSlowWeight + MathF.Sin(_time * TremorFastY + _phaseA) * TremorFastWeight) * amount;

        if (Input.Fired || _time >= Limit)
            _lingering = 0f;
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (_sheet is null) return;

        var source = new Rectangle(_frame * FrameSize, 0, FrameSize, FrameSize);
        var origin = new Vector2(FrameSize / 2f, FrameSize / 2f);
        Vector2 at = new(MathF.Round(Sight.X), MathF.Round(Sight.Y));

        // Red once the shot has gone.
        Color tint = _lingering >= 0f ? Palette.FiredTint : Color.White;

        spriteBatch.Draw(_sheet, at, source, tint, 0f, origin, Scale, SpriteEffects.None, Layers.Sight);
    }

    /// <summary>How well the shot was placed: clean inside the inner circle, nothing outside the outer.</summary>
    private float Score()
    {
        var inner = new BoundingCircle(_target, _inner);
        var outer = new BoundingCircle(_target, _outer);
        BoundingCircle sight = Bounds;

        if (CollisionHelper.Collides(sight, inner)) return 1f;
        if (!CollisionHelper.Collides(sight, outer)) return 0f;

        float distance = Vector2.Distance(sight.Center, _target);
        return 1f - (distance - _inner) / (_outer - _inner);
    }
}
