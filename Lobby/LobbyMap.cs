using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// The lobby itself: the floor and walls as a grid of tiles, the furniture, the lamps, and where
/// everyone and everything starts. Everything about the room's layout is in this one file.
/// </summary>
/// <remarks>
/// The map is ASCII so I can see the room while editing it. Positions are in art pixels (one
/// pixel of the tile art), which the constructor scales up to world pixels, the same 3x the
/// figures are drawn at. The room is 768x480 art pixels: 2304x1440 on screen, bigger than the
/// window both ways, which is what the camera is for.
/// </remarks>
public class LobbyMap
{
    /// <summary>One tile of lobby-tiles.png, in art pixels, and how many sit across a row of the sheet.</summary>
    public const int TileSize = 16;
    private const int SheetColumns = 8;

    /// <summary>How far the art is blown up. The same as the figures, or nothing would line up.</summary>
    public const float Scale = Characters.WalkerSprite.Scale;

    /// <summary>One tile on screen.</summary>
    public const int TileWorld = (int)(TileSize * Scale);

    /// <summary>How dark the room is away from every lamp, and how big a texel of the dark is, in art pixels.</summary>
    private const float Darkness = 0.86f;
    private const int ShadeCell = 4;
    private static readonly Color ShadeColor = new(5, 4, 9);

    // The room. # is wall seen from above and ^ is its lit edge; p, w, t, v, s and d are the face
    // of the north wall (pipe, plain panels with a seam every fourth tile, tally marks, vent,
    // skirting, damp skirting); _ is the floor in
    // the wall's shadow, . is floor, o is a drain, h is the paint in front of the box's door.
    private static readonly string[] Rows =
    {
        "################################################",
        "##^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^##",
        "##pppppppppppppppppppppppppppppppppppppppppppp##",
        "##wwwwwtwwwwwwvwwwwwwwwwwwwwwwwwwwvwwwwwwtwwww##",
        "##ssssdssssssssssssssssssssssssssssssssdssssss##",
        "##___________________hhhhhh___________________##",
        "##...................hhhhhh...................##",
        "##...................hhhhhh...................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##.................................o..........##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##..........o.................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "##............................o...............##",
        "##......o.....................................##",
        "##............................................##",
        "##............................................##",
        "##............................................##",
        "################################################",
        "################################################",
    };

    /// <summary>The floor tiles a plain '.' can turn out to be, and how often: three clean ones in turn so no pattern shows, some worn, the odd crack.</summary>
    private static readonly int[] FloorVariety = { 0, 0, 0, 0, 17, 17, 17, 17, 18, 18, 18, 18, 1, 1, 2, 3, 7 };

    private readonly int[,] _tiles;
    private readonly List<BoundingRectangle> _solids = new();

    private Texture2D _sheet;
    private Texture2D _shade;

    /// <summary>The whole room, in world pixels.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Everything nobody can walk through: the walls, as runs of tiles.</summary>
    public IReadOnlyList<BoundingRectangle> Walls => _solids;

    /// <summary>Every prop, in the order they are listed.</summary>
    public IReadOnlyList<LobbyProp> Props { get; }

    /// <summary>The door to the box. Also in <see cref="Props"/>.</summary>
    public LobbyProp Door { get; }

    /// <summary>Where the player starts, in world pixels, facing up the room toward the door.</summary>
    public Vector2 Spawn { get; } = new Vector2(384f, 440f) * Scale;

    /// <summary>Walking into this, once the door is open, is going through it. The strip of paint right under it.</summary>
    public BoundingRectangle Threshold { get; } = new(372f * Scale, 82f * Scale, 24f * Scale, 6f * Scale);

    /// <summary>Where the two people waiting in here stand, in world pixels: Serenity, and the other conscript.</summary>
    public Vector2 SerenitySpot { get; } = new Vector2(150f, 300f) * Scale;
    public Vector2 ConscriptSpot { get; } = new Vector2(258f, 270f) * Scale;

    /// <summary>The spots things can be left on the floor, by name. The save remembers which have been emptied.</summary>
    public IReadOnlyDictionary<string, Vector2> ItemSpots { get; } = new Dictionary<string, Vector2>
    {
        ["lockers"] = new Vector2(110f, 118f) * Scale,
        ["dorm"] = new Vector2(186f, 368f) * Scale,
        ["pillar"] = new Vector2(478f, 306f) * Scale,
        ["crates"] = new Vector2(650f, 362f) * Scale,
    };

