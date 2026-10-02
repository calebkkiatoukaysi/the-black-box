using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// Something somebody left on the lobby floor. Walk over it and it goes in your pockets, and it
/// comes with you to the table.
/// </summary>
/// <remarks>
/// It is the coin from the collision and audio tutorials, really: a bounding circle the player
/// runs into, and a sound when they do. The picture is the item's own frame off item-sheet.png,
/// the same one on the pocket plates.
/// </remarks>
public class LobbyItem
{
    /// <summary>How big the picture is drawn, how far it bobs, and how fast.</summary>
    private const float DrawScale = 2f;
    private const float BobHeight = 3f;
    private const float BobSpeed = 2.4f;

    /// <summary>How close the player's feet have to get to pick it up, in world pixels.</summary>
    private const float Reach = 30f;

    /// <summary>The shadow it hovers over.</summary>
    private static readonly Point ShadowSize = new(36, 10);
    private static readonly Color ShadowColor = new Color(0, 0, 0) * 0.5f;

    private float _clock;

    /// <summary>Which spot in the room it is on. The save remembers emptied spots by this.</summary>
    public string Spot { get; }

    /// <summary>What it is.</summary>
    public ItemId Item { get; }

    /// <summary>Where it lies, in world pixels.</summary>
    public Vector2 Position { get; }

    /// <summary>What the player has to touch to pick it up.</summary>
    public BoundingCircle Bounds => new(Position, Reach);

    /// <summary>Leaves an item on a spot.</summary>
    /// <param name="spot">The spot's name.</param>
    /// <param name="item">The item.</param>
    /// <param name="position">Where the spot is, in world pixels.</param>
    public LobbyItem(string spot, ItemId item, Vector2 position)
    {
        Spot = spot;
        Item = item;
        Position = position;

        // Each one bobs a little out of step with the others.
        _clock = (int)item * 0.7f;
    }

    /// <summary>Runs the bob.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime) => _clock += (float)gameTime.ElapsedGameTime.TotalSeconds;

    /// <summary>Draws the shadow and the item hovering over it.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in world space. Point-sampled.</param>
    /// <param name="shadow">The soft shadow texture.</param>
    /// <param name="items">The item pictures.</param>
    /// <param name="layerDepth">Where it sorts.</param>
    public void Draw(SpriteBatch spriteBatch, Texture2D shadow, ItemSprite items, float layerDepth)
    {
        var shade = new Rectangle((int)Position.X - ShadowSize.X / 2, (int)Position.Y - ShadowSize.Y / 2, ShadowSize.X, ShadowSize.Y);
        spriteBatch.Draw(shadow, shade, null, ShadowColor, 0f, Vector2.Zero, SpriteEffects.None, MathF.Min(1f, layerDepth + 0.0001f));

        float lift = MathF.Round(BobHeight + BobHeight * MathF.Sin(_clock * BobSpeed));
        float size = ItemSprite.FrameSize * DrawScale;
        var topLeft = new Vector2(MathF.Round(Position.X - size / 2f), MathF.Round(Position.Y - size - lift));
        items.Draw(spriteBatch, Item, topLeft, DrawScale, layerDepth);
    }
}
