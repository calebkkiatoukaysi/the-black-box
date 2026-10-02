using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Audio;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The box and the ash behind the title and every menu on top of it. This is the sample's BackgroundScreen.
/// </summary>
/// <remarks>
/// It only transitions off when it's told to leave, not when something covers it, so the box stays
/// up behind the options, the save form and the customization screen.
/// </remarks>
public class BoxBackgroundScreen : GameScreen
{
    /// <summary>How long the box takes to fade in and out.</summary>
    private static readonly TimeSpan FadeTime = TimeSpan.FromSeconds(0.6);

    private BoxScene _scene;

    /// <summary>Sets the fades.</summary>
    public BoxBackgroundScreen()
    {
        TransitionOnTime = FadeTime;
        TransitionOffTime = FadeTime;
    }

    /// <summary>Puts the box back in the middle of the screen and starts the title music.</summary>
    public override void Activate()
    {
        _scene = ScreenManager.Game.Services.GetService<BoxScene>();
        _scene.PlaceOnTitle();

        ScreenManager.Game.Services.GetService<AudioManager>().PlaySong(Track.Title);
    }

    /// <summary>Runs the fades, but never counts as covered so it stays drawn under the menus.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Ignored on purpose (see the class remarks).</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen) =>
        base.Update(gameTime, otherScreenHasFocus, false);

    /// <summary>Draws the sky, the box, the eye glow and the ash. Takes three batches.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        BlackBoxSprite box = _scene.Box;

        // 1. The sky and the box body. Point sampling keeps the pixel art crisp.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _scene.DrawSky(gameTime, spriteBatch);
        box.DrawBody(gameTime, spriteBatch);
        spriteBatch.End();

        // 2. The eye glow, additive so the light spills onto the rim. Linear sampling keeps it soft.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BoxScene.PremultipliedAdditive, SamplerState.LinearClamp);
        box.DrawEyeGlow(gameTime, spriteBatch);
        spriteBatch.End();

        // 3. The eyes over the glow, and the ash in front of everything.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        box.DrawEyes(gameTime, spriteBatch);
        _scene.DrawAsh(gameTime, spriteBatch);
        spriteBatch.End();

        ScreenManager.FadeBackBufferToBlack(TransitionPosition);
    }
}