    /// <summary>What is left on the floor in chapter one, by spot: two worth having and two that are not.</summary>
    /// <remarks>Three pockets and four things, so taking everything is not an option. Nothing that takes a life; the table leans the player's way enough already.</remarks>
    private static readonly Dictionary<string, ItemId> ChapterOneItems = new()
    {
        ["lockers"] = ItemId.Lens,
        ["dorm"] = ItemId.Cinder,
        ["pillar"] = ItemId.AshVeil,
        ["crates"] = ItemId.SpentShell,
    };

    /// <summary>What is left on the floor from chapter two on.</summary>
    private static readonly Dictionary<string, ItemId> LaterItems = new()
    {
        ["lockers"] = ItemId.Tally,
        ["dorm"] = ItemId.Tourniquet,
        ["pillar"] = ItemId.Cinder,
        ["crates"] = ItemId.MarkedDeck,
    };

    /// <summary>What is on the floor for a chapter, by spot.</summary>
    /// <param name="chapter">The chapter the run is on.</param>
    public static IReadOnlyDictionary<string, ItemId> ItemsFor(int chapter) => chapter <= 1 ? ChapterOneItems : LaterItems;

    /// <summary>The lamps: where each pool of light is on the floor, how far it reaches, and how bright, all in art pixels.</summary>
    private static readonly (Vector2 Centre, float Reach, float Strength)[] Lamps =
    {
        (new Vector2(384f, 118f), 118f, 1.00f),
        (new Vector2(124f, 236f), 128f, 0.95f),
        (new Vector2(612f, 236f), 118f, 0.90f),
        (new Vector2(236f, 404f), 112f, 0.85f),
        (new Vector2(540f, 404f), 112f, 0.85f),
        (new Vector2(384f, 300f), 86f, 0.55f),
    };

    /// <summary>Builds the room: reads the map, finds the walls, and places the furniture.</summary>
    public LobbyMap()
    {
        int height = Rows.Length, width = Rows[0].Length;
        _tiles = new int[width, height];

        for (int y = 0; y < height; y++)
        {
            if (Rows[y].Length != width)
                throw new InvalidOperationException($"Row {y} of the lobby is {Rows[y].Length} tiles; the rest are {width}.");

            int runStart = -1;
            for (int x = 0; x <= width; x++)
            {
                char c = x < width ? Rows[y][x] : '.';
                if (x < width) _tiles[x, y] = TileFor(c, x, y);

                // Solid tiles are merged into one rectangle per run, so there are a few dozen walls instead of hundreds.
                bool solid = x < width && IsSolid(c);
                if (solid && runStart < 0) runStart = x;
                if (!solid && runStart >= 0)
                {
                    _solids.Add(new BoundingRectangle(runStart * TileWorld, y * TileWorld, (x - runStart) * TileWorld, TileWorld));
                    runStart = -1;
                }
            }
        }

        Bounds = new Rectangle(0, 0, width * TileWorld, height * TileWorld);

        var props = new List<LobbyProp>();

        // Lockers both sides of the door, backed onto the wall.
        foreach (float x in new[] { 42f, 58f, 74f, 90f, 618f, 634f, 650f, 666f })
            props.Add(new LobbyProp("locker", new Vector2(x, 84f), PropPlacement.Floor, new Point(16, 6)));

        // The dormitory end: cots in rows.
        foreach (Vector2 at in new[] { new Vector2(66f, 170f), new Vector2(66f, 236f), new Vector2(66f, 302f), new Vector2(138f, 170f) })
            props.Add(new LobbyProp("cot", at, PropPlacement.Floor, new Point(34, 12)));

        // The middle: a bench, a chair, and the columns holding the ceiling up.
        props.Add(new LobbyProp("bench", new Vector2(300f, 250f), PropPlacement.Floor, new Point(42, 6)));
        props.Add(new LobbyProp("chair", new Vector2(350f, 248f), PropPlacement.Floor, new Point(14, 5)));
        foreach (Vector2 at in new[] { new Vector2(232f, 190f), new Vector2(536f, 190f), new Vector2(232f, 370f), new Vector2(536f, 370f) })
            props.Add(new LobbyProp("pillar", at, PropPlacement.Floor, new Point(16, 8)));

        // The other end: crates, a bucket, the sink.
        props.Add(new LobbyProp("crates", new Vector2(692f, 300f), PropPlacement.Floor, new Point(22, 10)));
        props.Add(new LobbyProp("crates", new Vector2(682f, 432f), PropPlacement.Floor, new Point(22, 10)));
        props.Add(new LobbyProp("bucket", new Vector2(724f, 104f), PropPlacement.Floor, new Point(12, 5)));
        props.Add(new LobbyProp("sink", new Vector2(706f, 80f), PropPlacement.Wall));

        // Two cameras watching the room, and the door with its lamp.
        props.Add(new LobbyProp("camera", new Vector2(156f, 46f), PropPlacement.Wall, frames: 2));
        props.Add(new LobbyProp("camera", new Vector2(612f, 46f), PropPlacement.Wall, frames: 2));
        Door = new LobbyProp("arena-door", new Vector2(384f, 81f), PropPlacement.Wall, frames: 3);
        props.Add(Door);

        Props = props;
    }

