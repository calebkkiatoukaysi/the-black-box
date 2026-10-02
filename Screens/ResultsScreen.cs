using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The end of a run: the veil comes down over the table with the verdict on it, how the run went,
/// and the two ways off the table.
/// </summary>
/// <remarks>
/// Either way out turns the page the same way LEAVE THE TABLE used to: the run is written back
/// ready to play again (next chapter if the player got up, the same table if they did not).
/// Escape does nothing here. The table is over, and it is waiting on a choice.
/// </remarks>
public class ResultsScreen : MenuScreen
{
    private const string LobbyLabel = "RETURN TO THE LOBBY";
    private const string TitleLabel = "RETURN TO TITLE";

    /// <summary>Where the run's numbers go under the verdict, the row of plates under them, and any save error under that.</summary>
    private const float StatsY = 520f;
    private const float RowY = 640f;
    private const float RowGap = 36f;
    private const float StatusY = 700f;

    /// <summary>The veil is slow to come down. The verdict has been heard first.</summary>
    private static readonly TimeSpan FadeInTime = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromSeconds(0.4);

    private static readonly Color StatsColor = new(150, 140, 142);

    private readonly RunSession _session;
    private readonly bool _won;
    private readonly Func<string> _finish;
    private readonly EndingVeil _veil = new();

    /// <summary>The run's numbers, read before the run is closed out and they are reset.</summary>
    private readonly string _stats;

    private SpriteFont _detailFont;

    /// <summary>Why writing the run back failed, or null.</summary>
    private string _status;

    /// <summary>Builds the results for a run that has just ended.</summary>
    /// <param name="session">The run, and the slot it goes back into.</param>
    /// <param name="won">Whether the player is still standing.</param>
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

        // Amber is the way forward, bone the way out.
        var lobby = new MenuEntry(LobbyLabel, ButtonSprite.Amber);
        var title = new MenuEntry(TitleLabel, ButtonSprite.BoneWhite);

        lobby.Selected += () => Leave(toTitle: false);
        title.Selected += () => Leave(toTitle: true);

        MenuEntries.Add(lobby);
        MenuEntries.Add(title);
    }

    /// <summary>Loads the veil and the fonts, and lays the row out under the verdict.</summary>
    public override void Activate()
    {
        base.Activate();

        _veil.LoadContent(Content, ScreenManager.GraphicsDevice);
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        LayOutRow(new Vector2(BlackBoxGame.ScreenWidth / 2f, RowY), RowGap);
    }

    /// <summary>There is no backing out of a verdict.</summary>
    protected override void OnCancel() { }

    /// <summary>Draws the veil and the verdict, the numbers, any error, then the plates.</summary>
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

    /// <summary>For the proof run: RETURN TO THE LOBBY, without a key to press it.</summary>
    internal void ProofReturnToLobby() => Leave(toTitle: false);

    /// <summary>A number and the word for it, singular or plural.</summary>
    private static string Count(int number, string one, string many) =>
        number.ToString(CultureInfo.InvariantCulture) + " " + (number == 1 ? one : many);

    /// <summary>Closes the run out, saves it, and goes to the lobby or the title. A failed save stays here to be tried again.</summary>
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
