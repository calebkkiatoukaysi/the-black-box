using Microsoft.Xna.Framework;
using TheBlackBox.Audio;
using TheBlackBox.Screens;
using TheBlackBox.StateManagement;

namespace TheBlackBox;

/// <summary>
/// The Black Box. Sets up the window and the three things every screen shares (the screen
/// manager, the box and the sound), then opens the title.
/// </summary>
/// <remarks>
/// This used to have the title screen, the forms and the table all in one class with a Screen
/// enum picking which one was showing. Now every screen is its own GameScreen on a ScreenManager
/// like the game state management tutorial, so this class is just setup.
/// </remarks>
public partial class BlackBoxGame : Game
{
    internal const int ScreenWidth = 1600;
    internal const int ScreenHeight = 900;

    /// <summary>Not quite black, so the box itself still reads as the darkest thing on screen.</summary>
    private static readonly Color VoidColor = new(10, 8, 16);

    private readonly GraphicsDeviceManager _graphics;
    private readonly ScreenManager _screenManager;
    private readonly BoxScene _boxScene;
    private readonly AudioManager _audio;

    public BlackBoxGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = ScreenWidth;
        _graphics.PreferredBackBufferHeight = ScreenHeight;
        _graphics.ApplyChanges();
        Content.RootDirectory = "Content";

        // The eyes follow the cursor, so the player needs to be able to see it.
        IsMouseVisible = true;

        // The box and the sound are components so they keep running no matter what screen is up, and
        // services so any screen can get to them (like the achievement service in the services tutorial).
        _boxScene = new BoxScene(this);
        Components.Add(_boxScene);
        Services.AddService(_boxScene);

        _audio = new AudioManager(this, SettingsStore.Load());
        Components.Add(_audio);
        Services.AddService(_audio);

        _screenManager = new ScreenManager(this);
        Components.Add(_screenManager);
        Services.AddService(_screenManager);
    }

    /// <summary>Loads the box and all the sounds, then opens the box screen and the title.</summary>
    /// <remarks>The first screens get added here instead of the constructor because the box screen starts the title music when it opens.</remarks>
    protected override void LoadContent()
    {
        // The proof run has no sound, doesn't write the settings file, and keeps going even if the window isn't focused.
        if (ProofDirectory is not null)
        {
            _audio.Silent = true;
            SettingsStore.ReadOnly = true;
            _screenManager.IgnoreWindowFocus = true;
        }

        _boxScene.LoadContent(Content);
        _audio.LoadContent(Content);

        _screenManager.AddScreen(new BoxBackgroundScreen());
        _screenManager.AddScreen(new TitleScreen());
    }

    protected override void Update(GameTime gameTime)
    {
        UpdateProof();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        BeginProofCapture();
        GraphicsDevice.Clear(VoidColor);

        base.Draw(gameTime);

        EndProofCapture();
    }
}
