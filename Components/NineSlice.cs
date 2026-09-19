using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// Draws a fixed-size frame of pixel art stretched to any rectangle, in nine pieces.
/// </summary>
/// <remarks>
/// Corners stay their drawn size, edges stretch one way, the middle stretches both. Shared by
/// <see cref="ButtonSprite"/> and the panels, which is why it's its own class.
/// </remarks>
public static class NineSlice
{
    /// <summary>Draws one frame of the texture stretched to cover the destination.</summary>
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

        // Don't let the two corners on an axis overlap on a small plate.
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