    /// <summary>Loads the tiles and the props, and lays the dark over the room.</summary>
    /// <param name="content">The content manager to load with.</param>
    /// <param name="graphicsDevice">The device, for the dark, which is made rather than loaded.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _sheet = content.Load<Texture2D>("lobby-tiles");
        foreach (LobbyProp prop in Props) prop.LoadContent(content);

        _shade = BuildShade(graphicsDevice);
    }

    /// <summary>Lets go of the dark, which the content manager does not own.</summary>
    public void Unload() => _shade?.Dispose();

    /// <summary>Draws the tiles that are on screen. Floor at the very back, the north wall just in front of it, and the south wall in front of everyone.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in world space. Point-sampled, back to front.</param>
    /// <param name="view">The part of the room on screen, in world pixels.</param>
    public void DrawTiles(SpriteBatch spriteBatch, Rectangle view)
    {
        int x0 = Math.Max(0, view.Left / TileWorld), x1 = Math.Min(_tiles.GetLength(0) - 1, view.Right / TileWorld);
        int y0 = Math.Max(0, view.Top / TileWorld), y1 = Math.Min(_tiles.GetLength(1) - 1, view.Bottom / TileWorld);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                int tile = _tiles[x, y];
                var source = new Rectangle(tile % SheetColumns * TileSize, tile / SheetColumns * TileSize, TileSize, TileSize);
                var destination = new Rectangle(x * TileWorld, y * TileWorld, TileWorld, TileWorld);

                // The wall along the bottom is between the viewer and the room, so it covers anyone standing against it.
                float layer = Rows[y][x] == '#' && y > Rows.Length / 2 ? Layers.WorldFront : Layers.Room;
                spriteBatch.Draw(_sheet, destination, source, Color.White, 0f, Vector2.Zero, SpriteEffects.None, layer);
            }
        }
    }

    /// <summary>Lays the dark over the whole room, with holes where the lamps are. Its own batch, sampled smooth.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in world space, linear-sampled.</param>
    public void DrawShade(SpriteBatch spriteBatch) =>
        spriteBatch.Draw(_shade, Bounds, Color.White);

    /// <summary>How lit a point of the room is, 0 for dark to 1 for under a lamp.</summary>
    /// <param name="world">The point, in world pixels.</param>
    private static float LightAt(Vector2 world)
    {
        Vector2 art = world / Scale;
        float light = 0f;

        foreach ((Vector2 centre, float reach, float strength) in Lamps)
        {
            // Pools are a little wider than deep: the view looks down at the floor at an angle.
            float d = Vector2.Distance(new Vector2(art.X, centre.Y + (art.Y - centre.Y) * 1.35f), centre) / reach;
            light += strength / (1f + d * d * d * 2.2f);
        }

        return MathHelper.Clamp(light, 0f, 1f);
    }

    /// <summary>Builds the dark: one texel every few art pixels, stretched over the room and sampled smooth.</summary>
    private Texture2D BuildShade(GraphicsDevice graphicsDevice)
    {
        int width = Bounds.Width / (int)Scale / ShadeCell, height = Bounds.Height / (int)Scale / ShadeCell;
        var texels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var world = new Vector2((x + 0.5f) * ShadeCell * Scale, (y + 0.5f) * ShadeCell * Scale);
                float alpha = Darkness * (1f - LightAt(world));

                // Premultiplied, the way SpriteBatch's alpha blend wants it.
                texels[y * width + x] = ShadeColor * alpha;
            }
        }

        var shade = new Texture2D(graphicsDevice, width, height);
        shade.SetData(texels);
        return shade;
    }

    /// <summary>Which tile in the sheet a map character is. Plain floor picks a variant off its position, so it is the same every time.</summary>
    private static int TileFor(char c, int x, int y) => c switch
    {
        '#' => 8,
        '^' => 9,
        'p' => 10,
        'w' => x % 4 == 0 ? 11 : 16,
        't' => 12,
        'v' => 13,
        's' => 14,
        'd' => 15,
        '_' => 6,
        'o' => 4,
        'h' => 5,
        _ => FloorVariety[(int)((uint)(x * 73856093 ^ y * 19349663) % (uint)FloorVariety.Length)],
    };

    /// <summary>Whether a map character is something nobody can stand on.</summary>
    private static bool IsSolid(char c) => c is '#' or '^' or 'p' or 'w' or 't' or 'v' or 's' or 'd';
}
