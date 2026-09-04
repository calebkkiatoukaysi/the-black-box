using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// A speck of ash spiralling into the mouth of the box.
/// </summary>
/// <remarks>
/// The motes exist to show what the box does rather than say it: everything loose nearby
/// ends up going in, and nothing ever comes back out. Each one travels in polar coordinates
/// around the aperture, accelerating as it closes, and is recycled to the outside once the
/// void has taken it.
/// </remarks>
public class MoteSprite
{
    /// <summary>Where a mote enters the field, in pixels from the mouth.</summary>
    private const float SpawnRadius = 350f;

    /// <summary>Inside this radius the void has swallowed the mote and it is recycled.</summary>
    private const float ConsumeRadius = 24f;

    /// <summary>The field is slightly wider than it is tall, matching the box.</summary>
    private const float VerticalSquash = 0.85f;

    private const float InwardSpeed = 33f;

    /// <summary>Extra pull that builds as a mote closes on the mouth. The void does not let go.</summary>
    private const float PullGain = 43f;

    private readonly Random _random;

    private Texture2D _texture;
    private Vector2 _position;
    private float _angle;
    private float _radius;
    private float _spin;
    private float _size;
    private float _alpha;
    private Color _tint;

    /// <summary>The mouth of the box, which everything here is falling into.</summary>
    public Vector2 Center;

    /// <summary>
    /// Creates a mote already somewhere along its way in, so the field starts full.
    /// </summary>
    /// <param name="random">Shared source of randomness, owned by the game.</param>
    public MoteSprite(Random random)
    {
        _random = random;
        Respawn();
        _radius = ConsumeRadius + (float)random.NextDouble() * (SpawnRadius - ConsumeRadius);
    }

    /// <summary>
    /// Loads the mote texture using the provided ContentManager.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _texture = content.Load<Texture2D>("mote");
    }

    /// <summary>
    /// Draws the mote further in, and recycles it once it is consumed.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    public void Update(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float safeRadius = MathF.Max(_radius, ConsumeRadius);

        // Both the inward pull and the swirl strengthen as the mote closes, which is what
        // makes the last stretch of the journey look like being taken rather than drifting.
        _radius -= (InwardSpeed + PullGain * (SpawnRadius / safeRadius - 1f)) * elapsed;
        _angle += _spin * MathF.Sqrt(SpawnRadius / safeRadius) * elapsed;

        if (_radius <= ConsumeRadius)
        {
            Respawn();
            return;
        }

        _position = Center + new Vector2(
            MathF.Cos(_angle) * _radius,
            MathF.Sin(_angle) * _radius * VerticalSquash);

        // Fade in out of the haze, then out again as the void takes it.
        float travel = (_radius - ConsumeRadius) / (SpawnRadius - ConsumeRadius);
        _alpha = MathHelper.Clamp(MathF.Min((1f - travel) * 4f, travel * 5f), 0f, 1f);
    }

    /// <summary>
    /// Draws the mote, shrinking as the void closes over it.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (_alpha <= 0f) return;

        float travel = (_radius - ConsumeRadius) / (SpawnRadius - ConsumeRadius);

        spriteBatch.Draw(
            _texture,
            _position,
            null,
            _tint * (_alpha * 0.85f),
            0f,
            new Vector2(_texture.Width / 2f, _texture.Height / 2f),
            _size * (0.45f + 0.55f * travel),
            SpriteEffects.None,
            Layers.Mote);
    }

    /// <summary>Throws the mote back out to the edge of the field with fresh properties.</summary>
    private void Respawn()
    {
        _radius = SpawnRadius * (0.92f + (float)_random.NextDouble() * 0.16f);
        _angle = (float)_random.NextDouble() * MathHelper.TwoPi;
        _size = 1.0f + (float)_random.NextDouble() * 1.6f;

        // Half the field drifts one way around the mouth and half the other.
        _spin = (0.25f + (float)_random.NextDouble() * 0.35f) * (_random.NextDouble() < 0.5 ? -1f : 1f);

        // Mostly cold grit, with the occasional ember still burning on its way down.
        _tint = _random.NextDouble() < 0.12
            ? new Color(228, 120, 62)
            : new Color(196, 192, 190);
    }
}
