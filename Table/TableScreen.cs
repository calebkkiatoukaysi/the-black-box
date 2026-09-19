using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Checks;

namespace TheBlackBox;

/// <summary>
/// The table: one run being played, from the first line of the discussion to the verdict.
/// </summary>
/// <remarks>
/// The rules are in RoundEngine; this is what to draw and when. It is split by phase: the
/// discussion, the hand and the payout, and the player's turn each have their own file, and
/// so does the proof schedule. BlackBoxGame owns the sprite batches and the box, so the
/// drawing is four calls, one per batch, and the box is handed in.
/// </remarks>
public partial class TableScreen
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

    /// <summary>How far above the pockets a save error is printed.</summary>
    private const float StatusRise = 30f;

    /// <summary>What the box asks for once the talking is done, and the plates that follow.</summary>
    private const string FeedLabel = "PLACE YOUR HAND IN THE BOX";
    private const string UseLabel = "USE IT";
    private const string KeepLabel = "KEEP IT";
    private const string LeaveLabel = "LEAVE IT";
    private const string ContinueLabel = "CONTINUE";
    private const string ReturnLabel = "LEAVE THE TABLE";

    /// <summary>Where the three thirds of the decision sit, across the bottom of the table.</summary>
    private const float DecideLeftX = 520f;
    private const float DecideMiddleX = 800f;
    private const float DecideRightX = 1080f;

    /// <summary>Size of a decision plate, and of CONTINUE. Three decisions have to fit between the pocket rows.</summary>
    private static readonly Point DecideSize = new(240, 84);
    private static readonly Point ContinueSize = new(380, 84);

    /// <summary>What a save error is prefixed with on the table.</summary>
    internal const string SaveFailed = "COULD NOT SAVE -- ";

    private readonly BlackBoxSprite _box;
    private readonly OpponentSprite _opponent = new();
    private readonly HandSprite _hand = new();
    private readonly BoxLidSprite _lid = new();
    private readonly TokenSprite _token = new();
    private readonly CatchHandSprite _catchHand = new();
    private readonly RunHeading _heading = new();
    private readonly DialoguePlate _plate = new();
    private readonly EndingVeil _ending = new();
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
    private readonly ButtonSprite _returnButton;

    /// <summary>What the box deals from. Seeded by the clock like any other run.</summary>
    private readonly Random _random = new();

    /// <summary>The wall, the lamp and the table. Drawn behind everything.</summary>
    private Texture2D _room;
    private SpriteFont _detailFont;

    /// <summary>The run on the table, and which slot it came out of and goes back into.</summary>
    private SaveData _run;
    private int _slot;

    /// <summary>Which part of the round the table is in.</summary>
    private RoundPhase _phase = RoundPhase.Discussion;

    /// <summary>Why the last save failed, or null. Shown under the run rather than swallowed.</summary>
    public string Status { get; set; }

    /// <summary>Whether the table has given way to the close-up of the box.</summary>
    public bool IsCloseUp => _closeUp;

    /// <summary>Raised once the run is written back and the player is off the table.</summary>
    public event Action Left;

    /// <summary>Builds the table around the box the game already has.</summary>
    /// <param name="box">The box, which the table moves onto the table and up into the close-up.</param>
    public TableScreen(BlackBoxSprite box)
    {
        _box = box;

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
        _returnButton = new ButtonSprite(ReturnLabel, new Vector2(centreX, ButtonY), ButtonSprite.BoneWhite);

        _feedButton.Clicked += OfferHand;
        _useButton.Clicked += () => Decide(DealtChoice.Use);
        _keepButton.Clicked += () => Decide(DealtChoice.Pocket);
        _leaveButton.Clicked += () => Decide(DealtChoice.Leave);
        _continueButton.Clicked += StepLog;
        _returnButton.Clicked += Leave;

        // Yours on the left, theirs on the right, in the same colours as the names on the plate.
        _playerPockets = new PocketStrip("YOUR POCKETS", new Vector2(RunHeading.Margin, PocketsY), ButtonSprite.BoneWhite);
        _opponentPockets = new PocketStrip("THEIR POCKETS",
            new Vector2(BlackBoxGame.ScreenWidth - RunHeading.Margin - PocketStrip.Width, PocketsY), ButtonSprite.Amber);
        _playerPockets.SlotChosen += PlayPocket;

        _wheel.Chosen += Answer;
    }

    /// <summary>Loads everything on the table.</summary>
    /// <param name="content">The ContentManager to load with.</param>
    /// <param name="graphicsDevice">The device, for the parts that draw from a pixel.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice)
    {
        _room = content.Load<Texture2D>("room");
        _detailFont = content.Load<SpriteFont>("spectral-detail");

        _opponent.LoadContent(content);
        _hand.LoadContent(content);
        _lid.LoadContent(content);
        _token.LoadContent(content);
        _catchHand.LoadContent(content);
        _heading.LoadContent(content);
        _plate.LoadContent(content, graphicsDevice);
        _ending.LoadContent(content, graphicsDevice);
        _wheel.LoadContent(content, graphicsDevice);

        _aimCheck.LoadContent(content);
        _steadyCheck.LoadContent(content);
        _readCheck.LoadContent(content);

        _feedButton.LoadContent(content);
        _useButton.LoadContent(content);
        _keepButton.LoadContent(content);
        _leaveButton.LoadContent(content);
        _continueButton.LoadContent(content);
        _returnButton.LoadContent(content);

        _playerPockets.LoadContent(content);
        _opponentPockets.LoadContent(content);
    }

    /// <summary>Puts the player at the table and moves the scene around them.</summary>
    /// <remarks>A run saved mid-decision comes back mid-decision, so closing the game never costs an item.</remarks>
    /// <param name="slot">Which slot the run came out of, and goes back into.</param>
    /// <param name="run">The run.</param>
    /// <param name="status">Why a save just failed, or null.</param>
    public void Enter(int slot, SaveData run, string status = null)
    {
        _slot = slot;
        _run = run;
        Status = status;

        ClearTable();
        _returnButton.Reset();

        // Between chapters the seat is empty and the roster fills it. Within one, the save says who is there.
        if (string.IsNullOrWhiteSpace(_run.OpponentId))
            _run.OpponentId = Opponents.ForChapter(_run.Chapter).Id;
        _opponent.Who = Opponents.ById(_run.OpponentId);
        _aimCheck.Target = AimTarget;

        _opponentLivesShown = _run.OpponentLives;

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

    /// <summary>Writes the run back to its slot and raises <see cref="Left"/>.</summary>
    /// <remarks>If the write fails the player stays in the run, since the copy in memory is the only one left.</remarks>
    public void Leave()
    {
        // A finished run is written back ready to play again: next chapter if they got up, same one if not.
        if (RoundEngine.IsOver(_run))
        {
            if (_run.PlayerLives > 0) _run.Advance();
            else _run.Restart();
        }

        if (!SaveSystem.Write(_slot, _run))
        {
            Status = SaveFailed + SaveSystem.LastError;
            return;
        }

        _run = null;
        ClearTable();
        _phase = RoundPhase.Discussion;

        Left?.Invoke();
    }

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

    /// <summary>Runs whichever part of the round the table is in.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
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
                _returnButton.Update(gameTime);
                break;
        }
    }

    /// <summary>The room, the opponent and the box's shadow. The first batch, behind the box.</summary>
    /// <remarks>Same batch as the box so the layer sort puts the opponent behind it and the box over its own shadow.</remarks>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawScene(SpriteBatch spriteBatch)
    {
        spriteBatch.Draw(_room, new Rectangle(0, 0, BlackBoxGame.ScreenWidth, BlackBoxGame.ScreenHeight), null,
            Color.White, 0f, Vector2.Zero, SpriteEffects.None, Layers.Room);

        _opponent.Draw(spriteBatch, OpponentFoot);
        _box.DrawContactShadow(spriteBatch);
    }

    /// <summary>Everything between the player and the box: the jaws, both hands, the tag and a check's furniture.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawFront(GameTime gameTime, SpriteBatch spriteBatch)
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

    /// <summary>The bookkeeping, the plate and the verdict. The text batch, sampled smooth.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawText(SpriteBatch spriteBatch)
    {
        _heading.DrawText(spriteBatch, _run);

        if (Status is not null)
        {
            Text.DrawCentered(spriteBatch, _detailFont, Status, PocketsY - StatusRise, ButtonSprite.EmberRed,
                Palette.Shadow, Palette.ShadowOffset);
        }

        DrawPlate(spriteBatch);

        if (_phase == RoundPhase.Over) _ending.Draw(spriteBatch, _run.PlayerLives > 0);
    }

    /// <summary>The hearts, the item pictures, the pockets and whatever can be clicked. The pixel-art batch.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="spriteBatch">The SpriteBatch to render with.</param>
    public void DrawPlates(GameTime gameTime, SpriteBatch spriteBatch)
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

            case RoundPhase.Over:
                _returnButton.Draw(gameTime, spriteBatch);
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
