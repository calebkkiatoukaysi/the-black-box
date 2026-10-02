// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Audio;

namespace TheBlackBox.StateManagement;

/// <summary>
/// A screen with a list of entries that can be walked with the keyboard or a pad, or clicked.
/// </summary>
/// <remarks>
/// Up and down always move. Left and right move too (the title's entries are in a row), unless
/// the entry is adjustable, in which case they change it. That is how the volume sliders work.
/// </remarks>
public abstract class MenuScreen : GameScreen
{
    private readonly List<MenuEntry> _menuEntries = new();
    private int _selectedEntry;

    private readonly InputAction _menuUp = new(
        new[] { Buttons.DPadUp, Buttons.LeftThumbstickUp },
        new[] { Keys.Up, Keys.W }, true);
    private readonly InputAction _menuDown = new(
        new[] { Buttons.DPadDown, Buttons.LeftThumbstickDown },
        new[] { Keys.Down, Keys.S }, true);
    private readonly InputAction _menuLeft = new(
        new[] { Buttons.DPadLeft, Buttons.LeftThumbstickLeft },
        new[] { Keys.Left, Keys.A }, true);
    private readonly InputAction _menuRight = new(
        new[] { Buttons.DPadRight, Buttons.LeftThumbstickRight },
        new[] { Keys.Right, Keys.D }, true);
    private readonly InputAction _menuSelect = new(
        new[] { Buttons.A, Buttons.Start },
        new[] { Keys.Enter, Keys.Space }, true);
    private readonly InputAction _menuCancel = new(
        new[] { Buttons.B, Buttons.Back },
        new[] { Keys.Escape }, true);

    /// <summary>Whether the menu had focus last frame. Coming back to it resets the plates so a stale click cannot land.</summary>
    private bool _wasActive;

    /// <summary>This screen's own content, unloaded with it.</summary>
    protected ContentManager Content { get; private set; }

    /// <summary>The sounds. Fetched from the game's services when the screen comes on.</summary>
    protected AudioManager Audio { get; private set; }

    /// <summary>The entries, in the order the keyboard walks them.</summary>
    protected IList<MenuEntry> MenuEntries => _menuEntries;

    /// <summary>The entry the keyboard is on.</summary>
    protected int SelectedEntry
    {
        get => _selectedEntry;
        set => _selectedEntry = MathHelper.Clamp(value, 0, _menuEntries.Count - 1);
    }

    /// <summary>Makes the content manager, fetches the sounds, and loads every entry. Lay out after calling this.</summary>
    public override void Activate()
    {
        Content ??= new ContentManager(ScreenManager.Game.Services, "Content");
        Audio = ScreenManager.Game.Services.GetService<AudioManager>();

        foreach (MenuEntry entry in _menuEntries)
        {
            entry.LoadContent(Content);

            // Adjustable entries are changed with left and right, not pressed, so they stay quiet on Enter.
            entry.Plate.Clicked += () =>
            {
                if (!entry.IsAdjustable) Audio?.Play(Sfx.MenuConfirm);
            };
        }
    }

    /// <summary>Unloads this screen's content.</summary>
    public override void Unload() => Content?.Unload();

    /// <summary>Moves the selection, adjusts, picks and backs out.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The input this frame.</param>
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (_menuEntries.Count == 0) return;

        // The mouse picks whatever it is over, but only on frames it moved, so a mouse resting on
        // a plate does not fight the keyboard.
        if (input.MouseMoved)
        {
            for (int i = 0; i < _menuEntries.Count; i++)
            {
                if (i != _selectedEntry && _menuEntries[i].Plate.Enabled
                    && _menuEntries[i].Plate.Bounds.Contains(input.MousePosition))
                {
                    _selectedEntry = i;
                    Audio?.Play(Sfx.MenuMove);
                }
            }
        }

        MenuEntry selected = _menuEntries[_selectedEntry];

        if (_menuUp.Occurred(input)) Move(-1);
        else if (_menuDown.Occurred(input)) Move(1);
        else if (_menuLeft.Occurred(input))
        {
            if (selected.IsAdjustable) Adjust(selected, -1);
            else Move(-1);
        }
        else if (_menuRight.Occurred(input))
        {
            if (selected.IsAdjustable) Adjust(selected, 1);
            else Move(1);
        }
        else if (_menuSelect.Occurred(input))
        {
            // Through the plate, so it sinks like it was clicked, and Clicked selects it.
            if (selected.Plate.Enabled) selected.Plate.Activate();
        }
        else if (_menuCancel.Occurred(input))
        {
            OnCancel();
        }
    }

    /// <summary>Runs the transition, and the plates while the menu has focus.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (!IsActive)
        {
            _wasActive = false;
            return;
        }

        if (!_wasActive)
        {
            foreach (MenuEntry entry in _menuEntries) entry.Plate.Reset();
            _wasActive = true;
        }

        for (int i = 0; i < _menuEntries.Count; i++)
            _menuEntries[i].Update(i == _selectedEntry, gameTime);
    }

    /// <summary>Draws the entries. A screen draws whatever is behind them first, then calls this.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        foreach (MenuEntry entry in _menuEntries) entry.Draw(this, gameTime, spriteBatch);
        spriteBatch.End();
    }

    /// <summary>What Escape does. Leaves the menu, unless a screen says otherwise.</summary>
    protected virtual void OnCancel()
    {
        Audio?.Play(Sfx.MenuBack);
        ExitScreen();
    }

    /// <summary>Lines the entries up in a column, centred on a point.</summary>
    /// <param name="centre">The middle of the column.</param>
    /// <param name="gap">The gap between one plate and the next.</param>
    protected void LayOutColumn(Vector2 centre, float gap)
    {
        float total = -gap;
        foreach (MenuEntry entry in _menuEntries) total += entry.Plate.Size.Y + gap;

        float top = centre.Y - total / 2f;
        foreach (MenuEntry entry in _menuEntries)
        {
            entry.Plate.Center = new Vector2(centre.X, top + entry.Plate.Size.Y / 2f);
            top += entry.Plate.Size.Y + gap;
        }
    }

    /// <summary>Lines the entries up in a row, centred on a point.</summary>
    /// <param name="centre">The middle of the row.</param>
    /// <param name="gap">The gap between one plate and the next.</param>
    protected void LayOutRow(Vector2 centre, float gap)
    {
        float total = -gap;
        foreach (MenuEntry entry in _menuEntries) total += entry.Plate.Size.X + gap;

        float left = centre.X - total / 2f;
        foreach (MenuEntry entry in _menuEntries)
        {
            entry.Plate.Center = new Vector2(left + entry.Plate.Size.X / 2f, centre.Y);
            left += entry.Plate.Size.X + gap;
        }
    }

    /// <summary>Moves the selection along, wrapping, and skipping anything that cannot be picked.</summary>
    /// <param name="step">-1 or 1.</param>
    private void Move(int step)
    {
        int count = _menuEntries.Count;
        int next = _selectedEntry;

        for (int tries = 0; tries < count; tries++)
        {
            next = (next + step + count) % count;
            if (_menuEntries[next].Plate.Enabled) break;
        }

        if (next == _selectedEntry) return;

        _selectedEntry = next;
        Audio?.Play(Sfx.MenuMove);
    }

    /// <summary>Changes an adjustable entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="direction">-1 or 1.</param>
    private void Adjust(MenuEntry entry, int direction)
    {
        entry.OnAdjust(direction);
        Audio?.Play(Sfx.OptionChange);
    }
}
