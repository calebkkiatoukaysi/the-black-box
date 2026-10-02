using System;
using Microsoft.Xna.Framework;

namespace TheBlackBox.Lobby;

/// <summary>
/// The lobby's camera: it eases after whatever it follows, never shows past the edge of the room,
/// and hands SpriteBatch.Begin the matrix that does it.
/// </summary>
/// <remarks>
/// Same idea as the parallax tutorial's Matrix.CreateTranslation, except the offset comes from
/// following the player instead of from a fixed scroll. The translation is rounded to whole
/// pixels; a fractional one made every tile edge shimmer as it moved.
/// </remarks>
public class Camera2D
{
    /// <summary>How quickly the camera closes the gap to its target, per second. Higher is tighter.</summary>
    private const float Stiffness = 7f;

    /// <summary>How big the view is, in screen pixels.</summary>
    public Point ViewSize { get; }

    /// <summary>The room, in world pixels. The view never leaves it.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>The world point at the middle of the view.</summary>
    public Vector2 Centre { get; private set; }

    /// <summary>The part of the world on screen right now, in world pixels.</summary>
    public Rectangle View => new(
        (int)MathF.Round(Centre.X - ViewSize.X / 2f),
        (int)MathF.Round(Centre.Y - ViewSize.Y / 2f),
        ViewSize.X, ViewSize.Y);

    /// <summary>World to screen, for SpriteBatch.Begin.</summary>
    public Matrix Transform => Matrix.CreateTranslation(-View.X, -View.Y, 0f);

    /// <summary>Creates a camera for a view of a given size.</summary>
    /// <param name="viewSize">The size of the window, in screen pixels.</param>
    public Camera2D(Point viewSize)
    {
        ViewSize = viewSize;
    }

    /// <summary>Puts the camera straight on a point, with no easing. For the first frame in a room.</summary>
    /// <param name="target">The world point to look at.</param>
    public void SnapTo(Vector2 target) => Centre = Clamp(target);

    /// <summary>Eases the camera toward a point.</summary>
    /// <remarks>Exponential, so it is the same speed at any frame rate: the gap shrinks by the same share each second.</remarks>
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

    /// <summary>Keeps the middle of the view far enough from the walls that the view stays inside the room.</summary>
    private Vector2 Clamp(Vector2 centre)
    {
        float halfW = ViewSize.X / 2f, halfH = ViewSize.Y / 2f;

        // A room smaller than the view on an axis is simply centred on it.
        float x = Bounds.Width <= ViewSize.X ? Bounds.Center.X : Math.Clamp(centre.X, Bounds.Left + halfW, Bounds.Right - halfW);
        float y = Bounds.Height <= ViewSize.Y ? Bounds.Center.Y : Math.Clamp(centre.Y, Bounds.Top + halfH, Bounds.Bottom - halfH);
        return new Vector2(x, y);
    }
}
