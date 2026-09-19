using Microsoft.Xna.Framework;

namespace TheBlackBox.Collisions;

/// <summary>
/// An axis-aligned bounding rectangle, done the way the collision tutorial does it.
/// </summary>
/// <remarks>The catching hand's palm and the edge of the table are both one of these.</remarks>
public struct BoundingRectangle
{
    /// <summary>The left edge, in screen pixels.</summary>
    public float X;

    /// <summary>The top edge, in screen pixels.</summary>
    public float Y;

    /// <summary>How wide it is, in screen pixels.</summary>
    public float Width;

    /// <summary>How tall it is, in screen pixels.</summary>
    public float Height;

    /// <summary>The left edge.</summary>
    public float Left => X;

    /// <summary>The right edge.</summary>
    public float Right => X + Width;

    /// <summary>The top edge.</summary>
    public float Top => Y;

    /// <summary>The bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>Makes a rectangle from its top-left corner and its size.</summary>
    /// <param name="x">The left edge, in screen pixels.</param>
    /// <param name="y">The top edge, in screen pixels.</param>
    /// <param name="width">How wide, in screen pixels.</param>
    /// <param name="height">How tall, in screen pixels.</param>
    public BoundingRectangle(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Makes a rectangle from its top-left corner and its size.</summary>
    /// <param name="position">The top-left corner, in screen pixels.</param>
    /// <param name="width">How wide, in screen pixels.</param>
    /// <param name="height">How tall, in screen pixels.</param>
    public BoundingRectangle(Vector2 position, float width, float height)
        : this(position.X, position.Y, width, height)
    {
    }

    /// <summary>Whether this rectangle overlaps another.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>True if they touch or overlap.</returns>
    public bool CollidesWith(BoundingRectangle other) => CollisionHelper.Collides(this, other);

    /// <summary>Whether this rectangle overlaps a circle.</summary>
    /// <param name="other">The circle.</param>
    /// <returns>True if they touch or overlap.</returns>
    public bool CollidesWith(BoundingCircle other) => CollisionHelper.Collides(other, this);
}
