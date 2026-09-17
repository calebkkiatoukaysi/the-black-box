using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The form the START button opens: three save slots, and what is in each of them.
/// </summary>
/// <remarks>
/// <para>
/// It is drawn over the title screen rather than replacing it, so the box keeps breathing
/// behind the veil while the player decides. The box is the thing the player is agreeing to
/// sit down with -- taking it off screen at the moment they choose a slot would be throwing
/// away the only pressure the title screen has.
/// </para>
/// <para>
/// The form owns no file handling of its own. It asks <see cref="SaveSystem"/> for three
/// <see cref="SaveSlot"/> descriptions when it opens and draws them; every path, exception
/// and byte stays on the other side of that call.
/// </para>
/// </remarks>
public class SaveSlotMenu
{
    /// <summary>Width and height of the single frame in panel.png.</summary>
    private const int PanelFrameSize = 32;

    /// <summary>
    /// Fixed corner of the panel's nine-slice. Authored in tools/generate_assets.py as
    /// <c>PANEL_CORNER</c>, and the two have to match or the recess shears when it stretches.
    /// </summary>
    private const int PanelCornerSize = 12;

    /// <summary>Blown up by the same whole number as the buttons that sit on it.</summary>
    private const float PanelScale = 3f;

    private const string Heading = "CHOOSE A SLOT";
    private const string BackLabel = "BACK";
    private const string EraseLabel = "ERASE";

    /// <summary>What the erase button says once it is waiting for the second click.</summary>
    private const string ConfirmLabel = "SURE?";

    private const int PanelWidth = 940;
    private const int SlotWidth = 620;
    private const int SlotHeight = 110;
    private const int EraseWidth = 170;

    /// <summary>Gap between a slot and its erase button.</summary>
    private const int EraseGap = 16;

    /// <summary>Gap between one slot row and the next.</summary>
    private const int RowGap = 18;

    /// <summary>Breathing room inside the panel's recess.</summary>
    private const int PanelPadding = 40;

    /// <summary>Gap under the heading, and over the back button.</summary>
    private const int HeadingGap = 30;

    /// <summary>Gap between the back button and the line that reports what went wrong.</summary>
    private const int StatusGap = 14;

    /// <summary>
    /// How far the veil darkens the title screen behind the form. Enough to push the box back
    /// and let the panel read, not enough to take it off the screen -- it is still watching.
    /// </summary>
    private const float VeilOpacity = 0.62f;

    private Texture2D _panel;

    /// <summary>A single white pixel, stretched over the screen to dim what is behind the form.</summary>
    private Texture2D _veil;

    private SpriteFont _font;
    private SpriteFont _detailFont;

    private readonly Rectangle _screen;

    private SaveSlot[] _slots;
    private ButtonSprite[] _slotButtons;
    private ButtonSprite[] _eraseButtons;
    private ButtonSprite _backButton;

    private Rectangle _panelBounds;

    /// <summary>Top of the line errors are reported on, worked out in <see cref="LayOut"/>.</summary>
    private float _statusY;

    /// <summary>Which slot's erase button is waiting for its second click, or -1 for none.</summary>
    private int _confirmingErase = -1;

    /// <summary>What went wrong with the last thing the player asked for, or null.</summary>
    private string _status;

    /// <summary>Whether the form is on screen and taking input.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Raised when the player commits to a slot, with the run that slot now holds.</summary>
    public event Action<int, SaveData> SlotChosen;

    /// <summary>Raised when the player backs out without choosing anything.</summary>
    public event Action Closed;

    /// <summary>
    /// Creates the form, sized and centred against the screen it will be drawn over.
    /// </summary>
    /// <param name="screen">The whole window, in screen pixels.</param>
    public SaveSlotMenu(Rectangle screen)
    {
        _screen = screen;
    }

    /// <summary>
    /// Loads the panel, the fonts, and every control on the form.
    /// </summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the one pixel the veil is made of.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _panel = content.Load<Texture2D>("panel");
        _font = content.Load<SpriteFont>("spectral-ui");
        _detailFont = content.Load<SpriteFont>("spectral-detail");

        // Not worth a PNG and a content-pipeline entry: it is one opaque pixel, stretched.
        _veil = new Texture2D(graphicsDevice, 1, 1);
        _veil.SetData(new[] { Color.White });

        _slotButtons = new ButtonSprite[SaveSystem.SlotCount];
        _eraseButtons = new ButtonSprite[SaveSystem.SlotCount];

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int slot = i;

            _slotButtons[i] = new ButtonSprite(string.Empty, Vector2.Zero, ButtonSprite.BoneWhite,
                new Point(SlotWidth, SlotHeight));
            _slotButtons[i].Clicked += () => Choose(slot);

