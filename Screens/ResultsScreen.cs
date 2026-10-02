using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The end of a run. The veil comes down over the table with the verdict, the run's numbers, and
/// two ways out: back to the lobby or back to the title.
/// </summary>
/// <remarks>
/// Both ways out do what LEAVE THE TABLE used to do: the run gets saved ready to play again (next
/// chapter if you won, the same table if you lost). Escape does nothing here, you have to pick one.
/// </remarks>
public class ResultsScreen : MenuScreen
{
    private const string LobbyLabel = "RETURN TO THE LOBBY";
    private const string TitleLabel = "RETURN TO TITLE";

    /// <summary>Where the numbers go under the verdict, the row of buttons under those, and a save error under that.</summary>
    private const float StatsY = 520f;
    private const float RowY = 640f;
    private const float RowGap = 36f;
    private const float StatusY = 700f;

    /// <summary>The veil comes down slowly, after the verdict sound has played.</summary>
    private static readonly TimeSpan FadeInTime = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromSeconds(0.4);

    private static readonly Color StatsColor = new(150, 140, 142);

    private readonly RunSession _session;
    private readonly bool _won;
    private readonly Func<string> _finish;
    private readonly EndingVeil _veil = new();

    /// <summary>The run's numbers. Read before FinishRun resets them.</summary>
    private readonly string _stats;

    private SpriteFont _detailFont;

    /// <summary>Why the save failed, or null.</summary>
    private string _status;

    /// <summary>Builds the results for a run that has just ended.</summary>
    /// <param name="session">The run, and the slot it goes back into.</param>
    /// <param name="won">Whether the player won.</param>
    /// <param name="finish">Closes the run out and saves it. Returns why the save failed, or null.</param>
    public ResultsScreen(RunSession session, bool won, Func<string> finish)
    {
        _session = session;
        _won = won;
        _finish = finish;

        IsPopup = true;
        TransitionOnTime = FadeInTime;
        TransitionOffTime = FadeOutTime;

        SaveData run = session.Run;
        int lives = Math.Max(0, run.PlayerLives);
        _stats = string.Format(CultureInfo.InvariantCulture, "CHAPTER {0}  ·  {1}  ·  {2} LEFT  ·  {3} FED",
            run.Chapter, Count(run.Round, "ROUND", "ROUNDS"), Count(lives, "LIFE", "LIVES"), Count(run.HandsFed, "HAND", "HANDS"));

        // Amber is the way forward, bone is the way out.
        var lobby = new MenuEntry(LobbyLabel, ButtonSprite.Amber);
        var title = new MenuEntry(TitleLabel, ButtonSprite.BoneWhite);

        lobby.Selected += () => Leave(toTitle: false);
        title.Selected += () => Leave(toTitle: true);

        MenuEntries.Add(lobby);
        MenuEntries.Add(title);
    }

    /// <summary>Loads the veil and fonts and lays the buttons out under the verdict.</summary>
    public override void Activate()
    {
        base.Activate();

        _veil.LoadContent(Content, ScreenManager.GraphicsDevice);
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        LayOutRow(new Vector2(BlackBoxGame.ScreenWidth / 2f, RowY), RowGap);
    }

    /// <summary>No backing out of a verdict.</summary>
    protected override void OnCancel() { }

    /// <summary>Draws the veil and verdict, the numbers, any error, then the buttons.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float alpha = TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _veil.Draw(spriteBatch, _won, alpha);
        Text.Draw(spriteBatch, _detailFont, _stats, BlackBoxGame.ScreenWidth / 2f, StatsY,
            StatsColor * alpha, Palette.Shadow * alpha, centred: true);
        if (_status is not null)
        {
            Text.Draw(spriteBatch, _detailFont, _status, BlackBoxGame.ScreenWidth / 2f, StatusY,
                ButtonSprite.EmberRed * alpha, Palette.Shadow * alpha, centred: true);
        }
        spriteBatch.End();

        base.Draw(gameTime);
    }

    /// <summary>For the proof run, since it can't press keys: RETURN TO THE LOBBY.</summary>
    internal void ProofReturnToLobby() => Leave(toTitle: false);

    /// <summary>A number and its word, so it says 1 ROUND and not 1 ROUNDS.</summary>
    private static string Count(int number, string one, string many) =>
        number.ToString(CultureInfo.InvariantCulture) + " " + (number == 1 ? one : many);

    /// <summary>Closes the run out, saves it, and goes to the lobby or the title. If the save fails it stays here so you can try again.</summary>
    /// <param name="toTitle">True for the title, false for the lobby.</param>
    private void Leave(bool toTitle)
    {
        string error = _finish();
        if (error is not null)
        {
            _status = "COULD NOT SAVE -- " + error;
            return;
        }

        if (toTitle) LoadingScreen.Load(ScreenManager, null, new BoxBackgroundScreen(), new TitleScreen());
        else LoadingScreen.Load(ScreenManager, null, new LobbyScreen(_session));
    }
}
