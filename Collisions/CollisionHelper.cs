using Microsoft.Xna.Framework;

namespace TheBlackBox.Collisions;

/// <summary>
/// The collision tests, in one place so both shapes agree. Same layout as the tutorial.
/// </summary>
public static class CollisionHelper
{
    /// <summary>Whether two circles overlap. Squared distance, so no square root.</summary>
    /// <param name="a">One circle.</param>
    /// <param name="b">The other.</param>
    /// <returns>True if they touch or overlap.</returns>
    public static bool Collides(BoundingCircle a, BoundingCircle b)
    {
        float reach = a.Radius + b.Radius;
        return Vector2.DistanceSquared(a.Center, b.Center) <= reach * reach;
    }

    /// <summary>Whether two rectangles overlap. They miss if one is entirely to one side of the other.</summary>
    /// <param name="a">One rectangle.</param>
    /// <param name="b">The other.</param>
    /// <returns>True if they touch or overlap.</returns>
    public static bool Collides(BoundingRectangle a, BoundingRectangle b)
    {
        return !(a.Right < b.Left
            || a.Left > b.Right
            || a.Bottom < b.Top
            || a.Top > b.Bottom);
    }

    /// <summary>Whether a circle overlaps a rectangle.</summary>
    /// <remarks>
    /// Clamp the circle's centre into the rectangle to get the nearest point, then check the
    /// distance. This handles corners, which an edges-only test would miss.
    /// </remarks>
    /// <param name="c">The circle.</param>
    /// <param name="r">The rectangle.</param>
    /// <returns>True if they touch or overlap.</returns>
    public static bool Collides(BoundingCircle c, BoundingRectangle r)
    {
        float nearestX = MathHelper.Clamp(c.Center.X, r.Left, r.Right);
        float nearestY = MathHelper.Clamp(c.Center.Y, r.Top, r.Bottom);
        var nearest = new Vector2(nearestX, nearestY);

        return Vector2.DistanceSquared(c.Center, nearest) <= c.Radius * c.Radius;
    }
}
