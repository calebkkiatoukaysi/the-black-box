using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox;

/// <summary>
/// Represents the clickable button, commonly used for a lot of interactions later in game.
/// </summary>
/// <remarks>
/// <para>
/// A button is a plate of the same dead concrete the box is made of, with a groove cut around
/// its face that light comes out of. It is not lit because a button ought to glow -- it is lit
/// because there is something behind every plate in this game, and the closer the cursor gets
/// the more of it shows. That is the whole reason the art is animated rather than hover-tinted.
/// </para>
/// <para>
/// The art lives in one sheet, <c>button.png</c>, split into two bands over the same grid. The
/// top band is the plate, drawn in neutral grey; the bottom band is a white mask of the light,
/// drawn over it in <see cref="Accent"/>. Keeping the light on its own band is what lets one
/// texture serve a red button, an amber one and a bone-white one without a second asset.
/// </para>
/// <para>
/// Every frame is nine-sliced, so a button is whatever size its label needs while the corners,
/// bolts and groove stay at their authored scale instead of stretching with it.
/// </para>
/// </remarks>
public class ButtonSprite
{
    /// <summary>Width and height of one frame in button.png.</summary>
    private const int FrameSize = 24;

    /// <summary>
    /// Size of the fixed corner in each frame. Everything inside it stretches, everything in
    /// it does not. This is the authored inset of the groove and the bolts, so it cannot be
    /// changed here alone -- <c>BUTTON_CORNER</c> in tools/generate_assets.py has to match.
    /// </summary>
    private const int CornerSize = 8;

    /// <summary>Rows in the sheet, which is also the number of animation states.</summary>
    private const int SheetRows = 4;

    /// <summary>Distance down the sheet to the light band, which mirrors the plate band.</summary>
    private const int AccentBandOffset = FrameSize * SheetRows;

    // Rows of the sheet, in the order they are authored.
    private const int RowIdle = 0;
    private const int RowHover = 1;
    private const int RowPress = 2;
    private const int RowDisabled = 3;

    private const int IdleFrames = 4;
    private const int HoverFrames = 6;
    private const int PressFrames = 4;

    /// <summary>The art is small and blown up by a whole number to stay crisp, like the box.</summary>
    public const float DrawScale = 3f;

    /// <summary>Seconds per frame of the idle smoulder. Slow, so it never reads as a prompt.</summary>
    private const double IdleFrameTime = 0.17;

    /// <summary>How fast the plate kindles under the cursor, in hover-animation per second.</summary>
    private const float KindleRate = 6.5f;

    /// <summary>How fast it goes out again. Slower than it lit, so the light lingers.</summary>
    private const float CoolRate = 4.5f;

    /// <summary>How fast the plate sinks while held, in press-animation per second.</summary>
    private const float SinkRate = 14f;

    /// <summary>How fast it comes back out on release. Quick, or the button feels sticky.</summary>
    private const float RiseRate = 9f;

    /// <summary>How far the plate travels into the wall when fully pressed, in screen pixels.</summary>
    private const float SinkDistance = 3f;

    /// <summary>Padding around the label when a button is left to size itself.</summary>
    private static readonly Point LabelPadding = new(54, 26);

    /// <summary>Gap between the label and the detail line under it, in screen pixels.</summary>
    private const float SublabelGap = 4f;

    /// <summary>The light the box gives off. For anything that commits the player to something.</summary>
    public static readonly Color EmberRed = new(226, 62, 44);

    /// <summary>Sickly amber, for the offers the box makes.</summary>
    public static readonly Color Amber = new(232, 150, 58);

    /// <summary>Cold bone-white, for the choices the box has no stake in.</summary>
    public static readonly Color BoneWhite = new(226, 214, 206);

    private Texture2D _texture;
    private SpriteFont _font;
    private SpriteFont _detailFont;
    private SpriteFont _smallFont;

    /// <summary>How far through the hover animation the plate is, 0 cold to 1 fully kindled.</summary>
    private float _kindle;

    /// <summary>How far through the press animation the plate is, 0 out to 1 fully sunk.</summary>
    private float _sink;

    private double _idleTimer;
    private bool _hovered;

