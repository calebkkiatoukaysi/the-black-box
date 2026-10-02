using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Audio;
using TheBlackBox.Checks;
using TheBlackBox.Screens;
using TheBlackBox.StateManagement;

namespace TheBlackBox;

/// <summary>
/// The table (the Black Box arena). Plays one run from the first line of the discussion to the
/// verdict.
/// </summary>
/// <remarks>
/// The rules are in RoundEngine; this is what to draw and when. It is split by phase: the
/// discussion, the hand and the payout, and the player's turn each have their own file, and
/// so does the proof schedule. The box belongs to BoxScene, so the table borrows it, moves it
/// onto the table and into the close-up, and the title puts it back after.
/// </remarks>
public partial class TableScreen : GameScreen
{
    /// <summary>Where the box sits on the table.</summary>
    /// <remarks>At TableScale this puts its bottom edge on the table with the opponent's face clear above it.</remarks>
    private const float BoxCenterY = 656f;

    /// <summary>Where the table cuts the opponent off. The bottom middle of their frame goes here.</summary>
    /// <remarks>
    /// The Y is the far lip of the table in room.png (ROOM_HORIZON in the generator). Left of
    /// centre so the box only covers one shoulder instead of everything from the collar down.
    /// The scale is on the roster (Opponent.Scale) since the two opponents are drawn differently.
    /// </remarks>
    private static readonly Vector2 OpponentFoot = new(600f, 640f);

    /// <summary>Where the aim check's sight has to land: the middle of the face, plus the clean and miss radii.</summary>
    /// <remarks>Where the face is comes off the roster (Opponent.Face), so Enter hands it to the check after the seat is filled.</remarks>
    private Vector2 AimTarget => OpponentFoot + _opponent.Who.Face;
    private const float AimInner = 44f;
    private const float AimOuter = 150f;

    /// <summary>Where the steady check's groove and the read check's tags go: the table left of the box, above the pockets.</summary>
    private static readonly Vector2 CheckBarOrigin = new(40f, 672f);
    private static readonly Vector2 CheckLaneStart = new(88f, 690f);
    private const float CheckLaneLength = 484f;

    /// <summary>Where the one button the table offers sits, low and central, and where the pocket rows sit above it.</summary>
    private const float ButtonY = 852f;
    private const float PocketsY = 722f;

    /// <summary>How long the table takes to fade in and out.</summary>
    private static readonly TimeSpan FadeInTime = TimeSpan.FromSeconds(0.8);
    private static readonly TimeSpan FadeOutTime = TimeSpan.FromSeconds(0.6);

    /// <summary>What the box asks for once the talking is done, and the plates that follow.</summary>
    private const string FeedLabel = "PLACE YOUR HAND IN THE BOX";
    private const string UseLabel = "USE IT";
    private const string KeepLabel = "KEEP IT";
    private const string LeaveLabel = "LEAVE IT";
    private const string ContinueLabel = "CONTINUE";

    /// <summary>Where the three thirds of the decision sit, across the bottom of the table.</summary>
    private const float DecideLeftX = 520f;
    private const float DecideMiddleX = 800f;
    private const float DecideRightX = 1080f;

    /// <summary>Size of a decision plate, and of CONTINUE. Three decisions have to fit between the pocket rows.</summary>
    private static readonly Point DecideSize = new(240, 84);
    private static readonly Point ContinueSize = new(380, 84);

    /// <summary>The keys that open the pause menu.</summary>
    private readonly InputAction _pause = new(
        new[] { Buttons.Start, Buttons.Back },
        new[] { Keys.Escape }, true);

    private readonly OpponentSprite _opponent = new();
    private readonly HandSprite _hand = new();
    private readonly BoxLidSprite _lid = new();
    private readonly TokenSprite _token = new();
    private readonly CatchHandSprite _catchHand = new();
    private readonly RunHeading _heading = new();
    private readonly DialoguePlate _plate = new();
    private readonly DialogueWheel _wheel = new();

    /// <summary>The three checks, made once and asked again each time an item calls for one.</summary>
    private readonly AimCheck _aimCheck;
    private readonly SteadyCheck _steadyCheck;
    private readonly ReadCheck _readCheck;

    /// <summary>The player's three pockets, and the opponent's.</summary>
    private readonly PocketStrip _playerPockets;
    private readonly PocketStrip _opponentPockets;

