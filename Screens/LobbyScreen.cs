using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Audio;
using TheBlackBox.Characters;
using TheBlackBox.Collisions;
using TheBlackBox.Lobby;
using TheBlackBox.StateManagement;

namespace TheBlackBox.Screens;

/// <summary>
/// The lobby: the room the players wait in between tables. Walk about, talk to whoever is
/// waiting, pick up what has been left on the floor, and go through the red door when the
/// one who is seated across from you has asked you to.
/// </summary>
/// <remarks>
/// Seen from above, in a room bigger than the window, with a camera that follows the player and
/// stops at the walls. The door stays shut until the chapter's opponent has been spoken to; that
/// is the challenge. Through the door is the table.
/// </remarks>
public partial class LobbyScreen : GameScreen
{
    /// <summary>How fast the player walks, in world pixels a second.</summary>
    private const float WalkSpeed = 230f;

    /// <summary>The player's feet on the floor, wide and deep, in world pixels. What they collide with.</summary>
    private static readonly Vector2 FeetSize = new(36f, 15f);

    /// <summary>How close to the door's foot the player has to be to try it.</summary>
    private const float DoorReach = 170f;

    /// <summary>How long the door stands open, and the room fades, before the table comes up.</summary>
    private const float DoorOpenSeconds = 0.5f;
    private static readonly TimeSpan FadeInTime = TimeSpan.FromSeconds(0.8);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromSeconds(1.0);

    /// <summary>The blink of the cameras' lights, on and off, in seconds.</summary>
    private const float CameraBlink = 0.9f;

    /// <summary>Footsteps: how loud, how far the pitch wanders, and how far left and right they are panned in turn.</summary>
    private const float StepVolume = 0.8f;
    private const float StepPitch = 0.1f;
    private const float StepPan = 0.12f;

    /// <summary>The voices: how high each one's blip is.</summary>
    private const float SerenityPitch = 0.12f;
    private const float ConscriptPitch = 0f;
    private const float BoxPitch = -0.55f;

    /// <summary>How the portraits are blown up in the dialogue box, and the part of a walking sheet used as a portrait.</summary>
    private const float PortraitScale = 2f;
    private const float WalkerPortraitScale = 6f;
    private static readonly Rectangle WalkerBust = new(0, 0, WalkerSprite.FrameWidth, 36);

    /// <summary>The red light over the door when it is open, and how it breathes.</summary>
    private static readonly Point DoorGlowSize = new(240, 150);
    private static readonly Vector2 DoorGlowOffset = new(0f, -150f);
    private static readonly Color DoorGlowColor = new(210, 36, 24);
    private const float DoorGlowBreath = 2.2f;

    /// <summary>Where the words on the heading go, and how long a message stays up.</summary>
    private const float HeadingY = 14f;
    private const float ObjectiveY = 42f;
    private const float ToastY = 96f;
    private const float ToastSeconds = 2.6f;
    private const float PromptRise = 160f;

    /// <summary>The highest a prompt may float, so one over the door never sits on top of a message.</summary>
    private const float PromptTop = 140f;
    private const float Margin = 24f;
    private static readonly Vector2 PocketsAt = new(Margin, 800f);

    private const string PauseHint = "ESC  ·  PAUSE";
    private const string WaitingObjective = "SOMEONE IN HERE IS WAITING FOR YOU.";
    private const string OpenObjective = "THE DOOR IS OPEN.";

    private readonly InputAction _interact = new(new[] { Buttons.A }, new[] { Keys.E, Keys.Space, Keys.Enter }, true);
    private readonly InputAction _pause = new(new[] { Buttons.Start, Buttons.Back }, new[] { Keys.Escape }, true);

    private readonly RunSession _session;
    private readonly SaveData _run;
    private readonly LobbyMap _map = new();
    private readonly Camera2D _camera = new(new Point(BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight));
    private readonly WalkerSprite _player = new();
    private readonly List<LobbyNpc> _npcs = new();
    private readonly List<LobbyItem> _items = new();
    private readonly DialogueBox _dialogue = new();
    private readonly CharacterPalettes _palettes = new();
    private readonly ItemSprite _itemPictures = new();
    private readonly PocketStrip _pockets = new("YOUR POCKETS", PocketsAt, ButtonSprite.BoneWhite);
    private readonly Random _random = new();