    /// <summary>Whether the press that is currently down started on this button.</summary>
    private bool _armed;

    private bool _hasMouseSample;
    private ButtonState _lastMouseButton = ButtonState.Released;

    /// <summary>The line drawn across the plate.</summary>
    public string Label;

    /// <summary>
    /// An optional second, smaller line under the label. A save slot needs to say what is in
    /// it as well as which slot it is, and two plates stacked to say that would read as two
    /// separate choices rather than one.
    /// </summary>
    public string Sublabel;

    /// <summary>
    /// Whether the label is set in the small font instead of the UI font. For plates that
    /// have to say a whole item name in the width of a pocket.
    /// </summary>
    public bool Small;

    /// <summary>Centre of the button in screen space.</summary>
    public Vector2 Center;

    /// <summary>
    /// Size of the button in screen pixels. Left at zero, the button measures its own label
    /// in <see cref="LoadContent"/> and sizes itself to fit.
    /// </summary>
    public Point Size;

    /// <summary>Colour of the light coming out of the groove.</summary>
    public Color Accent = EmberRed;

    /// <summary>
    /// Tint multiplied into the concrete. White leaves the plate as authored; warming or
    /// cooling it is a quieter way to separate two buttons than changing the light is.
    /// </summary>
    public Color PlateTint = Color.White;

    /// <summary>Whether the button responds to the cursor at all.</summary>
    public bool Enabled = true;

    /// <summary>Raised the moment a press that started on this button is released on it.</summary>
    public event Action Clicked;

    /// <summary>Whether the cursor is over the button. Always false while it is disabled.</summary>
    public bool Hovered => _hovered;

    /// <summary>Whether the button is being held down right now.</summary>
    public bool Held => _armed && _hovered;

    /// <summary>
    /// True for the single frame a click completes on this button. <see cref="Clicked"/> is
    /// usually the nicer way to read it, but a caller already polling in <c>Update</c> can
    /// use this instead.
    /// </summary>
    public bool WasClicked { get; private set; }

    /// <summary>The area the button covers, in screen pixels.</summary>
    public Rectangle Bounds => new(
        (int)MathF.Round(Center.X - Size.X / 2f),
        (int)MathF.Round(Center.Y - Size.Y / 2f),
        Size.X,
        Size.Y);

    /// <summary>
    /// Creates a button. Leave <paramref name="size"/> unset to have it fit its own label.
    /// </summary>
    /// <param name="label">The line drawn across the plate.</param>
    /// <param name="center">Centre of the button in screen space.</param>
    /// <param name="accent">Colour of the light in the groove.</param>
    /// <param name="size">Size in screen pixels, or default to size to the label.</param>
    public ButtonSprite(string label, Vector2 center, Color accent, Point size = default)
    {
        Label = label;
        Center = center;
        Accent = accent;
        Size = size;
    }

    /// <summary>
    /// Loads the button sheet and the label font using the provided ContentManager.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _texture = content.Load<Texture2D>("button");
        _font = content.Load<SpriteFont>("spectral-ui");
        _detailFont = content.Load<SpriteFont>("spectral-detail");
        _smallFont = content.Load<SpriteFont>("spectral-small");

        if (Size == Point.Zero)
        {
            Vector2 measured = MeasureText();
            Size = new Point(
                (int)MathF.Round(measured.X) + LabelPadding.X,
                (int)MathF.Round(measured.Y) + LabelPadding.Y);
        }

