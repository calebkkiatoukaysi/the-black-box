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
    /// <summary>The room the game is played in: wall, lamp and table. Behind everything.</summary>
    public const float Room = 1.00f;

    /// <summary>The slower, dimmer ash layer.</summary>
    public const float AshDriftFar = 1.00f;

    /// <summary>The faster ash layer, drifting the other way to sell the parallax.</summary>
    public const float AshDriftNear = 0.95f;

    /// <summary>The box, and the void it is built around.</summary>
    public const float Box = 0.80f;

    /// <summary>The figure across the table, in front of the wall and behind the box.</summary>
    public const float Opponent = 0.92f;

    /// <summary>An eye sitting in its socket, over the face it is set into.</summary>
    public const float OpponentEye = 0.91f;

    /// <summary>The pool of dark the box sits in, on the table and under it.</summary>
    public const float BoxShadow = 0.86f;

    /// <summary>The player's own hand, reaching past the box into the opening.</summary>
    public const float Hand = 0.30f;

    /// <summary>Red light bleeding out of each socket (drawn in the additive batch).</summary>
    public const float EyeGlow = 0.50f;

    /// <summary>The eye sprite, over the glow it casts.</summary>
    public const float Eye = 0.40f;

    /// <summary>The pupil, over its own eye.</summary>
    public const float Pupil = 0.35f;

    /// <summary>Ash spiralling into the mouth, in front of everything it is falling past.</summary>
    public const float Mote = 0.20f;

    /// <summary>
    /// The dimming pane a form is laid over. It is drawn in the form's own batch, behind
    /// everything else in it and in front of the whole title screen under it.
    /// </summary>
    public const float Veil = 0.60f;

    /// <summary>The concrete slab a form is built on, over the veil.</summary>
    public const float PanelPlate = 0.50f;

    /// <summary>The concrete plate of a button.</summary>
    public const float ButtonPlate = 0.18f;

    /// <summary>The light bleeding out of a button's groove, over its own plate.</summary>
    public const float ButtonAccent = 0.17f;

    /// <summary>Offset drop shadow behind a button's label.</summary>
    public const float ButtonLabelShadow = 0.16f;

    /// <summary>A button's label, the closest part of the button.</summary>
    public const float ButtonLabel = 0.15f;

    /// <summary>The dark plate the dialogue is written on, behind its own text.</summary>
    public const float DialoguePlate = 0.14f;

    /// <summary>Offset drop shadow sitting just behind a line of text.</summary>
    public const float TextShadow = 0.12f;

    /// <summary>Foreground text, the closest thing on screen.</summary>
    public const float Text = 0.10f;
}
