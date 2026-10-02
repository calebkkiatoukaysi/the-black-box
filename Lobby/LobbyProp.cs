using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// One piece of furniture in the lobby: its picture from Content/Lobby, where it stands, and the
/// floor under it that you can't walk through.
/// </summary>
/// <remarks>
/// Props are placed by the middle of their bottom edge, same as the characters are by their feet,
/// so they sort against each other the same way. Some pictures have frames (the camera blinks, the
/// door opens), and Frame picks which one.
/// </remarks>
public class LobbyProp
{
    private readonly string _name;
    private readonly int _frames;
    private Texture2D _texture;

    /// <summary>The middle of the bottom edge, in world pixels.</summary>
    public Vector2 Foot { get; }

    /// <summary>Whether it's on the floor or on the wall.</summary>
    public PropPlacement Placement { get; }

    /// <summary>The floor it takes up in world pixels, or null for things on the wall.</summary>
    public BoundingRectangle? Footprint { get; }

    /// <summary>Which frame to draw.</summary>
    public int Frame { get; set; }

    /// <summary>The lobby's scale, same as the characters.</summary>
    private const float WalkerSpriteScale = Characters.WalkerSprite.Scale;

    /// <summary>Places a prop.</summary>
    /// <param name="name">Its picture, under Content/Lobby.</param>
    /// <param name="foot">The middle of its bottom edge, in art pixels.</param>
    /// <param name="placement">Floor or wall.</param>
    /// <param name="footprint">How much floor it takes up (width and depth) in art pixels, measured up from its foot. Zero for none.</param>
    /// <param name="frames">How many frames its picture has across.</param>
    public LobbyProp(string name, Vector2 foot, PropPlacement placement, Point footprint = default, int frames = 1)
    {
        _name = name;
        _frames = frames;
        Placement = placement;
        Foot = foot * WalkerSpriteScale;

        if (footprint != Point.Zero)
        {
            Footprint = new BoundingRectangle(
                Foot.X - footprint.X * WalkerSpriteScale / 2f,
                Foot.Y - footprint.Y * WalkerSpriteScale,
                footprint.X * WalkerSpriteScale,
                footprint.Y * WalkerSpriteScale);
        }
    }

    /// <summary>Loads the picture.</summary>
    /// <param name="content">The content manager to load with.</param>
    public void LoadContent(ContentManager content) => _texture = content.Load<Texture2D>("Lobby/" + _name);

    /// <summary>Draws the current frame.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in world space. Point-sampled.</param>
    /// <param name="layerDepth">Where it sorts.</param>
    public void Draw(SpriteBatch spriteBatch, float layerDepth)
    {
        if (_texture is null) return;

        int width = _texture.Width / _frames;
        var source = new Rectangle(Math.Clamp(Frame, 0, _frames - 1) * width, 0, width, _texture.Height);
        var origin = new Vector2(width / 2, _texture.Height);

        spriteBatch.Draw(_texture, new Vector2(MathF.Round(Foot.X), MathF.Round(Foot.Y)), source, Color.White,
            0f, origin, WalkerSpriteScale, SpriteEffects.None, layerDepth);
    }
}
