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
/// On the player's turn the row is live and clicking a slot plays it; otherwise the plates are dead
/// and just show what's carried. The opponent's row is never live and only says how many are full,
/// unless a Tally has revealed them. A pocket the player can read carries the item's picture too.
/// Built on <see cref="ButtonSprite"/> like every other plate.
/// </remarks>
public class PocketStrip
{
    /// <summary>Size of one pocket plate. Wide enough for a picture and the longest item name, and three have to fit beside the box.</summary>
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

    private readonly ItemSprite _items = new();
    private readonly string _title;
    private readonly Color _accent;
    private readonly Vector2 _origin;

    private SpriteFont _detailFont;

    /// <summary>Whether the plates take clicks right now.</summary>
    public bool Interactive { get; private set; }

    /// <summary>Raised when a live pocket is clicked, with which one.</summary>
    public event Action<int> SlotChosen;

    /// <summary>Builds a row.</summary>
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

    /// <summary>Loads the plates, the item pictures and the title font.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        _detailFont = content.Load<SpriteFont>("spectral-detail");
        _items.LoadContent(content);
        foreach (ButtonSprite slot in _slots) slot.LoadContent(content);
    }

    /// <summary>Puts what a side is carrying onto the plates.</summary>
    /// <remarks>Safe to call every frame. A plate is only reset when it changes, or the hover animation would never play.</remarks>
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
            bool shown = full && revealed;
            string label = !full ? EmptyLabel : shown ? ItemCatalog.NameOf(item).ToUpperInvariant() : HiddenLabel;
            bool enabled = full && interactive;

            string key = label + (enabled ? "+" : "-");
            if (key == _shown[i]) continue;

            _shown[i] = key;
            _slots[i].Label = label;
            _slots[i].Enabled = enabled;
            _slots[i].Icon = shown ? _items.Sheet : null;
            _slots[i].IconSource = ItemSprite.SourceOf(item);
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
        // The title lights up with the row, so the player can tell the pockets are live.
        Color colour = Interactive ? _accent : Palette.DimText;
        string title = Interactive ? _title + "  ·  CLICK ONE TO PLAY IT" : _title;

        spriteBatch.DrawString(_detailFont, title, _origin + Palette.ShadowOffset, Palette.Shadow,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabelShadow);
        spriteBatch.DrawString(_detailFont, title, _origin, colour,
            0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.ButtonLabel);

        foreach (ButtonSprite slot in _slots) slot.Draw(gameTime, spriteBatch);
    }
}
