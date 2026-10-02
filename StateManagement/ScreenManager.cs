// Adapted from the Game State Management sample from Microsoft XNA Game Studio 4.0,
// as presented in the CIS 580 textbook (Game Architecture > Game Screens).
// Archived at https://github.com/SimonDarksideJ/GameStateManagementSample

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.StateManagement;

/// <summary>
/// A component that manages one or more GameScreen instances. It keeps a stack of screens,
/// calls their Update and Draw when appropriate, and routes input to the topmost one.
/// </summary>
public class ScreenManager : DrawableGameComponent
{
    private readonly List<GameScreen> _screens = new();
    private readonly List<GameScreen> _tmpScreensList = new();

    private readonly InputState _input = new();

    private bool _isInitialized;

    /// <summary>A SpriteBatch shared by every screen.</summary>
    public SpriteBatch SpriteBatch { get; private set; }

    /// <summary>One white pixel, stretched for fades and veils.</summary>
    public Texture2D BlankTexture { get; private set; }

    /// <summary>Whether the top screen keeps the input while the window is behind something else.</summary>
    /// <remarks>Off when playing, so a game in the background ignores the keys. The proof run turns it on: it plays itself, often behind whatever else is open.</remarks>
    public bool IgnoreWindowFocus { get; set; }

    /// <summary>Constructs a new ScreenManager.</summary>
    /// <param name="game">The game this ScreenManager belongs to.</param>
    public ScreenManager(Game game) : base(game) { }

    /// <summary>Initializes the ScreenManager, and starts listening for typed characters.</summary>
    public override void Initialize()
    {
        base.Initialize();
        _isInitialized = true;

        // Characters come from the window, so the name field never has to deal with layouts or shift.
        Game.Window.TextInput += (_, e) => TopScreenWithFocus()?.HandleTextInput(e.Character);
    }

    /// <summary>Loads the shared batch and pixel, and tells every screen already added to load.</summary>
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

    /// <summary>Updates every screen, and lets the topmost active one handle input.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Update(GameTime gameTime)
    {
        _input.Update();

        // Copy the list, so a screen that adds or removes another while updating does not upset the loop.
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

    /// <summary>Draws every screen that is not hidden, bottom of the stack first.</summary>
    /// <param name="gameTime">The frame's timing.</param>
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

        // If there is a graphics device yet, the screen can load now.
        if (_isInitialized) screen.Activate();

        _screens.Add(screen);
    }

    /// <summary>Takes a screen off the stack straight away. Normally a screen calls ExitScreen instead, to transition off first.</summary>
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
    /// <returns>A copy, so it is safe to exit screens while walking it.</returns>
    public GameScreen[] GetScreens() => _screens.ToArray();

    /// <summary>Draws a black quad over the whole screen. Used for fades and for dimming what is behind a popup.</summary>
    /// <param name="alpha">How black, from 0 to 1.</param>
    public void FadeBackBufferToBlack(float alpha)
    {
        if (alpha <= 0f) return;

        SpriteBatch.Begin();
        SpriteBatch.Draw(BlankTexture, GraphicsDevice.Viewport.Bounds, Color.Black * alpha);
        SpriteBatch.End();
    }

    /// <summary>The screen typed characters go to: the topmost one that is on and not leaving.</summary>
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
