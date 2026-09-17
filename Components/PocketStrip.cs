using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox;

/// <summary>
/// One side's three pockets, drawn as a row of plates along the near edge of the table.
/// </summary>
/// <remarks>
/// <para>
/// A pocket is a plate because a pocket is a thing the player clicks. On their turn the
/// row is live and clicking a slot plays what is in it; the rest of the time the plates are
/// dead and the row is only a record of what is being carried. The opponent's row is never
/// live -- their pockets are across the table -- and it only says how many are full unless a
/// <see cref="ItemId.Tally"/> has bought a look at what is in them.
/// </para>
/// <para>
/// Built on <see cref="ButtonSprite"/> like every other plate in the game, so a pocket
/// kindles under the cursor and sinks under a press the same way a reply does, and there is
/// nothing new for the player to learn about what clicking it means.
/// </para>
/// </remarks>
public class PocketStrip
{
    /// <summary>
    /// Size of one pocket plate, in screen pixels. Wide enough for the longest item name in
    /// the small font, and no wider, because three of them have to fit beside the box.
    /// </summary>
    private static readonly Point SlotSize = new(190, 60);

    /// <summary>Gap between plates.</summary>
    private const int Gap = 10;

    /// <summary>How far the plates sit under the title line.</summary>
    private const int TitleGap = 24;

    /// <summary>What an empty pocket says.</summary>
    private const string EmptyLabel = "EMPTY";

    /// <summary>What a full pocket says when the player is not allowed to know what is in it.</summary>
    private const string HiddenLabel = "SOMETHING";

    /// <summary>The whole row, plates and gaps.</summary>
    public static int Width => RoundRules.PocketSlots * SlotSize.X + (RoundRules.PocketSlots - 1) * Gap;

    private readonly ButtonSprite[] _slots = new ButtonSprite[RoundRules.PocketSlots];

    /// <summary>What each plate was last told to show, so it is only reset when that changes.</summary>
    private readonly string[] _shown = new string[RoundRules.PocketSlots];

    private readonly string _title;
    private readonly Color _accent;
    private readonly Vector2 _origin;

    private SpriteFont _detailFont;

    /// <summary>Whether the plates take clicks right now.</summary>
    public bool Interactive { get; private set; }

    /// <summary>Raised when a live pocket is clicked, with which one.</summary>
    public event Action<int> SlotChosen;

    /// <summary>
    /// Builds a row.
    /// </summary>
    /// <param name="title">The line over the row: whose pockets these are.</param>
    /// <param name="origin">Top-left of the title, in screen pixels.</param>
    /// <param name="accent">Colour of the light in the plates' grooves.</param>
    public PocketStrip(string title, Vector2 origin, Color accent)
    {
        _title = title;
        _origin = origin;
        _accent = accent;

        for (int i = 0; i < _slots.Length; i++)
        {
            var centre = new Vector2(
                origin.X + i * (SlotSize.X + Gap) + SlotSize.X / 2f,
                origin.Y + TitleGap + SlotSize.Y / 2f);

            _slots[i] = new ButtonSprite(EmptyLabel, centre, accent, SlotSize) { Small = true, Enabled = false };

            int slot = i;
            _slots[i].Clicked += () => SlotChosen?.Invoke(slot);
        }
    }

    /// <summary>Loads the plates and the title font.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _detailFont = content.Load<SpriteFont>("spectral-detail");
        foreach (ButtonSprite slot in _slots) slot.LoadContent(content);
    }

    /// <summary>
    /// Puts what a side is carrying onto the plates.
    /// </summary>
    /// <remarks>
    /// Safe to call every frame. A plate is only reset when what it shows changes, because
    /// resetting it every frame would kill the hover animation under the cursor.
    /// </remarks>
    /// <param name="pockets">The item ids being carried, in pocket order.</param>
    /// <param name="revealed">Whether the player is allowed to read what they are.</param>
    /// <param name="interactive">Whether clicking a full pocket plays it.</param>
    public void Show(IReadOnlyList<string> pockets, bool revealed, bool interactive)
    {
        ArgumentNullException.ThrowIfNull(pockets);

        Interactive = interactive;

        for (int i = 0; i < _slots.Length; i++)
        {
            ItemId item = default;
            bool full = i < pockets.Count && ItemCatalog.TryParse(pockets[i], out item);
            string label = !full ? EmptyLabel : revealed ? ItemCatalog.NameOf(item).ToUpperInvariant() : HiddenLabel;
            bool enabled = full && interactive;

            string key = label + (enabled ? "+" : "-");
            if (key == _shown[i]) continue;

            _shown[i] = key;
            _slots[i].Label = label;
            _slots[i].Enabled = enabled;
            _slots[i].Reset();
        }
    }

    /// <summary>Runs the plates. Only the live ones take the cursor.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        if (!Interactive) return;

        foreach (ButtonSprite slot in _slots) slot.Update(gameTime);
    }

    /// <summary>Draws the title and the row of plates.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        // The title kindles with the row: while the pockets are live it is the one line on
        // the table that tells the player they can do something other than press a button.
        Color colour = Interactive ? _accent : new Color(122, 112, 114);
        string title = Interactive ? _title + "  ·  CLICK ONE TO PLAY IT" : _title;

        spriteBatch.DrawString(_detailFont, title, _origin + new Vector2(2f, 2f), Color.Black * 0.6f,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabelShadow);
        spriteBatch.DrawString(_detailFont, title, _origin, colour,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabel);

        foreach (ButtonSprite slot in _slots) slot.Draw(gameTime, spriteBatch);
    }
}
