using Microsoft.Xna.Framework;
using TheBlackBox.Audio;
using TheBlackBox.Screens;
using TheBlackBox.StateManagement;

namespace TheBlackBox;

/// <summary>
/// The Black Box. Sets up the window, the three things every screen shares (the screen
/// manager, the box and the sound), and puts the title up.
/// </summary>
/// <remarks>
/// This used to be the title screen, the forms and the table's sprite batches all in one class
/// with a Screen enum deciding which was in front. Now every screen is its own GameScreen on a
/// ScreenManager, the way the game state management tutorial does it, and this is just the setup.
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

        // The box and the sound are components so they keep running whatever screen is up, and
        // services so any screen can reach them, like the achievement service in the services tutorial.
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

    /// <summary>Loads the box and every sound, then puts the box and the title up.</summary>
    /// <remarks>The first screens go on here and not in the constructor, because the box screen starts the title music as it comes on.</remarks>
    protected override void LoadContent()
    {
        // The proof run is silent, never writes the settings file, and plays on with the window behind other things.
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
