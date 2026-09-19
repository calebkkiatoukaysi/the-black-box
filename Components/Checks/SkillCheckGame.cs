using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Checks;

/// <summary>
/// Base class for the little minigames that run before an item resolves.
/// </summary>
/// <remarks>
/// A check runs for a few seconds and ends with an efficiency from 0 to 1. The round code
/// never knows a check ran, it just gets that number, and 1 means the item works as normal.
/// </remarks>
public abstract class SkillCheckGame
{
    /// <summary>The player's hands, read once a frame.</summary>
    protected readonly PlayerInput Input = new();

    /// <summary>Which check this is.</summary>
    public abstract SkillCheck Kind { get; }

    /// <summary>What the plate says while the check is running.</summary>
    public abstract string Prompt { get; }

    /// <summary>Whether the check has ended and <see cref="Efficiency"/> can be read.</summary>
    public bool IsDone { get; protected set; }

    /// <summary>How well it went, from 0 to 1. Meaningful once <see cref="IsDone"/>.</summary>
    public float Efficiency { get; protected set; }

    /// <summary>Loads whatever the check draws with.</summary>
    /// <param name="content">The content manager to load through.</param>
    public abstract void LoadContent(ContentManager content);

    /// <summary>Starts the check from the beginning.</summary>
    /// <param name="random">The run's random, for where things start.</param>
    public virtual void Begin(Random random)
    {
        IsDone = false;
        Efficiency = 0f;
        Input.Reset();
    }

    /// <summary>Runs one frame of the check.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public abstract void Update(GameTime gameTime);

    /// <summary>Draws the check onto the table.</summary>
    /// <param name="gameTime">The GameTime.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public abstract void Draw(GameTime gameTime, SpriteBatch spriteBatch);

    /// <summary>Ends the check with a result.</summary>
    /// <param name="efficiency">How well it went, clamped to 0..1.</param>
    protected void Finish(float efficiency)
    {
        Efficiency = Math.Clamp(efficiency, 0f, 1f);
        IsDone = true;
    }
}