            _eraseButtons[i] = new ButtonSprite(EraseLabel, Vector2.Zero, ButtonSprite.EmberRed,
                new Point(EraseWidth, SlotHeight));
            _eraseButtons[i].Clicked += () => Erase(slot);

            _slotButtons[i].LoadContent(content);
            _eraseButtons[i].LoadContent(content);
        }

        _backButton = new ButtonSprite(BackLabel, Vector2.Zero, ButtonSprite.BoneWhite);
        _backButton.Clicked += Close;
        _backButton.LoadContent(content);

        LayOut();
    }

    /// <summary>
    /// Opens the form, reading what is actually on disk as it does.
    /// </summary>
    /// <remarks>
    /// The slots are read here rather than once at startup on purpose. The form is the only
    /// place the player sees what is saved, so it should be showing the disk as it is now --
    /// after a run was played, after a slot was erased, after the files were edited by hand
    /// with the game left running.
    /// </remarks>
    public void Open()
    {
        Refresh();

        _confirmingErase = -1;
        _status = null;
        IsOpen = true;

        // Every control comes back cold, and forgets whatever the mouse was doing while the
        // form was away -- including the click on START that opened it.
        foreach (var button in _slotButtons) button.Reset();
        foreach (var button in _eraseButtons) button.Reset();
        _backButton.Reset();
    }

    /// <summary>Closes the form and tells whoever opened it.</summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        Closed?.Invoke();
    }

    /// <summary>
    /// Updates every control on the form.
    /// </summary>
    /// <param name="gameTime">The GameTime.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsOpen) return;

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            _slotButtons[i].Update(gameTime);
            if (_eraseButtons[i].Enabled) _eraseButtons[i].Update(gameTime);
        }

        _backButton.Update(gameTime);
    }

    /// <summary>
    /// Draws the veil, the panel and everything on it.
    /// </summary>
    /// <remarks>
    /// Expected to be called inside its own batch, over everything the title screen drew.
    /// </remarks>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        if (!IsOpen) return;

        // The veil is tinted toward the box's own dark rather than to flat black, so the
        // title screen behind it dims instead of turning grey.
        spriteBatch.Draw(_veil, _screen, null, new Color(6, 5, 10) * VeilOpacity,
            0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);

        NineSlice.Draw(spriteBatch, _panel, _panelBounds, Point.Zero,
            PanelFrameSize, PanelCornerSize, PanelScale, Color.White, Layers.PanelPlate);

        DrawCentered(spriteBatch, _font, Heading, _panelBounds.Top + PanelPadding, new Color(214, 206, 200));

        for (int i = 0; i < _slotButtons.Length; i++)
        {
            _slotButtons[i].Draw(gameTime, spriteBatch);
            if (_eraseButtons[i].Enabled) _eraseButtons[i].Draw(gameTime, spriteBatch);
        }

        _backButton.Draw(gameTime, spriteBatch);

        // The row is reserved in the layout whether or not anything goes wrong, so a failure
        // does not shove the form around underneath the cursor that caused it.
        if (_status is not null)
            DrawCentered(spriteBatch, _detailFont, _status, _statusY, ButtonSprite.EmberRed);
    }

    /// <summary>
    /// Re-reads the slots and puts what they say back onto the plates.
    /// </summary>
    private void Refresh()
    {
        _slots = SaveSystem.ReadAll();

        for (int i = 0; i < _slots.Length; i++)
        {
            SaveSlot slot = _slots[i];

            _slotButtons[i].Label = slot.Name;
            _slotButtons[i].Sublabel = slot.Summary;

            // Amber for a run waiting to be picked up, bone-white for an empty slot offering
            // a new one, and a dead plate for a slot the game could not read -- there is
            // nothing in there to load, so the button should not pretend there is.
            _slotButtons[i].Accent = slot.HasRun ? ButtonSprite.Amber : ButtonSprite.BoneWhite;
            _slotButtons[i].Enabled = slot.State != SaveSlotState.Unreadable;

            // Nothing to erase in a slot that was never written. An unreadable one still gets
            // the button: erasing it is the only way the player gets the slot back.
            _eraseButtons[i].Enabled = slot.State != SaveSlotState.Empty;
            _eraseButtons[i].Label = EraseLabel;
            _eraseButtons[i].Reset();
        }
    }

    /// <summary>
    /// Commits to a slot: loads the run in it, or starts a new one and writes it immediately.
    /// </summary>
    /// <remarks>
    /// A new run is saved the moment it is created rather than at the first checkpoint. The
    /// slot the player just claimed should still be theirs if the game is closed on the next
    /// screen, and it makes the first write happen while the player is looking at the form --
    /// which is the one place the game can tell them if the disk will not take it.
    /// </remarks>
    /// <param name="slot">Which slot was clicked.</param>
    private void Choose(int slot)
    {
        // Clicking a slot while an erase is half-confirmed is the player changing their mind
        // about erasing, not confirming it somewhere else.
        CancelConfirm();

        SaveData data = _slots[slot].Data;

        if (data is null)
        {
            data = SaveData.NewRun();

            if (!SaveSystem.Write(slot, data))
            {
                _status = "COULD NOT WRITE " + _slots[slot].Name + " -- " + SaveSystem.LastError;
                return;
            }

            Refresh();
        }

        IsOpen = false;
        SlotChosen?.Invoke(slot, data);
    }

    /// <summary>
    /// Erases a slot, on the second click.
    /// </summary>
    /// <remarks>
    /// The first click turns the button into the question and the second one answers it. A
    /// modal "are you sure" would need a second form, a second set of controls and a way back
    /// out of it; the button asking in place costs one field and is just as hard to do by
    /// accident, because the plate the player is about to click has changed under them.
    /// </remarks>
    /// <param name="slot">Which slot's erase button was clicked.</param>
    private void Erase(int slot)
    {
        if (_confirmingErase != slot)
        {
            CancelConfirm();
            _confirmingErase = slot;
            _eraseButtons[slot].Label = ConfirmLabel;
            _status = null;
            return;
        }

        _confirmingErase = -1;

        if (!SaveSystem.Delete(slot))
        {
            _status = "COULD NOT ERASE " + _slots[slot].Name + " -- " + SaveSystem.LastError;
            _eraseButtons[slot].Label = EraseLabel;
            return;
        }

        _status = null;
        Refresh();
    }

    /// <summary>Takes back a half-confirmed erase and puts the button's own label back.</summary>
    private void CancelConfirm()
    {
        if (_confirmingErase < 0) return;

        _eraseButtons[_confirmingErase].Label = EraseLabel;
        _confirmingErase = -1;
    }

    /// <summary>
    /// Sizes the panel around its contents and places every control on it.
    /// </summary>
    /// <remarks>
    /// Run once, after the controls have measured themselves in <c>LoadContent</c> -- the back
    /// button sizes itself to its label, so until then the panel does not know how tall it is.
    /// Deriving the panel from the rows rather than hardcoding a rectangle is what lets the
    /// slot count change later without any of these numbers being wrong.
    /// </remarks>
    private void LayOut()
    {
        int rows = _slotButtons.Length;
        int rowsHeight = rows * SlotHeight + (rows - 1) * RowGap;

        int headingHeight = (int)MathF.Round(_font.MeasureString(Heading).Y);
        int statusHeight = (int)MathF.Round(_detailFont.MeasureString(Heading).Y);

        int panelHeight = PanelPadding + headingHeight + HeadingGap
            + rowsHeight + HeadingGap + _backButton.Size.Y + StatusGap + statusHeight + PanelPadding;

        _panelBounds = new Rectangle(
            _screen.Center.X - PanelWidth / 2,
            _screen.Center.Y - panelHeight / 2,
            PanelWidth,
            panelHeight);

        // Each row is a slot and its erase button, centred as a pair so the slots stay lined
        // up whether or not their erase button happens to be shown.
        int rowWidth = SlotWidth + EraseGap + EraseWidth;
        int left = _panelBounds.Center.X - rowWidth / 2;
        int top = _panelBounds.Top + PanelPadding + headingHeight + HeadingGap;

        for (int i = 0; i < rows; i++)
        {
            float centreY = top + i * (SlotHeight + RowGap) + SlotHeight / 2f;

            _slotButtons[i].Center = new Vector2(left + SlotWidth / 2f, centreY);
            _eraseButtons[i].Center = new Vector2(left + SlotWidth + EraseGap + EraseWidth / 2f, centreY);
        }

        float backTop = top + rowsHeight + HeadingGap;
        _backButton.Center = new Vector2(_panelBounds.Center.X, backTop + _backButton.Size.Y / 2f);

        _statusY = backTop + _backButton.Size.Y + StatusGap;
    }

    /// <summary>
    /// Draws a line of text centred across the panel, over a hard offset shadow.
    /// </summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The line to draw.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    private void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float y, Color color)
    {
        Vector2 size = font.MeasureString(text);
        var position = new Vector2(MathF.Round(_panelBounds.Center.X - size.X / 2f), MathF.Round(y));

        spriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), Color.Black * 0.7f,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        spriteBatch.DrawString(font, text, position, color,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }
}
