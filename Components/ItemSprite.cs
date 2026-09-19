using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// A picture of an item, off item-sheet.png.
/// </summary>
/// <remarks>
/// One frame per ItemId in enum order, so the frame is the enum value and there is nothing to
/// look up. Shown at 2x on a pocket plate and at 3x beside the dealt item on the wall.
/// </remarks>
public class ItemSprite
{
    /// <summary>Width and height of one frame in item-sheet.png.</summary>
    public const int FrameSize = 24;

    /// <summary>The sheet, for a button that draws its own icon.</summary>
    public Texture2D Sheet { get; private set; }

    /// <summary>Loads item-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => Sheet = content.Load<Texture2D>("item-sheet");

    /// <summary>Where an item's frame is in the sheet.</summary>
    /// <param name="item">The item.</param>
    public static Rectangle SourceOf(ItemId item) => new((int)item * FrameSize, 0, FrameSize, FrameSize);

    /// <summary>Draws one item from a top-left corner.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    /// <param name="item">The item to draw.</param>
    /// <param name="topLeft">Where the frame's top-left corner goes, in screen pixels.</param>
    /// <param name="scale">How far the art is blown up. Whole numbers keep it crisp.</param>
    /// <param name="layer">The sort depth to draw at.</param>
    public void Draw(SpriteBatch spriteBatch, ItemId item, Vector2 topLeft, float scale, float layer)
    {
        if (Sheet is null) return;

        spriteBatch.Draw(Sheet, topLeft, SourceOf(item), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, layer);
    }
}
