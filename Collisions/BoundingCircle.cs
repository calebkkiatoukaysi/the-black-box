using Microsoft.Xna.Framework;

namespace TheBlackBox.Collisions;

/// <summary>
/// A bounding circle, done the way the collision tutorial does it.
/// </summary>
/// <remarks>The spinning token uses this since a spinning tag is round on average.</remarks>
public struct BoundingCircle
{
    /// <summary>The middle of the circle, in screen pixels.</summary>
    public Vector2 Center;

    /// <summary>How far from the middle it reaches, in screen pixels.</summary>
    public float Radius;

    /// <summary>Makes a circle.</summary>
    /// <param name="center">The middle, in screen pixels.</param>
    /// <param name="radius">The radius, in screen pixels.</param>
    public BoundingCircle(Vector2 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>Whether this circle overlaps another.</summary>
    /// <param name="other">The other circle.</param>
    /// <returns>True if they touch or overlap.</returns>
    public bool CollidesWith(BoundingCircle other) => CollisionHelper.Collides(this, other);

    /// <summary>Whether this circle overlaps a rectangle.</summary>
    /// <param name="other">The rectangle.</param>
    /// <returns>True if they touch or overlap.</returns>
    public bool CollidesWith(BoundingRectangle other) => CollisionHelper.Collides(this, other);
}