    private ContentManager _content;
    private AudioManager _audio;
    private Texture2D _playerSheet;
    private Texture2D _glow;
    private SpriteFont _detailFont;
    private SpriteFont _font;

    /// <summary>The box, as somebody to be spoken to: no picture, red name, a low voice.</summary>
    private LobbySpeaker _box;

    /// <summary>Who the box has seated across from the player this chapter.</summary>
    private string _challenger;

    /// <summary>Seconds since the door was opened to go through, or a negative number while it has not been.</summary>
    private float _entering = -1f;

    private float _clock;
    private string _toast;
    private float _toastLeft;
    private bool _stepLeft;

    /// <summary>The item the player is standing on with full pockets, so the warning is said once and not every frame.</summary>
    private LobbyItem _warnedAbout;

    /// <summary>Puts the player in the lobby with a run.</summary>
    /// <param name="session">The run, and the slot it goes back into.</param>
    public LobbyScreen(RunSession session)
    {
        _session = session;
        _run = session.Run;

        TransitionOnTime = FadeInTime;
        TransitionOffTime = FadeOutTime;
    }

    /// <summary>Loads the room and everyone in it, dresses the player, and starts the lobby's song.</summary>
    public override void Activate()
    {
        _content ??= new ContentManager(ScreenManager.Game.Services, "Content");
        _audio = ScreenManager.Game.Services.GetService<AudioManager>();
        GraphicsDevice device = ScreenManager.GraphicsDevice;

        _map.LoadContent(_content, device);
        _palettes.LoadContent(_content);
        _itemPictures.LoadContent(_content);
        _pockets.LoadContent(_content);
        _dialogue.LoadContent(_content, device, _audio);
        _glow = _content.Load<Texture2D>("glow");
        _font = _content.Load<SpriteFont>("spectral-ui");
        _detailFont = _content.Load<SpriteFont>("spectral-detail");

        // The player, in the colours they picked.
        PlayerLook look = _run.Look;
        Texture2D painted = _content.Load<Texture2D>(SheetFor(look.Who));
        _playerSheet = _palettes.Recolour(device, painted, look);
        _player.LoadContent(_content);
        _player.Sheet = _playerSheet;
        _player.Wearing = look.Wearing;
        _player.Position = _map.Spawn;
        _player.Facing = Direction.Up;
        _player.Stepped += Footstep;

        // Between chapters the seat is empty and the roster fills it, the same way the table does.
        _challenger = string.IsNullOrWhiteSpace(_run.OpponentId) ? Opponents.ForChapter(_run.Chapter).Id : _run.OpponentId;

        AddPeople(look.Who);
        AddItems();

        _box = new LobbySpeaker("THE BOX", ButtonSprite.EmberRed, null, Rectangle.Empty, 1f, BoxPitch);
        _camera.Bounds = _map.Bounds;
        _camera.SnapTo(_player.Position);

        _audio.PlaySong(Track.Lobby);
    }

    /// <summary>Unloads the room, and lets go of what was made rather than loaded.</summary>
    public override void Unload()
    {
        _playerSheet?.Dispose();
        _map.Unload();
        _content?.Unload();
    }

    /// <summary>Walking, talking, the door and the pause menu.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The input this frame.</param>
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (_entering >= 0f) return;

        bool pressed = _interact.Occurred(input) || TakeProofPress();

        if (_pause.Occurred(input))
        {
            _player.IsWalking = false;
            ScreenManager.AddScreen(new PauseMenuScreen(SaveAndLeave));
            return;
        }

        if (_dialogue.IsOpen)
        {
            _player.IsWalking = false;
            _dialogue.Update(gameTime, pressed);
            return;
        }

        Walk(gameTime, ReadMovement(input));

        if (pressed) Interact();

        PickUpItems();

