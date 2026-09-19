using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Checks;

/// <summary>
/// The read check: three tags slide past each other, one flashes red now and then, pick that one.
/// </summary>
/// <remarks>
/// No partial credit here, it was the marked one or it was not. A click hits a tag using
/// the circle test with a zero-radius circle for the mouse point.
/// </remarks>
public class ReadCheck : SkillCheckGame
{
    /// <summary>How many tags cross the table.</summary>
    private const int Tags = 3;

    /// <summary>Width and height of one frame in sight-sheet.png.</summary>
    private const int SightSize = 24;

    /// <summary>How far the sight is blown up.</summary>
    private const float Scale = 4f;

    /// <summary>How long the player has, in seconds.</summary>
    private const float Limit = 6f;

    /// <summary>Slowest and fastest a tag slides, in screen pixels a second.</summary>
    private const float SlowSpeed = 150f;
    private const float FastSpeed = 260f;

    /// <summary>How often the mark shows, in seconds, and for how long each time.</summary>
    private const float FlashEvery = 0.9f;
    private const float FlashFor = 0.16f;

    /// <summary>How long the pick is shown before the check ends, in seconds.</summary>
    private const float Linger = 0.5f;

    private readonly TokenSprite[] _tags = new TokenSprite[Tags];
    private readonly float[] _speeds = new float[Tags];
    private Texture2D _sightSheet;
    private readonly Vector2 _laneStart;
    private readonly float _laneLength;
    private int _marked;
    private int _highlight;
    private int _picked = -1;
    private float _time;
    private float _lingering;
    private float _flashClock;

    /// <summary>Makes a read check along a lane.</summary>
    /// <param name="laneStart">The left end of the lane, in screen pixels.</param>
    /// <param name="laneLength">How long the lane is, in screen pixels.</param>
    public ReadCheck(Vector2 laneStart, float laneLength)
    {
        _laneStart = laneStart;
        _laneLength = laneLength;
        for (int i = 0; i < Tags; i++) _tags[i] = new TokenSprite();
    }

    /// <inheritdoc/>
    public override SkillCheck Kind => SkillCheck.Read;

    /// <inheritdoc/>
    public override string Prompt => "ONE IS MARKED. WATCH FOR THE RED, AND PICK IT.  CLICK IT, OR 1, 2, 3.";

    /// <inheritdoc/>
    public override void LoadContent(ContentManager content)
    {
        foreach (TokenSprite tag in _tags) tag.LoadContent(content);
        _sightSheet = content.Load<Texture2D>("sight-sheet");
    }

    /// <inheritdoc/>
    public override void Begin(Random random)
    {
        base.Begin(random);

        _marked = random.Next(Tags);
        _highlight = 0;
        _picked = -1;
        _time = 0f;
        _lingering = 0f;
        _flashClock = 0f;

        // Spread them along the lane at different speeds, alternating direction. No gravity, it is a table.
        for (int i = 0; i < Tags; i++)
        {
            float t = (i + 0.5f) / Tags;
            float speed = MathHelper.Lerp(SlowSpeed, FastSpeed, (float)random.NextDouble());
            _speeds[i] = i % 2 == 0 ? speed : -speed;
            _tags[i].Launch(_laneStart + new Vector2(_laneLength * t, 0f), new Vector2(_speeds[i], 0f), gravity: 0f);
        }
    }

    /// <inheritdoc/>
    public override void Update(GameTime gameTime)
    {
        if (IsDone) return;

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Input.Update();

        if (_picked >= 0)
        {
            _lingering += elapsed;
            if (_lingering >= Linger) Finish(_picked == _marked ? 1f : 0f);
            return;
        }

        _time += elapsed;
        _flashClock += elapsed;
        if (_flashClock >= FlashEvery) _flashClock -= FlashEvery;

        // The tags slide, and turn round at the ends of the lane.
        for (int i = 0; i < Tags; i++)
        {
            _tags[i].Update(gameTime);
            float x = _tags[i].Position.X;
            if (x < _laneStart.X && _speeds[i] < 0f || x > _laneStart.X + _laneLength && _speeds[i] > 0f)
            {
                _speeds[i] = -_speeds[i];
                _tags[i].Launch(new Vector2(Math.Clamp(x, _laneStart.X, _laneStart.X + _laneLength), _laneStart.Y),
                    new Vector2(_speeds[i], 0f), gravity: 0f);
            }
        }

        // Step the sight by where the tags are right now, so "left" always means the leftmost one.
        int[] order = OrderLeftToRight();
        int place = Array.IndexOf(order, _highlight);
        if (Input.SteppedLeft && place > 0) _highlight = order[place - 1];
        if (Input.SteppedRight && place < Tags - 1) _highlight = order[place + 1];

        int number = Input.NumberPressed;
        if (number >= 0)
        {
            _picked = order[number];
        }
        else if (Input.Clicked)
        {
            var point = new BoundingCircle(Input.MousePosition, 0f);
            for (int i = 0; i < Tags; i++)
            {
                if (CollisionHelper.Collides(point, _tags[i].Bounds)) _picked = i;
            }
        }
        else if (Input.Fired || _time >= Limit)
        {
            _picked = _highlight;
        }
    }

    /// <inheritdoc/>
    public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        bool flashing = _flashClock < FlashFor;
        for (int i = 0; i < Tags; i++)
        {
            bool showMark = _picked >= 0 ? i == _marked : (i == _marked && flashing);
            _tags[i].Tint = showMark ? new Color(255, 88, 64) : Color.White;
            _tags[i].Draw(gameTime, spriteBatch);
        }

        if (_sightSheet is null) return;

        // The sight sits over the highlighted tag, or the pick once made.
        int over = _picked >= 0 ? _picked : _highlight;
        var source = new Rectangle(0, 0, SightSize, SightSize);
        var origin = new Vector2(SightSize / 2f, SightSize / 2f);
        Vector2 at = _tags[over].Position;
        at = new Vector2(MathF.Round(at.X), MathF.Round(at.Y));
        Color tint = _picked < 0 ? Color.White : (_picked == _marked ? Palette.PickedTint : Palette.FiredTint);

        spriteBatch.Draw(_sightSheet, at, source, tint, 0f, origin, Scale, SpriteEffects.None, Layers.Sight);
    }

    /// <summary>The tags' indices, sorted by where they are on the table, left to right.</summary>
    private int[] OrderLeftToRight()
    {
        var order = new int[Tags];
        for (int i = 0; i < Tags; i++) order[i] = i;
        Array.Sort(order, (a, b) => _tags[a].Position.X.CompareTo(_tags[b].Position.X));
        return order;
    }
}