    private readonly ButtonSprite _feedButton;
    private readonly ButtonSprite _useButton;
    private readonly ButtonSprite _keepButton;
    private readonly ButtonSprite _leaveButton;
    private readonly ButtonSprite _continueButton;

    /// <summary>What the box deals from. Seeded by the clock like any other run.</summary>
    private readonly Random _random = new();

    /// <summary>The run being played and the slot it saves to.</summary>
    private readonly RunSession _session;
    private readonly SaveData _run;

    /// <summary>The box and the ash, borrowed from the game.</summary>
    private BoxScene _scene;
    private BlackBoxSprite _box;

    private ContentManager _content;
    private AudioManager _audio;

    /// <summary>The wall, the lamp and the table. Drawn behind everything.</summary>
    private Texture2D _room;

    /// <summary>Which part of the round the table is in.</summary>
    private RoundPhase _phase = RoundPhase.Discussion;

    /// <summary>Whether the results screen is already up. Only happens once per run.</summary>
    private bool _verdictShown;

    /// <summary>Whether the table had focus last frame. Update uses it.</summary>
    private bool _wasActive;

    /// <summary>Builds the table for a run.</summary>
    /// <param name="session">The run and the slot it saves to.</param>
    public TableScreen(RunSession session)
    {
        _session = session;
        _run = session.Run;

        TransitionOnTime = FadeInTime;
        TransitionOffTime = FadeOutTime;

        _aimCheck = new AimCheck(AimTarget, AimInner, AimOuter);
        _steadyCheck = new SteadyCheck(CheckBarOrigin);
        _readCheck = new ReadCheck(CheckLaneStart, CheckLaneLength);

        float centreX = BlackBoxGame.ScreenWidth / 2f;

        // Red spends something, amber keeps the offer, bone walks away.
        _feedButton = new ButtonSprite(FeedLabel, new Vector2(centreX, ButtonY), ButtonSprite.EmberRed);
        _useButton = new ButtonSprite(UseLabel, new Vector2(DecideLeftX, ButtonY), ButtonSprite.EmberRed, DecideSize);
        _keepButton = new ButtonSprite(KeepLabel, new Vector2(DecideMiddleX, ButtonY), ButtonSprite.Amber, DecideSize);
        _leaveButton = new ButtonSprite(LeaveLabel, new Vector2(DecideRightX, ButtonY), ButtonSprite.BoneWhite, DecideSize);
        _continueButton = new ButtonSprite(ContinueLabel, new Vector2(centreX, ButtonY), ButtonSprite.Amber, ContinueSize);

        _feedButton.Clicked += OfferHand;
        _useButton.Clicked += () => Decide(DealtChoice.Use);
        _keepButton.Clicked += () => Decide(DealtChoice.Pocket);
        _leaveButton.Clicked += () => Decide(DealtChoice.Leave);
        _continueButton.Clicked += Continue;

        // Yours on the left, theirs on the right, in the same colours as the names on the plate.
        _playerPockets = new PocketStrip("YOUR POCKETS", new Vector2(RunHeading.Margin, PocketsY), ButtonSprite.BoneWhite);
        _opponentPockets = new PocketStrip("THEIR POCKETS",
            new Vector2(BlackBoxGame.ScreenWidth - RunHeading.Margin - PocketStrip.Width, PocketsY), ButtonSprite.Amber);
        _playerPockets.SlotChosen += PlayPocket;

        _wheel.Chosen += Answer;
    }

    /// <summary>Loads everything for the table, borrows the box, and sets up the round.</summary>
    public override void Activate()
    {
        _content ??= new ContentManager(ScreenManager.Game.Services, "Content");
        _scene = ScreenManager.Game.Services.GetService<BoxScene>();
        _box = _scene.Box;
        _audio = ScreenManager.Game.Services.GetService<AudioManager>();

        LoadContent(_content, ScreenManager.GraphicsDevice);
        Enter();

        _audio.PlaySong(Track.Arena);
    }

    /// <summary>Unloads the table.</summary>
    public override void Unload() => _content?.Unload();

    /// <summary>Loads everything on the table.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the parts that draw from a pixel.</param>
    private void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _room = content.Load<Texture2D>("room");