        if (DoorIsOpen && _map.Threshold.CollidesWith(FeetAt(_player.Position)))
            GoThroughDoor();
    }

    /// <summary>Runs everything that moves on its own: the people, the items, the cameras, the camera, and the door.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        // Paused: nothing moves, and the player stops mid-stride rather than walking on the spot.
        if (!IsActive && _entering < 0f)
        {
            _player.IsWalking = false;
            return;
        }

        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _clock += elapsed;

        foreach (LobbyNpc npc in _npcs) npc.Update(gameTime);
        foreach (LobbyItem item in _items) item.Update(gameTime);
        _player.Update(gameTime);
        _camera.Follow(_player.Position, gameTime);

        foreach (LobbyProp prop in _map.Props)
        {
            if (prop != _map.Door && prop.Placement == PropPlacement.Wall) prop.Frame = (int)(_clock / CameraBlink) % 2;
        }
        _map.Door.Frame = _entering >= 0f ? 2 : DoorIsOpen ? 1 : 0;

        if (_toastLeft > 0f) _toastLeft -= elapsed;

        if (_entering >= 0f)
        {
            _entering += elapsed;
            if (_entering >= DoorOpenSeconds && !IsExiting)
                LoadingScreen.Load(ScreenManager, LobbyLines.ThroughTheDoor, new TableScreen(_session));
        }
    }

    /// <summary>Draws the room, the dark over it, the lights in it, and the heading, pockets and dialogue over all of that.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        Matrix world = _camera.Transform;
        float roomHeight = _map.Bounds.Height;

        // 1. The room: tiles, the wall's fittings, and everyone and everything on the floor, sorted by how far down the room they stand.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp, transformMatrix: world);
        _map.DrawTiles(spriteBatch, _camera.View);
        foreach (LobbyProp prop in _map.Props)
        {
            if (prop.Placement == PropPlacement.Floor) prop.Draw(spriteBatch, Layers.OnFloor(prop.Foot.Y, roomHeight));
            else if (prop.Placement == PropPlacement.Wall) prop.Draw(spriteBatch, Layers.WorldWall);
        }
        foreach (LobbyItem item in _items) item.Draw(spriteBatch, _glow, _itemPictures, Layers.OnFloor(item.Position.Y, roomHeight));
        foreach (LobbyNpc npc in _npcs) npc.Walker.Draw(spriteBatch, Layers.OnFloor(npc.Walker.Position.Y, roomHeight));
        _player.Draw(spriteBatch, Layers.OnFloor(_player.Position.Y, roomHeight));
        spriteBatch.End();

        // 2. The dark, with the lamps' pools cut out of it. Smooth, so the pools have soft edges.
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, transformMatrix: world);
        _map.DrawShade(spriteBatch);
        spriteBatch.End();

        // 3. The door's red lamp, once it is lit.
        if (DoorIsOpen)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BoxScene.PremultipliedAdditive, SamplerState.LinearClamp, transformMatrix: world);
            DrawDoorLight(spriteBatch);
            spriteBatch.End();
        }

        // 4. On the glass: the pockets, or the dialogue box's frame and picture when somebody is talking.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        if (_dialogue.IsOpen)
        {
            _dialogue.DrawFrame(spriteBatch, 1f);
        }
        else
        {
            _pockets.Show(_run.Banked, revealed: true, interactive: false);
            _pockets.Draw(gameTime, spriteBatch);
        }
        spriteBatch.End();

        // 5. The words: the heading, what to do, a message, a prompt, and whatever is being said.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawHeading(spriteBatch);
        DrawPrompt(spriteBatch);
        _dialogue.DrawText(spriteBatch, 1f);
        spriteBatch.End();

        ScreenManager.FadeBackBufferToBlack(TransitionPosition);
    }

    /// <summary>Whether the chapter's opponent has asked the player to the table yet.</summary>
    private bool DoorIsOpen => _run.HasFlag(ChallengedFlag);

    /// <summary>The flag that says the door is open this chapter. Per chapter, so the next one starts with it shut.</summary>
    private string ChallengedFlag => "lobby." + _run.Chapter + ".challenged";

    /// <summary>The flag that says somebody has already been spoken to this chapter.</summary>
    private string SpokenFlag(string id) => "lobby." + _run.Chapter + ".spoke." + id;

    /// <summary>The walking sheet for one of the two the player can be.</summary>
    private static string SheetFor(Conscript who) => who == Conscript.Second ? "conscript-second" : "conscript-first";

    /// <summary>Puts the two people waiting in the room: Serenity, and whichever conscript the player is not.</summary>
    /// <param name="player">Who the player is.</param>
    private void AddPeople(Conscript player)
    {
        var serenity = new WalkerSprite { Sheet = _content.Load<Texture2D>("serenity-walker") };
        Conscript otherWho = player == Conscript.First ? Conscript.Second : Conscript.First;
        Texture2D otherSheet = _content.Load<Texture2D>(SheetFor(otherWho));
        var other = new WalkerSprite { Sheet = otherSheet };

        serenity.LoadContent(_content);
        other.LoadContent(_content);

        // Her picture comes off her table sheet, the even-tempered column.
        Opponent first = Opponents.First;
        var serenityPicture = new Rectangle((int)OpponentPose.Even * first.FrameWidth, 0, first.FrameWidth, first.FrameHeight);

        _npcs.Add(new LobbyNpc(first.Id,
            new LobbySpeaker(first.Script.OpponentName, ButtonSprite.Amber, _content.Load<Texture2D>(first.Sheet), serenityPicture, PortraitScale, SerenityPitch),
            serenity, _map.SerenitySpot));

        _npcs.Add(new LobbyNpc(LobbyLines.ConscriptId,
            new LobbySpeaker(LobbyLines.ConscriptName, ButtonSprite.BoneWhite, otherSheet, WalkerBust, WalkerPortraitScale, ConscriptPitch),
            other, _map.ConscriptSpot));
    }

    /// <summary>Puts this chapter's items on the floor, minus whatever has already been picked up since the table was cleared.</summary>
    private void AddItems()
    {
        foreach ((string spot, ItemId item) in LobbyMap.ItemsFor(_run.Chapter))
        {
            if (!_run.LobbyTaken.Contains(spot)) _items.Add(new LobbyItem(spot, item, _map.ItemSpots[spot]));
        }
    }

    /// <summary>Which way the player is pushing: WASD, the arrows, the stick or the d-pad, added up.</summary>
    private Vector2 ReadMovement(InputState input)
    {
        if (_proofMove is Vector2 scripted) return scripted;

        Vector2 push = Vector2.Zero;
        if (input.IsKeyPressed(Keys.W) || input.IsKeyPressed(Keys.Up)) push.Y -= 1f;
        if (input.IsKeyPressed(Keys.S) || input.IsKeyPressed(Keys.Down)) push.Y += 1f;
        if (input.IsKeyPressed(Keys.A) || input.IsKeyPressed(Keys.Left)) push.X -= 1f;
        if (input.IsKeyPressed(Keys.D) || input.IsKeyPressed(Keys.Right)) push.X += 1f;

        Vector2 stick = input.CurrentGamePadStates[0].ThumbSticks.Left;
        push += new Vector2(stick.X, -stick.Y);

        return push;
    }

    /// <summary>Moves the player one axis at a time, so a wall stops one direction and lets them slide along it in the other.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="push">Which way, from the input.</param>
    private void Walk(GameTime gameTime, Vector2 push)
    {
        if (push.LengthSquared() < 0.04f)
        {
            _player.IsWalking = false;
            return;
        }

        // Diagonals are not faster.
        if (push.LengthSquared() > 1f) push.Normalize();

        Vector2 step = push * WalkSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 at = _player.Position;

        at.X += step.X;
        at.X = ResolveX(at, step.X);
        at.Y += step.Y;
        at.Y = ResolveY(at, step.Y);

        _player.Position = at;
        _player.IsWalking = true;

        // Face the way they are pushing hardest; on a dead diagonal, keep whichever they already had.
        if (MathF.Abs(push.X) > MathF.Abs(push.Y) + 0.01f) _player.Facing = push.X < 0 ? Direction.Left : Direction.Right;
        else if (MathF.Abs(push.Y) > MathF.Abs(push.X) + 0.01f) _player.Facing = push.Y < 0 ? Direction.Up : Direction.Down;
    }

    /// <summary>Backs the feet out of anything they walked into sideways, flush against its edge.</summary>
    private float ResolveX(Vector2 at, float moved)
    {
        foreach (BoundingRectangle solid in Solids())
        {
            BoundingRectangle feet = FeetAt(at);
            if (!feet.CollidesWith(solid)) continue;

            at.X = moved > 0f ? solid.Left - FeetSize.X / 2f - CollisionGap : solid.Right + FeetSize.X / 2f + CollisionGap;
        }
        return at.X;
    }

    /// <summary>Backs the feet out of anything they walked into up or down the room.</summary>
    private float ResolveY(Vector2 at, float moved)
    {
        foreach (BoundingRectangle solid in Solids())
        {
            BoundingRectangle feet = FeetAt(at);
            if (!feet.CollidesWith(solid)) continue;

            at.Y = moved > 0f ? solid.Top - CollisionGap : solid.Bottom + FeetSize.Y + CollisionGap;
        }
        return at.Y;
    }

    /// <summary>How far off a wall the feet are left. The tutorial's rectangles count touching as hitting.</summary>
    private const float CollisionGap = 0.01f;

    /// <summary>Everything the player cannot walk through: walls, furniture and people.</summary>
    private IEnumerable<BoundingRectangle> Solids()
    {
        foreach (BoundingRectangle wall in _map.Walls) yield return wall;
        foreach (LobbyProp prop in _map.Props)
            if (prop.Footprint is BoundingRectangle footprint) yield return footprint;
        foreach (LobbyNpc npc in _npcs) yield return npc.Footprint;
    }

    /// <summary>The player's feet as a box, for a given position.</summary>
    private static BoundingRectangle FeetAt(Vector2 at) =>
        new(at.X - FeetSize.X / 2f, at.Y - FeetSize.Y, FeetSize.X, FeetSize.Y);

    /// <summary>E, Space or Enter: talk to whoever is closest, or try the door.</summary>
    private void Interact()
    {
        LobbyNpc nearest = NearestInReach();
        if (nearest is not null)
        {
            Talk(nearest);
            return;
        }

        if (!NearDoor) return;

        if (DoorIsOpen)
        {
            GoThroughDoor();
            return;
        }

        _audio.Play(Sfx.DoorLocked);
        _dialogue.Open(_box, LobbyLines.DoorLocked);
    }

    /// <summary>Whoever is close enough to talk to, closest first, or null.</summary>
    private LobbyNpc NearestInReach()
    {
        LobbyNpc nearest = null;
        float best = float.MaxValue;

        foreach (LobbyNpc npc in _npcs)
        {
            float distance = Vector2.Distance(npc.Walker.Position, _player.Position);
            if (npc.InReach(_player.Position) && distance < best)
            {
                best = distance;
                nearest = npc;
            }
        }

        return nearest;
    }

    /// <summary>Whether the player is standing close enough to the door to try it.</summary>
    private bool NearDoor => Vector2.Distance(_player.Position, _map.Door.Foot) < DoorReach;

    /// <summary>Starts a conversation. When the chapter's opponent finishes theirs, the door opens.</summary>
    /// <param name="npc">Who is being spoken to.</param>
    private void Talk(LobbyNpc npc)
    {
        npc.Walker.Face(_player.Position);
        _player.Face(npc.Walker.Position);
        _player.IsWalking = false;

        bool challenger = npc.Id == _challenger;
        bool spoken = _run.HasFlag(SpokenFlag(npc.Id));

        _dialogue.Open(npc.Speaker, LobbyLines.For(npc.Id, spoken), () =>
        {
            _run.SetFlag(SpokenFlag(npc.Id));
            if (!challenger || DoorIsOpen) return;

            // The challenge: the door unlocks and its lamp comes on.
            _run.SetFlag(ChallengedFlag);
            _audio.Play(Sfx.DoorOpen, pan: Pan(_map.Door.Foot));
            ShowToast(OpenObjective);
        });
    }

    /// <summary>Picks up anything the player is standing on, if there is room for it.</summary>
    private void PickUpItems()
    {
        BoundingRectangle feet = FeetAt(_player.Position);

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            LobbyItem item = _items[i];
            if (!item.Bounds.CollidesWith(feet))
            {
                if (_warnedAbout == item) _warnedAbout = null;
                continue;
            }

            if (_run.PocketsFull)
            {
                if (_warnedAbout == item) continue;

                _warnedAbout = item;
                _audio.Play(Sfx.PocketsFull);
                ShowToast("YOUR POCKETS ARE FULL.");
                continue;
            }

            _run.Banked.Add(ItemCatalog.ToSaveId(item.Item));
            _run.LobbyTaken.Add(item.Spot);
            _items.RemoveAt(i);

            _audio.PlayItem(item.Item);
            ShowToast("YOU PICK UP THE " + ItemCatalog.NameOf(item.Item).ToUpperInvariant() + ".");
        }
    }

    /// <summary>Opens the door the rest of the way and starts the walk through it. The table comes up once the room has gone dark.</summary>
    private void GoThroughDoor()
    {
        if (_entering >= 0f) return;

        _entering = 0f;
        _player.IsWalking = false;
        _player.Facing = Direction.Up;
        _audio.StopSong();
        _audio.Play(Sfx.EnterArena);

        // Saved on the way in, so the pockets go to the table even if the game is closed there.
        _session.Save();
    }

    /// <summary>The pause menu's way out: save the run as it is.</summary>
    /// <returns>Why the save failed, or null.</returns>
    private string SaveAndLeave() => _session.Save() ? null : _session.LastError;

    /// <summary>A footstep, a little different every time, left and right in turn.</summary>
    private void Footstep()
    {
        _stepLeft = !_stepLeft;
        float pitch = ((float)_random.NextDouble() * 2f - 1f) * StepPitch;
        _audio.Play(Sfx.Footstep, StepVolume, pitch, _stepLeft ? -StepPan : StepPan);
    }

    /// <summary>How far left or right of the middle of the screen something is, for panning a sound to it.</summary>
    private float Pan(Vector2 world) =>
        Math.Clamp((_camera.ToScreen(world).X - BlackBoxGame.ScreenWidth / 2f) / (BlackBoxGame.ScreenWidth / 2f), -1f, 1f) * 0.6f;

    /// <summary>Puts a message up near the top of the screen for a moment.</summary>
    private void ShowToast(string message)
    {
        _toast = message;
        _toastLeft = ToastSeconds;
    }

    /// <summary>The door's red lamp, breathing.</summary>
    private void DrawDoorLight(SpriteBatch spriteBatch)
    {
        float breath = 0.55f + 0.25f * MathF.Sin(_clock * DoorGlowBreath);
        Vector2 at = _map.Door.Foot + DoorGlowOffset;
        var glow = new Rectangle((int)at.X - DoorGlowSize.X / 2, (int)at.Y - DoorGlowSize.Y / 2, DoorGlowSize.X, DoorGlowSize.Y);
        spriteBatch.Draw(_glow, glow, DoorGlowColor * breath);
    }

    /// <summary>The chapter and the room, what is left to do, any message, and the way to pause.</summary>
    private void DrawHeading(SpriteBatch spriteBatch)
    {
        string heading = "CHAPTER " + _run.Chapter + "  ·  THE LOBBY";
        Text.DrawCentered(spriteBatch, _detailFont, heading, HeadingY, Palette.DimText, Palette.Shadow, Palette.ShadowOffset);
        Text.DrawCentered(spriteBatch, _detailFont, DoorIsOpen ? OpenObjective : WaitingObjective, ObjectiveY,
            DoorIsOpen ? ButtonSprite.EmberRed : Palette.DimText, Palette.Shadow, Palette.ShadowOffset);

        Text.Draw(spriteBatch, _detailFont, _run.PlayerName.ToUpperInvariant(), Margin, HeadingY, Palette.DimText, Palette.Shadow);
        Text.Draw(spriteBatch, _detailFont, PauseHint, BlackBoxGame.ScreenWidth - Margin, HeadingY,
            Palette.DimText, Palette.Shadow, rightAligned: true);

        if (_toastLeft > 0f && _toast is not null)
        {
            float fade = MathF.Min(1f, _toastLeft / 0.4f);
            Text.DrawCentered(spriteBatch, _font, _toast, ToastY, ButtonSprite.BoneWhite * fade, Palette.Shadow * fade, Palette.ShadowOffset);
        }
    }

    /// <summary>What E would do here, floating over whoever or whatever it would do it to.</summary>
    private void DrawPrompt(SpriteBatch spriteBatch)
    {
        if (_dialogue.IsOpen || _entering >= 0f || !IsActive) return;

        string prompt = null;
        Vector2 over = Vector2.Zero;

        LobbyNpc nearest = NearestInReach();
        if (nearest is not null)
        {
            prompt = "E  ·  TALK";
            over = nearest.Walker.Position;
        }
        else if (NearDoor)
        {
            prompt = DoorIsOpen ? "E  ·  GO IN" : "E  ·  TRY THE DOOR";
            over = _map.Door.Foot;
        }

        if (prompt is null) return;

        Vector2 at = _camera.ToScreen(over) - new Vector2(0f, PromptRise);
        at.Y = MathF.Max(at.Y, PromptTop);
        Text.Draw(spriteBatch, _detailFont, prompt, at.X, at.Y, ButtonSprite.Amber, Palette.Shadow, centred: true);
    }
}
