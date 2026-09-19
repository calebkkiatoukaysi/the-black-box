using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// The four ways to answer, and the clock they have to be answered inside.
/// </summary>
/// <remarks>
/// Two columns either side of the box instead of a ring, because a ring would sit over the box or
/// the opponent. Corner order is <see cref="Tone"/> order, so the player always knows what a
/// position costs before reading the label.
/// </remarks>
public class DialogueWheel
{
    /// <summary>Where the two columns sit, in screen pixels.</summary>
    private const float LeftColumnX = 300f;

    /// <summary>The other column. See <see cref="LeftColumnX"/>.</summary>
    private const float RightColumnX = 1300f;

    /// <summary>The upper row of plates. It straddles the table lip, clear of the pockets below.</summary>
    private const float TopRowY = 578f;

    /// <summary>The lower row of plates.</summary>
    private const float BottomRowY = 668f;

    /// <summary>How wide a plate is, so a long paraphrase does not reach the box.</summary>
    private static readonly Point PlateSize = new(420, 84);

    /// <summary>Where the patience bar sits: on the dialogue plate, under the name, inside its padding.</summary>
    private static readonly Rectangle PatienceBar = new(
        DialoguePlate.Left + DialoguePlate.Padding, DialoguePlate.PatienceY, DialoguePlate.Width - 2 * DialoguePlate.Padding, 8);

    /// <summary>What the plate says instead of a tone when the opponent will not hear it.</summary>
    private const string LockedNote = "THEY ARE NOT OPEN ENOUGH FOR THAT";

    /// <summary>The colour each tone lights up in, indexed by <see cref="Tone"/>. Probing is a cold brass; the rest are the box's own colours.</summary>
    private static readonly Color[] ToneAccents =
    {
        ButtonSprite.Amber,
        ButtonSprite.BoneWhite,
        new(198, 172, 128),
        ButtonSprite.EmberRed,
    };

    /// <summary>The empty part of the patience bar, and how much patience is left when the bar turns red.</summary>
    private static readonly Color PatienceTrack = new(28, 22, 26);
    private const float PatienceWarning = 0.35f;

    private readonly ButtonSprite[] _plates = new ButtonSprite[DialogueScript.WheelSize];

    private Texture2D _pixel;

    /// <summary>Whether the wheel is on screen and taking input.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Raised when the player commits to one of the four, with which corner it was.</summary>
    public event Action<int> Chosen;

    /// <summary>Builds the four plates in tone order.</summary>
    public DialogueWheel()
    {
        for (int i = 0; i < _plates.Length; i++)
        {
            // Corner 0 and 1 are the left column, 2 and 3 the right.
            float x = i < 2 ? LeftColumnX : RightColumnX;
            float y = i % 2 == 0 ? TopRowY : BottomRowY;

            _plates[i] = new ButtonSprite(string.Empty, new Vector2(x, y), ToneAccents[i], PlateSize);

            int corner = i;
            _plates[i].Clicked += () => Choose(corner);
        }
    }

    /// <summary>Loads the plates and the one pixel the patience bar is drawn from.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the bar.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        foreach (ButtonSprite plate in _plates) plate.LoadContent(content);

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Puts one beat's four replies onto the plates.</summary>
    /// <param name="node">The beat being answered.</param>
    /// <param name="disposition">What the opponent thinks of the player, which locks some replies.</param>
    public void Show(DialogueNode node, Disposition disposition)
    {
        ArgumentNullException.ThrowIfNull(node);

        for (int i = 0; i < _plates.Length; i++)
        {
            DialogueOption option = node.Options[i];
            bool open = option.IsOpen(disposition);

            _plates[i].Label = option.Label;

            // A locked plate says why, so the player knows the door is there and shut.
            _plates[i].Sublabel = open ? ToneName(option.Tone) : LockedNote;
            _plates[i].Enabled = open;
            _plates[i].Reset();
        }

        IsOpen = true;
    }

    /// <summary>Takes the wheel off screen, for when there is nothing left to answer.</summary>
    public void Hide()
    {
        IsOpen = false;
        foreach (ButtonSprite plate in _plates) plate.Reset();
    }

    /// <summary>Runs the four plates.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!IsOpen) return;

        foreach (ButtonSprite plate in _plates) plate.Update(gameTime);
    }

    /// <summary>Draws the plates, and the bar showing how much of the box's patience is left.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="patience">How much time is left, from 1 to 0.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, float patience)
    {
        if (!IsOpen) return;

        DrawPatience(spriteBatch, patience);

        foreach (ButtonSprite plate in _plates) plate.Draw(gameTime, spriteBatch);
    }

    /// <summary>Draws the patience bar.</summary>
    /// <remarks>It drains from both ends toward the middle. Sideways read as a progress bar, and this isn't progress.</remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    /// <param name="patience">How much time is left, from 1 to 0.</param>
    private void DrawPatience(SpriteBatch spriteBatch, float patience)
    {
        patience = Math.Clamp(patience, 0f, 1f);

        spriteBatch.Draw(_pixel, PatienceBar, null, PatienceTrack,
            0f, Vector2.Zero, SpriteEffects.None, Layers.ButtonPlate);

        int width = (int)MathF.Round(PatienceBar.Width * patience);
        if (width <= 0) return;

        var remaining = new Rectangle(
            PatienceBar.Center.X - width / 2, PatienceBar.Y, width, PatienceBar.Height);

        // Bone while there is time, red once there isn't.
        Color colour = Color.Lerp(ButtonSprite.EmberRed, ButtonSprite.BoneWhite,
            MathF.Min(1f, patience / PatienceWarning));

        spriteBatch.Draw(_pixel, remaining, null, colour,
            0f, Vector2.Zero, SpriteEffects.None, Layers.ButtonAccent);
    }

    /// <summary>Raises <see cref="Chosen"/> for a plate that was clicked.</summary>
    /// <param name="corner">Which corner, from 0 to <see cref="DialogueScript.WheelSize"/> - 1.</param>
    private void Choose(int corner)
    {
        if (!IsOpen) return;

        Chosen?.Invoke(corner);
    }

    /// <summary>What a tone is called on a plate.</summary>
    /// <param name="tone">The tone the reply carries.</param>
    private static string ToneName(Tone tone) => tone switch
    {
        Tone.Warm => "WARM",
        Tone.Level => "LEVEL",
        Tone.Probing => "PROBING",
        Tone.Cutting => "CUTTING",
        _ => string.Empty,
    };
}
