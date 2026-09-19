using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The box's jaws in the close-up: the plate over its mouth, drawing back into the rim before the hand goes in.
/// </summary>
/// <remarks>
/// Two halves of one plate, each cut shorter as the mouth opens. The mouth opens first and the
/// hand only starts once it is all the way open, so the arm never goes through concrete.
/// </remarks>
public class BoxLidSprite
{
    /// <summary>How long the mouth takes to open once a hand is offered, in seconds.</summary>
    private const float OpenSeconds = 0.8f;

    private Texture2D _lid;

    /// <summary>How far open the jaws are, from 0 shut to 1.</summary>
    public float Open { get; set; } = 1f;

    /// <summary>Whether the jaws are all the way back.</summary>
    public bool IsOpen => Open >= 1f;

    /// <summary>Loads box-lid.png.</summary>
    /// <param name="content">The content manager to load through.</param>
    public void LoadContent(ContentManager content) => _lid = content.Load<Texture2D>("box-lid");

    /// <summary>Opens the jaws a little further.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime) =>
        Open = MathF.Min(1f, Open + (float)gameTime.ElapsedGameTime.TotalSeconds / OpenSeconds);

    /// <summary>Draws the jaws over the opening, as far shut as they are.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="mouth">Where the opening is on screen. See BlackBoxSprite.Aperture.</param>
    /// <param name="scale">How far the box is blown up, so the plate matches its rim.</param>
    public void Draw(SpriteBatch spriteBatch, Rectangle mouth, float scale)
    {
        if (_lid is null || IsOpen) return;

        int half = _lid.Height / 2;
        int showing = (int)MathF.Round(half * (1f - Open));
        if (showing <= 0) return;

        var top = new Rectangle(0, 0, _lid.Width, showing);
        var bottom = new Rectangle(0, _lid.Height - showing, _lid.Width, showing);

        spriteBatch.Draw(_lid, new Vector2(mouth.X, mouth.Y), top, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Lid);
        spriteBatch.Draw(_lid, new Vector2(mouth.X, mouth.Bottom - showing * scale), bottom, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Lid);
    }
}
