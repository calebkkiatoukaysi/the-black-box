using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox;

/// <summary>
/// What the box pays with: a steel tag, spinning, skidding across the table toward you.
/// </summary>
/// <remarks>
/// The box throws the item instead of handing it over, and the player has to get a hand under it
/// before it slides off the near edge. Two things at once: an eight-frame spin on its own clock,
/// and a body with a position, a velocity and gravity. The collision shape is a circle, since a spinning tag is round on average.
/// </remarks>
public class TokenSprite
{
    /// <summary>Width and height of one frame in token-sheet.png.</summary>
    private const int FrameSize = 24;

    /// <summary>Frames in the sheet, one half-turn of the tag.</summary>
    private const int Frames = 8;

    /// <summary>How far the art is blown up. The same whole number as the table.</summary>
    private const float Scale = 4f;

    /// <summary>How fast the tag turns, in frames a second.</summary>
    private const float SpinRate = 18f;

    /// <summary>How fast the slide picks up, in screen pixels a second each second.</summary>
    /// <remarks>Low enough that the keys can get across the table in time, high enough that you can't just watch.</remarks>
    private const float Gravity = 280f;

    /// <summary>The radius of the collision circle, in sheet pixels.</summary>
    private const float Radius = 8f;

    private Texture2D _sheet;
    private Vector2 _position;
    private Vector2 _velocity;
    private float _gravity = Gravity;
    private float _frameTimer;
    private int _frame;

    /// <summary>A colour multiplied into the tag when it is drawn. White is the tag as it is.</summary>
    public Color Tint { get; set; } = Color.White;

    /// <summary>Whether the tag is on the table at all.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Where the middle of the tag is, in screen pixels.</summary>
    public Vector2 Position => _position;

    /// <summary>The circle the tag occupies, for the catch and for the edge.</summary>
    public BoundingCircle Bounds => new(_position, Radius * Scale);

    /// <summary>Loads token-sheet.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _sheet = content.Load<Texture2D>("token-sheet");

    /// <summary>Throws the tag out of the box, or sets it sliding.</summary>
    /// <param name="from">Where it starts, in screen pixels: the mouth of the box.</param>
    /// <param name="velocity">How fast it leaves, in screen pixels a second. Y is down the table.</param>
    /// <param name="gravity">How fast the slide picks up. The read check's tags use none.</param>
    public void Launch(Vector2 from, Vector2 velocity, float gravity = Gravity)
    {
        _position = from;
        _velocity = velocity;
        _gravity = gravity;
        _frame = 0;
        _frameTimer = 0f;
        IsVisible = true;
    }

    /// <summary>Puts the tag somewhere without changing how it is moving. For the proof shots.</summary>
    /// <param name="position">Where the middle of it goes, in screen pixels.</param>
    public void Place(Vector2 position) => _position = position;

    /// <summary>Moves the tag and turns it.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsVisible) return;

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        _velocity.Y += _gravity * elapsed;
        _position += _velocity * elapsed;

        // The spin is on its own clock. Stepping frames means a long frame skips forward, never backward.
        _frameTimer += elapsed;
        while (_frameTimer >= 1f / SpinRate)
        {
            _frameTimer -= 1f / SpinRate;
            _frame = (_frame + 1) % Frames;
        }
    }

    /// <summary>Draws the tag, if it is on the table.</summary>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (!IsVisible || _sheet is null) return;

        var source = new Rectangle(_frame * FrameSize, 0, FrameSize, FrameSize);
        var origin = new Vector2(FrameSize / 2f, FrameSize / 2f);

        spriteBatch.Draw(_sheet, _position, source, Tint, 0f,
            origin, Scale, SpriteEffects.None, Layers.Token);
    }
}
