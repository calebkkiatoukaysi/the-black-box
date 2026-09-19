using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// A speck of ash spiralling into the mouth of the box.
/// </summary>
/// <remarks>
/// Each mote moves in polar coordinates around the opening, speeding up as it gets close, and is
/// recycled to the outside once the void takes it. Nothing ever comes back out.
/// </remarks>
public class AshSprite
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

    /// <summary>How quickly a mote fades in at the edge and out at the mouth, and how bright it is between.</summary>
    private const float FadeInRate = 4f;
    private const float FadeOutRate = 5f;
    private const float MoteOpacity = 0.85f;

    /// <summary>How small a mote has shrunk to by the time the void takes it.</summary>
    private const float MinShrink = 0.45f;

    // What a fresh mote gets: a little jitter on where it enters, a size, a spin, and once
    // in a while it is still burning.
    private const float SpawnJitter = 0.08f;
    private const float SizeMin = 1.0f;
    private const float SizeSpread = 1.6f;
    private const float SpinMin = 0.25f;
    private const float SpinSpread = 0.35f;
    private const double EmberChance = 0.12;
    private static readonly Color EmberTint = new(228, 120, 62);
    private static readonly Color GritTint = new(196, 192, 190);

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

    /// <summary>Creates a mote already somewhere along its way in, so the field starts full.</summary>
    public AshSprite()
    {
        Respawn();
        _radius = ConsumeRadius + (float)Random.Shared.NextDouble() * (SpawnRadius - ConsumeRadius);
    }

    /// <summary>Loads the mote texture.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _texture = content.Load<Texture2D>("mote");
    }

    /// <summary>Moves the mote further in, and recycles it once it is consumed.</summary>
    /// <param name="gameTime">The GameTime.</param>
    public void Update(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float safeRadius = MathF.Max(_radius, ConsumeRadius);

        // Both the pull and the swirl get stronger near the mouth, so the last stretch looks like being taken.
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
        _alpha = MathHelper.Clamp(MathF.Min((1f - travel) * FadeInRate, travel * FadeOutRate), 0f, 1f);
    }

    /// <summary>Draws the mote, shrinking as the void closes over it.</summary>
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
            _tint * (_alpha * MoteOpacity),
            0f,
            new Vector2(_texture.Width / 2f, _texture.Height / 2f),
            _size * (MinShrink + (1f - MinShrink) * travel),
            SpriteEffects.None,
            Layers.Mote);
    }

    /// <summary>Throws the mote back out to the edge of the field with fresh properties.</summary>
    private void Respawn()
    {
        _radius = SpawnRadius * (1f - SpawnJitter + (float)Random.Shared.NextDouble() * 2f * SpawnJitter);
        _angle = (float)Random.Shared.NextDouble() * MathHelper.TwoPi;
        _size = SizeMin + (float)Random.Shared.NextDouble() * SizeSpread;

        // Half the field drifts one way around the mouth and half the other.
        _spin = (SpinMin + (float)Random.Shared.NextDouble() * SpinSpread) * (Random.Shared.NextDouble() < 0.5 ? -1f : 1f);

        // Mostly cold grit, with the occasional ember still burning on its way down.
        _tint = Random.Shared.NextDouble() < EmberChance ? EmberTint : GritTint;
    }
}
