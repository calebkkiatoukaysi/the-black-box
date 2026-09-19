namespace TheBlackBox;

/// <summary>
/// Layer depths for SpriteBatch in BackToFront mode. 1 is the back, 0 is the front.
/// </summary>
/// <remarks>
/// Depth only sorts within one Begin/End. The eye glow needs its own additive batch, so the
/// batch order in BlackBoxGame.Draw sorts first and these sort inside each batch.
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

    /// <summary>The pool of dark the box sits in, on the table and under it.</summary>
    public const float BoxShadow = 0.86f;

    /// <summary>The player's own hand, reaching past the box into the opening.</summary>
    public const float Hand = 0.30f;

    /// <summary>The box's jaws in the close-up, over its eyes and behind the hand going in.</summary>
    public const float Lid = 0.33f;

    /// <summary>The tag the box pays with. In front of the catching hand so it lies in the palm when caught.</summary>
    public const float Token = 0.29f;

    /// <summary>The sight a check puts over its target.</summary>
    public const float Sight = 0.27f;

    /// <summary>The ember in the steady check, over the groove it sits in.</summary>
    public const float Marker = 0.28f;

    /// <summary>The groove the ember sits in.</summary>
    public const float Bar = 0.31f;

    /// <summary>Red light bleeding out of each socket (drawn in the additive batch).</summary>
    public const float EyeGlow = 0.50f;

    /// <summary>The eye sprite, over the glow it casts.</summary>
    public const float Eye = 0.40f;

    /// <summary>The pupil, over its own eye.</summary>
    public const float Pupil = 0.35f;

    /// <summary>Ash spiralling into the mouth.</summary>
    public const float Mote = 0.20f;

    /// <summary>The dimming pane a form is laid over. Back of the form's batch.</summary>
    public const float Veil = 0.60f;

    /// <summary>The concrete slab a form is built on, over the veil.</summary>
    public const float PanelPlate = 0.50f;

    /// <summary>The concrete plate of a button.</summary>
    public const float ButtonPlate = 0.18f;

    /// <summary>The light bleeding out of a button's groove, over its own plate.</summary>
    public const float ButtonAccent = 0.17f;

    /// <summary>Offset drop shadow behind a button's label.</summary>
    public const float ButtonLabelShadow = 0.16f;

    /// <summary>A button's label.</summary>
    public const float ButtonLabel = 0.15f;

    /// <summary>The dark plate the dialogue is written on.</summary>
    public const float DialoguePlate = 0.14f;

    /// <summary>Offset drop shadow sitting just behind a line of text.</summary>
    public const float TextShadow = 0.12f;

    /// <summary>Foreground text.</summary>
    public const float Text = 0.10f;
}
