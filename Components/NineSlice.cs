using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Draws a fixed-size frame of pixel art stretched to any rectangle, in nine pieces.
/// </summary>
/// <remarks>
/// <para>
/// The four corners are drawn at their authored size, the four edges are stretched along the
/// one axis they run down, and only the middle is stretched both ways. Everything drawn this
/// way is authored to suit it -- nothing inside an edge varies along the direction it gets
/// stretched -- so none of it smears no matter how far it is pulled.
/// </para>
/// <para>
/// Both <see cref="ButtonSprite"/> and <see cref="SaveSlotMenu"/> are built out of plates that
/// have to size themselves to their contents, so the slicing lives here rather than in either
/// of them.
/// </para>
/// </remarks>
public static class NineSlice
{
    /// <summary>
    /// Draws one frame of <paramref name="texture"/> stretched to cover <paramref name="destination"/>.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="texture">The sheet the frame lives in.</param>
    /// <param name="destination">The area to cover, in screen pixels.</param>
    /// <param name="origin">Top-left of the frame inside the sheet.</param>
    /// <param name="frameSize">Width and height of the frame, in texture pixels.</param>
    /// <param name="cornerSize">Size of the fixed corner, in texture pixels.</param>
    /// <param name="scale">How far the art is blown up, so corners keep their authored size.</param>
    /// <param name="tint">Colour multiplied into the frame.</param>
    /// <param name="layerDepth">Sort key for the batch. See <see cref="Layers"/>.</param>
    public static void Draw(SpriteBatch spriteBatch, Texture2D texture, Rectangle destination,
        Point origin, int frameSize, int cornerSize, float scale, Color tint, float layerDepth)
    {
        int corner = (int)MathF.Round(cornerSize * scale);

        // Never let the two corners on an axis overrun the plate, or they would overlap and
        // double-draw whatever they carry where they met.
        int cornerX = Math.Min(corner, destination.Width / 2);
        int cornerY = Math.Min(corner, destination.Height / 2);

        int[] sourceX = { 0, cornerSize, frameSize - cornerSize, frameSize };
        int[] sourceY = { 0, cornerSize, frameSize - cornerSize, frameSize };
        int[] destX = { destination.Left, destination.Left + cornerX, destination.Right - cornerX, destination.Right };
        int[] destY = { destination.Top, destination.Top + cornerY, destination.Bottom - cornerY, destination.Bottom };

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                var piece = new Rectangle(
                    destX[column],
                    destY[row],
                    destX[column + 1] - destX[column],
                    destY[row + 1] - destY[row]);

                if (piece.Width <= 0 || piece.Height <= 0) continue;

                var source = new Rectangle(
                    origin.X + sourceX[column],
                    origin.Y + sourceY[row],
                    sourceX[column + 1] - sourceX[column],
                    sourceY[row + 1] - sourceY[row]);

                spriteBatch.Draw(texture, piece, source, tint, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
            }
        }
    }
}
