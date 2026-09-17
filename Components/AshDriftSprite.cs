using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// One scrolling layer of the ash drifting behind the box.
/// </summary>
/// <remarks>
/// ash-drift.png is authored with a clear margin around its edges, so the tile wraps with no
/// visible seam and the layer can be drifted forever by just moving the tiling origin. Two of
/// these at different speeds and tints give the dead sky its parallax.
/// </remarks>
public class AshDriftSprite
{
    private readonly Vector2 _velocity;
    private readonly Color _tint;
    private readonly float _layerDepth;

    private Texture2D _texture;
    private Vector2 _offset;

    /// <summary>
    /// Creates an ash layer.
    /// </summary>
    /// <param name="velocity">Drift in pixels per second.</param>
    /// <param name="tint">Colour multiplied into the stars; scale it down to push the layer back.</param>
    /// <param name="layerDepth">Sort key for the batch. See <see cref="Layers"/>.</param>
    public AshDriftSprite(Vector2 velocity, Color tint, float layerDepth)
    {
        _velocity = velocity;
        _tint = tint;
        _layerDepth = layerDepth;
    }

    /// <summary>
    /// Loads the ash texture using the provided ContentManager.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _texture = content.Load<Texture2D>("ash-drift");
    }

    /// <summary>
    /// Drifts the layer, wrapping the offset so it never grows without bound.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    public void Update(GameTime gameTime)
    {
        _offset += _velocity * (float)gameTime.ElapsedGameTime.TotalSeconds;
        // %= is the modulo assignment operator that ensures the offset stays within the texture bounds.
        _offset.X %= _texture.Width;
        // Same as above.
        _offset.Y %= _texture.Height;
    }

    /// <summary>
    /// Tiles the layer across the viewport.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="viewport">The area to cover.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Viewport viewport)
    {
        // Start one tile behind the origin so a positive offset still covers the top-left.
        float startX = _offset.X > 0 ? _offset.X - _texture.Width : _offset.X;
        float startY = _offset.Y > 0 ? _offset.Y - _texture.Height : _offset.Y;

        for (float y = startY; y < viewport.Height; y += _texture.Height)
        {
            for (float x = startX; x < viewport.Width; x += _texture.Width)
            {
                spriteBatch.Draw(
                    _texture,
                    new Vector2(x, y),
                    null,
                    _tint,
                    0f,
                    Vector2.Zero,
                    1f,
                    SpriteEffects.None,
                    _layerDepth);
            }
        }
    }
}
