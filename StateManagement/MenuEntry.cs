// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.StateManagement;

/// <summary>
/// One entry on a MenuScreen. The sample just draws text, but I made each entry one of my button
/// plates so the menus match the rest of the game and can still be clicked.
/// </summary>
public class MenuEntry
{
    /// <summary>The plate the entry is drawn as.</summary>
    public ButtonSprite Plate { get; }

    /// <summary>What the entry says.</summary>
    public string Text
    {
        get => Plate.Label;
        set => Plate.Label = value;
    }

    /// <summary>Whether left and right change this entry instead of moving off it. True once something listens to Adjusted.</summary>
    public bool IsAdjustable => Adjusted is not null;

    /// <summary>Raised when the entry is picked, by Enter or a click.</summary>
    public event Action Selected;

    /// <summary>Raised with -1 or 1 when left or right is pressed on it.</summary>
    public event Action<int> Adjusted;

    /// <summary>Creates an entry.</summary>
    /// <param name="text">What it says.</param>
    /// <param name="accent">The light in its groove. See ButtonSprite.</param>
    /// <param name="size">Its size, or default to fit the text.</param>
    public MenuEntry(string text, Color accent, Point size = default)
    {
        Plate = new ButtonSprite(text, Vector2.Zero, accent, size);
        Plate.Clicked += OnSelectEntry;
    }

    /// <summary>Loads the plate. It measures its label here, so lay the menu out after this.</summary>
    /// <param name="content">The content manager to load with.</param>
    public void LoadContent(ContentManager content) => Plate.LoadContent(content);

    /// <summary>Raises Selected.</summary>
    protected internal virtual void OnSelectEntry() => Selected?.Invoke();

    /// <summary>Raises Adjusted.</summary>
    /// <param name="direction">-1 for left, 1 for right.</param>
    protected internal virtual void OnAdjust(int direction) => Adjusted?.Invoke(direction);

    /// <summary>Runs the plate. Only called while the menu has focus, so you can't click a menu that's under a popup.</summary>
    /// <param name="isSelected">Whether this is the entry the keyboard is on.</param>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(bool isSelected, GameTime gameTime)
    {
        Plate.Focused = isSelected;
        Plate.Update(gameTime);
    }

    /// <summary>Draws the plate, faded with the menu's transition.</summary>
    /// <param name="screen">The menu it is on.</param>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with. Point-sampled.</param>
    public void Draw(MenuScreen screen, GameTime gameTime, SpriteBatch spriteBatch)
    {
        Plate.Opacity = screen.TransitionAlpha;
        Plate.Draw(gameTime, spriteBatch);
    }
}