        _opponent.LoadContent(content);
        _hand.LoadContent(content);
        _lid.LoadContent(content);
        _token.LoadContent(content);
        _catchHand.LoadContent(content);
        _heading.LoadContent(content);
        _plate.LoadContent(content, graphicsDevice);
        _wheel.LoadContent(content, graphicsDevice);

        _aimCheck.LoadContent(content);
        _steadyCheck.LoadContent(content);
        _readCheck.LoadContent(content);

        _feedButton.LoadContent(content);
        _useButton.LoadContent(content);
        _keepButton.LoadContent(content);
        _leaveButton.LoadContent(content);
        _continueButton.LoadContent(content);

        _playerPockets.LoadContent(content);
        _opponentPockets.LoadContent(content);
    }

    /// <summary>Puts the player at the table and moves the scene around them.</summary>
    /// <remarks>A run saved mid-decision comes back mid-decision, so closing the game never costs an item.</remarks>
    private void Enter()
    {
        ClearTable();
        _verdictShown = false;

        // Between chapters the seat is empty and the roster fills it. Within one, the save says who is there.
        if (string.IsNullOrWhiteSpace(_run.OpponentId))
            _run.OpponentId = Opponents.ForChapter(_run.Chapter).Id;
        _opponent.Who = Opponents.ById(_run.OpponentId);
        _aimCheck.Target = AimTarget;

        _opponentLivesShown = _run.OpponentLives;

        // If the run was saved on the verdict it's already over, so Update shows the results on the first frame.
        if (RoundEngine.IsOver(_run))
        {
            _phase = RoundPhase.Over;
            return;
        }

        if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            _opponent.SetDisposition(_run.OpponentDisposition);
            BeginTurn();
            return;
        }

        StartDiscussion();
    }

    /// <summary>Finishes the run and saves it: next chapter if they won, the same table again if not.</summary>
    /// <remarks>The results screen calls this. If the save fails the run stays the same in memory, so pressing again just tries again.</remarks>
    /// <returns>Why the save failed, or null.</returns>
    internal string FinishRun()
    {
        if (RoundEngine.IsOver(_run))
        {
            if (_run.PlayerLives > 0) _run.Advance();
            else _run.Restart();
        }

        return _session.Save() ? null : _session.LastError;
    }

    /// <summary>Saves the run as it is, mid round or not. The pause menu calls this.</summary>
    /// <returns>Why the save failed, or null.</returns>
    private string SaveAndLeave() => _session.Save() ? null : _session.LastError;

    /// <summary>Takes everything off the table: the wheel, the hands, the tag, a check, the close-up and the log.</summary>
    private void ClearTable()
    {
        _discussion = null;
        _discussionEnd = null;
        _saidHold = 0f;

        _wheel.Hide();
        _hand.IsVisible = false;
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        _check = null;
        _afterCheck = null;

        _log.Clear();
        _logLine = null;
        _resumeTurn = false;

        LeaveCloseUp();
    }

    /// <summary>Opens the pause menu.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="input">The input this frame.</param>
    public override void HandleInput(GameTime gameTime, InputState input)
    {
        if (_phase != RoundPhase.Over && _pause.Occurred(input))
            ScreenManager.AddScreen(new PauseMenuScreen(SaveAndLeave));
    }

    /// <summary>Runs whatever part of the round the table is in, unless another screen is on top.</summary>
    /// <remarks>Nothing moves while paused, including the discussion timer. When the menu closes the plates get reset so the same click doesn't also hit the table.</remarks>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="otherScreenHasFocus">Whether a screen above has the input.</param>
    /// <param name="coveredByOtherScreen">Whether a non-popup screen is on top.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (!IsActive)
        {
            _wasActive = false;
            return;
        }

        if (!_wasActive)
        {
            _wasActive = true;
            _feedButton.Reset();
            _useButton.Reset();
            _keepButton.Reset();
            _leaveButton.Reset();
            _continueButton.Reset();
        }

        // Playtime is counted here, not from the system clock, so time with the game closed does not count.
        _run.Playtime += gameTime.ElapsedGameTime;

        // The pockets read off the run every frame, so nothing that changes them has to remember to tell them.
        _playerPockets.Show(_run.Banked, revealed: true, interactive: _phase == RoundPhase.PlayerTurn);
        _opponentPockets.Show(_run.OpponentBanked, revealed: _run.SeesOpponentPockets, interactive: false);

        switch (_phase)
        {
            case RoundPhase.Discussion:
                UpdateDiscussion(gameTime);
                break;

            case RoundPhase.Offer:
                _feedButton.Update(gameTime);
                break;

            case RoundPhase.Reaching:
                UpdateReach(gameTime);
                break;

            case RoundPhase.Catching:
                UpdateCatch(gameTime);
                break;

            case RoundPhase.SkillCheck:
                UpdateCheck(gameTime);
                break;

            case RoundPhase.PlayerTurn:
                _playerPockets.Update(gameTime);
                _useButton.Update(gameTime);
                _keepButton.Update(gameTime);
                _leaveButton.Update(gameTime);
                break;

            case RoundPhase.Resolving:
                _continueButton.Update(gameTime);
                break;

            case RoundPhase.Over:
                if (!_verdictShown) ShowVerdict();
                break;
        }
    }

    /// <summary>The run is over, so stop the music, play the verdict, and show the results.</summary>
    private void ShowVerdict()
    {
        _verdictShown = true;

        bool won = _run.PlayerLives > 0;
        _audio.StopSong();
        _audio.Play(won ? Sfx.Win : Sfx.Lose);

        ScreenManager.AddScreen(new ResultsScreen(_session, won, FinishRun));
    }

    /// <summary>Draws the table in five batches and fades it with the transition.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;

        // 1. The room (or the background in the close-up) and the box. Point sampling keeps the pixel art crisp.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        if (_closeUp) _scene.DrawSky(gameTime, spriteBatch);
        else DrawScene(spriteBatch);
        _box.DrawBody(gameTime, spriteBatch);
        spriteBatch.End();

        // 2. The eye glow. Additive so it lights up the rim, and linear sampling so it stays soft.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BoxScene.PremultipliedAdditive, SamplerState.LinearClamp);
        _box.DrawEyeGlow(gameTime, spriteBatch);
        spriteBatch.End();

        // 3. The eyes on top of the glow, then the ash, then the hands on top of that.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _box.DrawEyes(gameTime, spriteBatch);
        _scene.DrawAsh(gameTime, spriteBatch);
        DrawFront(gameTime, spriteBatch);
        spriteBatch.End();

        // 4. Text, with linear sampling so the font stays smooth.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawText(spriteBatch);
        spriteBatch.End();

        // 5. The plates. Point sampling since they're pixel art. The labels are drawn 1:1 so they look fine too.
        spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawPlates(gameTime, spriteBatch);
        spriteBatch.End();

        ScreenManager.FadeBackBufferToBlack(TransitionPosition);
    }

    /// <summary>The room, the opponent and the box's shadow. The first batch, behind the box.</summary>
    /// <remarks>Same batch as the box so the layer sort puts the opponent behind it and the box over its own shadow.</remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawScene(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_room, new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight), null,
            Color.White, 0f, Vector2.Zero, SpriteEffects.None, Layers.Room);

        _opponent.Draw(spriteBatch, OpponentFoot);
        _box.DrawContactShadow(spriteBatch);
    }

    /// <summary>Everything between the player and the box: the jaws, both hands, the tag and a check's furniture.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawFront(GameTime gameTime, SpriteBatch spriteBatch)
    {
        // The jaws go over the eyes and under the arm, which is between the player and the box.
        if (_closeUp)
        {
            _lid.Draw(spriteBatch, _box.Aperture, CloseUpScale);
            _hand.Draw(spriteBatch, HandRest, HandMouth, HandScale);
        }

        _catchHand.Draw(spriteBatch);
        _token.Draw(gameTime, spriteBatch);
        _check?.Draw(gameTime, spriteBatch);
    }

    /// <summary>The bookkeeping text and its plate. Goes in the text batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawText(SpriteBatch spriteBatch)
    {
        _heading.DrawText(spriteBatch, _run);
        DrawPlate(spriteBatch);
    }

    /// <summary>The hearts, the item pictures, the pockets and whatever can be clicked. The pixel-art batch.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawPlates(GameTime gameTime, SpriteBatch spriteBatch)
    {
        _heading.DrawHearts(spriteBatch, _run);
        if (DealtOnShow is ItemId dealt) _plate.DrawIcon(spriteBatch, dealt);

        // The pockets are always on the table, so the player can plan around them while talking.
        if (!_closeUp)
        {
            _playerPockets.Draw(gameTime, spriteBatch);
            _opponentPockets.Draw(gameTime, spriteBatch);
        }

        // Exactly one thing to click at a time.
        if (_wheel.IsOpen)
        {
            _wheel.Draw(gameTime, spriteBatch, _discussion?.Patience ?? 0f);
            return;
        }

        switch (_phase)
        {
            case RoundPhase.Offer:
                _feedButton.Draw(gameTime, spriteBatch);
                break;

            case RoundPhase.PlayerTurn:
                _useButton.Draw(gameTime, spriteBatch);
                _keepButton.Draw(gameTime, spriteBatch);
                _leaveButton.Draw(gameTime, spriteBatch);
                break;

            case RoundPhase.Resolving:
                _continueButton.Draw(gameTime, spriteBatch);
                break;
        }
    }

    /// <summary>Draws the plate on the wall and whatever is being said on it.</summary>
    /// <remarks>Whoever is speaking owns the plate: the opponent, the player for a beat after a reply, then the box.</remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    private void DrawPlate(SpriteBatch spriteBatch)
    {
        string text = RunLine;
        if (string.IsNullOrEmpty(text) && _discussion is null) return;

        (string speaker, Color colour) = Speaker;
        bool live = _phase == RoundPhase.Discussion && _discussionEnd is null;

        _plate.Draw(spriteBatch, speaker, colour, text, live, DealtOnShow is not null);
    }

    /// <summary>Who the plate belongs to right now, and the colour their name is set in.</summary>
    private (string, Color) Speaker
    {
        get
        {
            if (_phase != RoundPhase.Discussion || _discussionEnd is not null)
                return ("THE BOX", ButtonSprite.EmberRed);

            return IsPlayerSpeaking
                ? (_run.PlayerName.ToUpperInvariant(), ButtonSprite.BoneWhite)
                : (_discussion?.OpponentName ?? string.Empty, ButtonSprite.Amber);
        }
    }

    /// <summary>The item in the player's hand while they are deciding about it, if they are allowed to see it.</summary>
    private ItemId? DealtOnShow =>
        _phase == RoundPhase.PlayerTurn && !_run.DealtBlind && ItemCatalog.TryParse(_run.Dealt, out ItemId dealt)
            ? dealt
            : null;

    /// <summary>What the table is saying right now: a line, or what the box just did.</summary>
    /// <remarks>A blind deal is the one case where the game knows the item and will not print it. That is the Rotgut's debt.</remarks>
    private string RunLine
    {
        get
        {
            switch (_phase)
            {
                case RoundPhase.Offer:
                    return _discussionEnd + "  IT WANTS A HAND.";

                case RoundPhase.Reaching:
                    return _reach < 1f ? "..." : "IT HAS YOU.";

                case RoundPhase.Catching:
                    return "IT LETS GO.  CATCH WHAT IT GIVES YOU.";

                case RoundPhase.SkillCheck:
                    return _check?.Prompt ?? string.Empty;

                case RoundPhase.Resolving:
                    return _logLine ?? string.Empty;

                case RoundPhase.Over:
                    return _run.PlayerLives <= 0
                        ? "YOU DO NOT GET UP FROM THE TABLE."
                        : "YOU GET UP. THE BOX IS STILL WATCHING.";

                case RoundPhase.PlayerTurn when _run.DealtBlind:
                    return "IT PUT SOMETHING IN YOUR HAND. YOU ARE NOT ALLOWED TO LOOK AT IT.";

                case RoundPhase.PlayerTurn when DealtOnShow is ItemId dealt:
                    ItemDefinition item = ItemCatalog.Get(dealt);
                    return item.Name.ToUpperInvariant() + "  --  " + item.Description;

                case RoundPhase.PlayerTurn:
                    return "THE BOX GAVE YOU NOTHING.";

                default:
                    if (_discussionEnd is not null) return _discussionEnd;
                    if (_discussion is null) return string.Empty;

                    return IsPlayerSpeaking ? _discussion.Said : _discussion.Line;
            }
        }
    }
}