        // Below two corners the nine-slice would have to crop the authored art, so a button
        // is never allowed to be smaller than the frame it is built from.
        int minimum = (int)MathF.Round(CornerSize * 2 * DrawScale);
        Size = new Point(Math.Max(Size.X, minimum), Math.Max(Size.Y, minimum));
    }

    /// <summary>
    /// Puts the button back in its cold, unpressed state and forgets the cursor.
    /// </summary>
    /// <remarks>
    /// A button stops being updated the moment something else takes over the screen, which
    /// leaves whatever it was mid-animation frozen on it -- so a START button covered by the
    /// slot form is still glowing when the form closes. Dropping the mouse sample as well
    /// matters more: the button re-samples on its next <see cref="Update"/> instead of
    /// comparing against a press from before it was hidden, so a click made somewhere else
    /// cannot complete here the instant the button comes back.
    /// </remarks>
    public void Reset()
    {
        _kindle = 0f;
        _sink = 0f;
        _hovered = false;
        _armed = false;
        _hasMouseSample = false;
        WasClicked = false;
    }

    /// <summary>
    /// Advances the animation and works out whether the button was clicked this frame.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    public void Update(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        WasClicked = false;

        MouseState mouse = Mouse.GetState();

        // The first frame has nothing to compare against, so a button sitting under a held
        // cursor at startup cannot fire off a click the player never made.
        if (!_hasMouseSample)
        {
            _hasMouseSample = true;
            _lastMouseButton = mouse.LeftButton;
        }

        _hovered = Enabled && IsHovered(mouse.Position);

        if (!Enabled)
        {
            // Drop everything in progress, so a button disabled mid-press does not come back
            // still holding a click that is no longer owed to anyone.
            _armed = false;
            _kindle = 0f;
            _sink = 0f;
        }
        else
        {
            bool pressedNow = mouse.LeftButton == ButtonState.Pressed
                && _lastMouseButton == ButtonState.Released;
            bool releasedNow = mouse.LeftButton == ButtonState.Released
                && _lastMouseButton == ButtonState.Pressed;

            // A click has to start and finish on the same plate. Pressing here and dragging
            // off cancels it, which is what every other button the player has used does.
            if (pressedNow && _hovered) _armed = true;

            if (releasedNow)
            {
                if (_armed && _hovered)
                {
                    WasClicked = true;
                    Clicked?.Invoke();
                }
                _armed = false;
            }

            _kindle = Approach(_kindle, _hovered ? 1f : 0f, (_hovered ? KindleRate : CoolRate) * elapsed);
            _sink = Approach(_sink, Held ? 1f : 0f, (Held ? SinkRate : RiseRate) * elapsed);
        }

        _lastMouseButton = mouse.LeftButton;

        _idleTimer += gameTime.ElapsedGameTime.TotalSeconds;
        if (_idleTimer >= IdleFrameTime * IdleFrames) _idleTimer -= IdleFrameTime * IdleFrames;
    }

    /// <summary>
    /// Draws the plate, the light in its groove, and the label across it.
    /// </summary>
    /// <param name="gameTime">The game time.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Point frame = CurrentFrame();
        Rectangle plate = Bounds;

        // A held plate moves with its light, so the whole button reads as going into the wall
        // rather than the art swapping underneath a label that stayed put.
        if (_sink > 0f)
        {
            int travel = (int)MathF.Round(_sink * SinkDistance);
            plate.Offset(travel, travel);
        }

        DrawPlate(spriteBatch, plate, frame, PlateTint, Layers.ButtonPlate);
        DrawPlate(spriteBatch, plate, new Point(frame.X, frame.Y + AccentBandOffset), Accent, Layers.ButtonAccent);

        DrawLabel(spriteBatch, plate);
    }

    /// <summary>
    /// Determines whether the button is currently being hovered over based on the given mouse position.
    /// </summary>
    /// <param name="mousePosition">The current position of the mouse cursor.</param>
    /// <returns>True if the button is being hovered over, otherwise false.</returns>
    public bool IsHovered(Point mousePosition)
    {
        return Bounds.Contains(mousePosition);
    }

    /// <summary>
    /// Picks the frame to draw, as its top-left corner in the sheet's plate band.
    /// </summary>
    /// <remarks>
    /// The hover row is a ramp rather than a loop, so running <see cref="_kindle"/> back down
    /// plays it in reverse and the plate goes out the same way it lit. That is why the sheet
    /// carries no separate un-hover animation.
    /// </remarks>
    private Point CurrentFrame()
    {
        if (!Enabled) return new Point(0, RowDisabled * FrameSize);

        if (_sink > 0f)
            return new Point(Index(_sink, PressFrames) * FrameSize, RowPress * FrameSize);

        if (_kindle > 0f)
            return new Point(Index(_kindle, HoverFrames) * FrameSize, RowHover * FrameSize);

        int idle = (int)(_idleTimer / IdleFrameTime) % IdleFrames;
        return new Point(idle * FrameSize, RowIdle * FrameSize);
    }

    /// <summary>
    /// Draws one frame of the sheet over the whole plate, sliced so the corners, bolts and
    /// groove keep their authored size however wide the label made the button.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="destination">The area to cover, in screen pixels.</param>
    /// <param name="origin">Top-left of the frame in the sheet.</param>
    /// <param name="tint">Colour multiplied into the frame.</param>
    /// <param name="layerDepth">Sort key for the batch. See <see cref="Layers"/>.</param>
    private void DrawPlate(SpriteBatch spriteBatch, Rectangle destination, Point origin, Color tint, float layerDepth) =>
        NineSlice.Draw(spriteBatch, _texture, destination, origin, FrameSize, CornerSize, DrawScale, tint, layerDepth);

    /// <summary>
    /// Measures the block of text on the plate: the label, plus the detail line if there is one.
    /// </summary>
    private Vector2 MeasureText()
    {
        Vector2 size = LabelFont.MeasureString(Label ?? string.Empty);
        if (string.IsNullOrEmpty(Sublabel)) return size;

        Vector2 detail = _detailFont.MeasureString(Sublabel);
        return new Vector2(MathF.Max(size.X, detail.X), size.Y + SublabelGap + detail.Y);
    }

    /// <summary>
    /// Draws the label -- and the detail line under it, if there is one -- centred on the
    /// plate over a hard offset shadow.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="plate">The plate the label sits on, already offset by any press.</param>
    private void DrawLabel(SpriteBatch spriteBatch, Rectangle plate)
    {
        if (string.IsNullOrEmpty(Label)) return;

        // The label warms toward the accent as the plate kindles, so the light reads as
        // falling on the text rather than only ringing it.
        Color cold = Enabled ? new Color(196, 188, 186) : new Color(96, 94, 98);
        Color lit = Color.Lerp(cold, Color.Lerp(Accent, Color.White, 0.55f), _kindle);

        // Both lines are centred on the block rather than on the plate, so adding a detail
        // line pushes the label up instead of leaving it centred with the detail hanging off.
        Vector2 block = MeasureText();
        float top = MathF.Round(plate.Y + (plate.Height - block.Y) / 2f);

        DrawLine(spriteBatch, LabelFont, Label, plate, top, lit);

        if (string.IsNullOrEmpty(Sublabel)) return;

        // Dimmer than the label and kept nearer its cold colour: it is what the slot holds,
        // not what the button does, and it should not compete with the name above it.
        Color detail = Color.Lerp(cold * 0.72f, Color.Lerp(Accent, Color.White, 0.35f), _kindle * 0.7f);
        DrawLine(spriteBatch, _detailFont, Sublabel, plate, top + LabelFont.MeasureString(Label).Y + SublabelGap, detail);
    }

    /// <summary>The font the label is set in. See <see cref="Small"/>.</summary>
    private SpriteFont LabelFont => Small ? _smallFont : _font;

    /// <summary>
    /// Draws one line of text centred across the plate, over a hard offset shadow.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The line to draw.</param>
    /// <param name="plate">The plate to centre across.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    private static void DrawLine(SpriteBatch spriteBatch, SpriteFont font, string text, Rectangle plate, float y, Color color)
    {
        Vector2 size = font.MeasureString(text);
        var position = new Vector2(MathF.Round(plate.X + (plate.Width - size.X) / 2f), MathF.Round(y));

        spriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), Color.Black * 0.75f,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabelShadow);
        spriteBatch.DrawString(font, text, position, color,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabel);
    }

    /// <summary>Moves <paramref name="value"/> toward <paramref name="target"/> without overshooting.</summary>
    private static float Approach(float value, float target, float step)
    {
        if (value < target) return MathF.Min(target, value + step);
        return MathF.Max(target, value - step);
    }

    /// <summary>
    /// Maps a 0..1 animation position onto a frame index, holding the last frame on screen
    /// once the animation has arrived rather than wrapping back to the first.
    /// </summary>
    private static int Index(float progress, int frames) =>
        Math.Clamp((int)(progress * frames), 0, frames - 1);
}
