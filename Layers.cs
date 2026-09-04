namespace TheBlackBox;

/// <summary>
/// Sort keys handed to <see cref="Microsoft.Xna.Framework.Graphics.SpriteBatch"/> while it is
/// running in <see cref="Microsoft.Xna.Framework.Graphics.SpriteSortMode.BackToFront"/>.
/// 1 is the far distance and 0 is pressed against the camera, so a larger number draws first.
/// </summary>
/// <remarks>
/// Depth only sorts sprites inside a single Begin/End batch. The title screen uses several
/// batches because the eye glow needs additive blending, so the batch order in
/// <see cref="BlackBoxGame.Draw"/> is the outer sort and these values are the inner one.
/// </remarks>
public static class Layers
{
    /// <summary>The slower, dimmer ash layer.</summary>
    public const float AshDriftFar = 1.00f;

    /// <summary>The faster ash layer, drifting the other way to sell the parallax.</summary>
    public const float AshDriftNear = 0.95f;

    /// <summary>The box, and the void it is built around.</summary>
    public const float Box = 0.80f;

    /// <summary>Red light bleeding out of each socket (drawn in the additive batch).</summary>
    public const float EyeGlow = 0.50f;

    /// <summary>The eye sprite, over the glow it casts.</summary>
    public const float Eye = 0.40f;

    /// <summary>The pupil, over its own eye.</summary>
    public const float Pupil = 0.35f;

    /// <summary>Ash spiralling into the mouth, in front of everything it is falling past.</summary>
    public const float Mote = 0.20f;

    /// <summary>Offset drop shadow sitting just behind a line of text.</summary>
    public const float TextShadow = 0.12f;

    /// <summary>Foreground text, the closest thing on screen.</summary>
    public const float Text = 0.10f;
}
