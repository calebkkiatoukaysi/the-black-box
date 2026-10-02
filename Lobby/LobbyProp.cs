using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// One piece of furniture in the lobby: a picture off Content/Lobby, where it stands, and the
/// patch of floor under it that nobody can walk through.
/// </summary>
/// <remarks>
/// A prop is placed by the middle of its bottom edge, like the figures are by their feet, so the
/// two sort against each other by the same number. Some pictures are strips of frames (the
/// camera blinks, the door opens); <see cref="Frame"/> picks one.
/// </remarks>
public class LobbyProp
{
    private readonly string _name;
    private readonly int _frames;
    private Texture2D _texture;

    /// <summary>The middle of the bottom edge, in world pixels.</summary>
    public Vector2 Foot { get; }

    /// <summary>Whether it stands on the floor, hangs on the wall, or hangs from the ceiling.</summary>
    public PropPlacement Placement { get; }

    /// <summary>The floor it takes up, in world pixels, or null for something nobody can walk into anyway.</summary>
    public BoundingRectangle? Footprint { get; }

    /// <summary>Which frame of the strip to draw.</summary>
    public int Frame { get; set; }

    /// <summary>The lobby's scale, the same as the figures'.</summary>
    private const float WalkerSpriteScale = Characters.WalkerSprite.Scale;

    /// <summary>Places a prop.</summary>
    /// <param name="name">Its picture, under Content/Lobby.</param>
    /// <param name="foot">The middle of its bottom edge, in art pixels.</param>
    /// <param name="placement">Floor, wall or ceiling.</param>
    /// <param name="footprint">How much floor it takes up, wide and deep, in art pixels, measured up from its foot. Zero for none.</param>
    /// <param name="frames">How many frames across its picture holds.</param>
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

    /// <summary>Draws the current frame standing on its foot.</summary>
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
