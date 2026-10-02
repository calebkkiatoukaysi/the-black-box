using System;
using Microsoft.Xna.Framework;

namespace TheBlackBox.Lobby;

/// <summary>
/// The lobby camera. It smoothly follows the player, never shows past the edge of the room, and
/// gives SpriteBatch.Begin the matrix for it.
/// </summary>
/// <remarks>
/// Same idea as Matrix.CreateTranslation in the parallax tutorial, except the offset follows the
/// player. I round it to whole pixels because the tiles were shimmering while the camera moved.
/// </remarks>
public class Camera2D
{
    /// <summary>How fast the camera catches up to its target. Higher is tighter.</summary>
    private const float Stiffness = 7f;

    /// <summary>How big the view is, in screen pixels.</summary>
    public Point ViewSize { get; }

    /// <summary>The room in world pixels. The view never goes outside it.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>The world point at the middle of the view.</summary>
    public Vector2 Centre { get; private set; }

    /// <summary>The part of the world on screen right now, in world pixels.</summary>
    public Rectangle View => new(
        (int)MathF.Round(Centre.X - ViewSize.X / 2f),
        (int)MathF.Round(Centre.Y - ViewSize.Y / 2f),
        ViewSize.X, ViewSize.Y);

    /// <summary>The world to screen matrix for SpriteBatch.Begin.</summary>
    public Matrix Transform => Matrix.CreateTranslation(-View.X, -View.Y, 0f);

    /// <summary>Creates a camera for a view of a given size.</summary>
    /// <param name="viewSize">The size of the window, in screen pixels.</param>
    public Camera2D(Point viewSize)
    {
        ViewSize = viewSize;
    }

    /// <summary>Jumps the camera straight to a point with no easing. Used on the first frame.</summary>
    /// <param name="target">The world point to look at.</param>
    public void SnapTo(Vector2 target) => Centre = Clamp(target);

    /// <summary>Eases the camera toward a point.</summary>
    /// <remarks>Exponential easing, so it moves the same at any frame rate.</remarks>
    /// <param name="target">The world point to follow.</param>
    /// <param name="gameTime">The frame's timing.</param>
    public void Follow(Vector2 target, GameTime gameTime)
    {
        float t = 1f - MathF.Exp(-Stiffness * (float)gameTime.ElapsedGameTime.TotalSeconds);
        Centre = Clamp(Vector2.Lerp(Centre, target, t));
    }

    /// <summary>Turns a world point into a screen point.</summary>
    /// <param name="world">The point in the room.</param>
    public Vector2 ToScreen(Vector2 world) => world - new Vector2(View.X, View.Y);

    /// <summary>Keeps the view inside the room.</summary>
    private Vector2 Clamp(Vector2 centre)
    {
        float halfW = ViewSize.X / 2f, halfH = ViewSize.Y / 2f;

        // If the room is smaller than the view on an axis, just centre it.
        float x = Bounds.Width <= ViewSize.X ? Bounds.Center.X : Math.Clamp(centre.X, Bounds.Left + halfW, Bounds.Right - halfW);
        float y = Bounds.Height <= ViewSize.Y ? Bounds.Center.Y : Math.Clamp(centre.Y, Bounds.Top + halfH, Bounds.Bottom - halfH);
        return new Vector2(x, y);
    }
}
