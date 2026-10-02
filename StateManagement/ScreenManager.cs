// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0,
// as presented in the CIS 580 textbook (Game Architecture > Game Screens).
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.StateManagement;

/// <summary>
/// Keeps the stack of screens, updates and draws them, and gives the input to the top one.
/// </summary>
public class ScreenManager : DrawableGameComponent
{
    private readonly List<GameScreen> _screens = new();
    private readonly List<GameScreen> _tmpScreensList = new();

    private readonly InputState _input = new();

    private bool _isInitialized;

    /// <summary>A SpriteBatch shared by every screen.</summary>
    public SpriteBatch SpriteBatch { get; private set; }

    /// <summary>One white pixel, stretched out for fades and veils.</summary>
    public Texture2D BlankTexture { get; private set; }

    /// <summary>Whether the top screen still gets input when the window isn't in front.</summary>
    /// <remarks>Off when you play. The proof run turns it on since it plays itself, usually with the window behind everything else.</remarks>
    public bool IgnoreWindowFocus { get; set; }

    /// <summary>Constructs a new ScreenManager.</summary>
    /// <param name="game">The game this ScreenManager belongs to.</param>
    public ScreenManager(Game game) : base(game) { }

    /// <summary>Initializes the ScreenManager and starts listening for typed characters.</summary>
    public override void Initialize()
    {
        base.Initialize();
        _isInitialized = true;

        // Typed characters come from the window, so the name field doesn't have to deal with shift or keyboard layouts.
        Game.Window.TextInput += (_, e) => TopScreenWithFocus()?.HandleTextInput(e.Character);
    }

    /// <summary>Loads the shared batch and pixel, then has every screen that's already added load too.</summary>
    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);

        BlankTexture = new Texture2D(GraphicsDevice, 1, 1);
        BlankTexture.SetData(new[] { Color.White });

        foreach (GameScreen screen in _screens)
            screen.Activate();
    }

    /// <summary>Unloads every screen's content.</summary>
    protected override void UnloadContent()
    {
        foreach (GameScreen screen in _screens)
            screen.Unload();
    }

    /// <summary>Updates every screen and lets the top active one handle input.</summary>
    /// <param name="gameTime">The game time.</param>
    public override void Update(GameTime gameTime)
    {
        _input.Update();

        // Copy the list first, since a screen can add or remove screens while it updates.
        _tmpScreensList.Clear();
        _tmpScreensList.AddRange(_screens);

        bool otherScreenHasFocus = !Game.IsActive && !IgnoreWindowFocus;
        bool coveredByOtherScreen = false;

        while (_tmpScreensList.Count > 0)
        {
            GameScreen screen = _tmpScreensList[^1];
            _tmpScreensList.RemoveAt(_tmpScreensList.Count - 1);

            screen.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            if (screen.ScreenState == ScreenState.TransitionOn || screen.ScreenState == ScreenState.Active)
            {
                // The first active screen gets the input.
                if (!otherScreenHasFocus)
                {
                    screen.HandleInput(gameTime, _input);
                    otherScreenHasFocus = true;
                }

                // An active non-popup covers everything under it.
                if (!screen.IsPopup) coveredByOtherScreen = true;
            }
        }
    }

    /// <summary>Draws every screen that isn't hidden, bottom of the stack first.</summary>
    /// <param name="gameTime">The game time.</param>
    public override void Draw(GameTime gameTime)
    {
        foreach (GameScreen screen in _screens)
        {
            if (screen.ScreenState == ScreenState.Hidden) continue;

            screen.Draw(gameTime);
        }
    }

    /// <summary>Adds a screen to the top of the stack.</summary>
    /// <param name="screen">The screen to add.</param>
    public void AddScreen(GameScreen screen)
    {
        screen.ScreenManager = this;
        screen.IsExiting = false;

        // If the graphics device is ready the screen can load right away.
        if (_isInitialized) screen.Activate();

        _screens.Add(screen);
    }

    /// <summary>Takes a screen off the stack right away. Screens usually call ExitScreen instead so they fade out first.</summary>
    /// <param name="screen">The screen to remove.</param>
    public void RemoveScreen(GameScreen screen)
    {
        if (_isInitialized)
        {
            screen.Deactivate();
            screen.Unload();
        }

        _screens.Remove(screen);
        _tmpScreensList.Remove(screen);
    }

    /// <summary>Every screen on the stack, bottom first.</summary>
    /// <returns>A copy, so it's safe to exit screens while looping over it.</returns>
    public GameScreen[] GetScreens() => _screens.ToArray();

    /// <summary>Draws black over the whole screen. Used for the fades.</summary>
    /// <param name="alpha">How black, from 0 to 1.</param>
    public void FadeBackBufferToBlack(float alpha)
    {
        if (alpha <= 0f) return;

        SpriteBatch.Begin();
        SpriteBatch.Draw(BlankTexture, GraphicsDevice.Viewport.Bounds, Color.Black * alpha);
        SpriteBatch.End();
    }

    /// <summary>Which screen gets typed characters: the top one that's on and not leaving.</summary>
    private GameScreen TopScreenWithFocus()
    {
        for (int i = _screens.Count - 1; i >= 0; i--)
        {
            GameScreen screen = _screens[i];
            if (screen.IsExiting) continue;
            if (screen.ScreenState is ScreenState.TransitionOn or ScreenState.Active) return screen;
        }

        return null;
    }
}
