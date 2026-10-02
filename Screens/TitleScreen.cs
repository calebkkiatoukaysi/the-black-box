using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The title, with START GAME, OPTIONS and EXIT under the box. The sample's MainMenuScreen.
/// </summary>
/// <remarks>Escape on the title closes the game, same as before the screens were split up.</remarks>
public class TitleScreen : MenuScreen
{
    private const string Title = "The Black Box";
    private const string StartLabel = "START GAME";
    private const string OptionsLabel = "OPTIONS";
    private const string ExitLabel = "EXIT";
    private const string Hint = "ARROWS  ·  ENTER  ·  ESC TO LEAVE";

    private const float TitleY = 56f;
    private const float HintY = 856f;

    /// <summary>Centre line of the row of buttons, clear of the bottom of the box, and the gap between them.</summary>
    private const float ButtonRowY = 806f;
    private const float ButtonGap = 36f;

    /// <summary>How long the title takes to come and go.</summary>
    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.5);

    // The title gets a tight deep red shadow instead of black. The serif is light, and anything wider ghosts around the strokes.
    private static readonly Color TitleColor = new(240, 235, 240);
    private static readonly Color TitleShadow = new(104, 12, 17);
    private static readonly Color HintColor = new(150, 140, 142);

    private SpriteFont _titleFont;
    private SpriteFont _detailFont;

    /// <summary>Builds the three entries.</summary>
    public TitleScreen()
    {
        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;

        // Amber is the box making an offer; red is saved for the choice that ends things; bone for the one it has no stake in.
        var start = new MenuEntry(StartLabel, ButtonSprite.Amber);
        var options = new MenuEntry(OptionsLabel, ButtonSprite.BoneWhite);
        var exit = new MenuEntry(ExitLabel, ButtonSprite.EmberRed);

        start.Selected += () => ScreenManager.AddScreen(new SaveSlotScreen());
        options.Selected += () => ScreenManager.AddScreen(new OptionsScreen());
        exit.Selected += () => ScreenManager.Game.Exit();

        MenuEntries.Add(start);
        MenuEntries.Add(options);
        MenuEntries.Add(exit);
    }

    /// <summary>Loads the fonts and the entries, and centres the row under the box.</summary>
    public override void Activate()
    {
        base.Activate();

        _titleFont = Content.Load<SpriteFont>("spectral-title");
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        LayOutRow(new Vector2(BlackBoxGame.ScreenWidth / 2f, ButtonRowY), ButtonGap);
    }

    /// <summary>Escape on the title closes the game.</summary>
    protected override void OnCancel() => ScreenManager.Game.Exit();

    /// <summary>Draws the title and the hint over the box, then the row of buttons.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float alpha = TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        Text.DrawCentered(spriteBatch, _titleFont, Title, TitleY, TitleColor * alpha, TitleShadow * alpha, Palette.ShadowOffset);
        Text.DrawCentered(spriteBatch, _detailFont, Hint, HintY, HintColor * alpha, Palette.Shadow * alpha, Palette.ShadowOffset);
        spriteBatch.End();

        base.Draw(gameTime);
    }
}
