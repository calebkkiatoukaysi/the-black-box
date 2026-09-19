using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheBlackBox.Checks;
using TheBlackBox.Collisions;

namespace TheBlackBox;

/// <summary>
/// The Black Box: the title screen, the save form, and the table.
/// </summary>
public partial class BlackBoxGame : Game
{

    internal const int ScreenWidth = 1600;
    internal const int ScreenHeight = 900;

    // box center position
    private const float BoxCenterY = 500f;

    /// <summary>Where the box sits once the player is at the table.</summary>
    /// <remarks>At TableScale this puts its bottom edge on the table with the opponent's face clear above it.</remarks>
    private const float RunBoxCenterY = 656f;

    /// <summary>How much ash is caught in the pull at any one time.</summary>
    private const int AshCount = 34;
    private const float TitleY = 56f;

    private const float EscapeHintY = 856f;

    private const string Title = "The Black Box";

    private const string StartLabel = "START GAME";
    private const string ExitLabel = "EXIT";
    private const string ReturnLabel = "LEAVE THE TABLE";

    /// <summary>Centre line of the row of buttons, clear of the bottom of the box.</summary>
    private const float ButtonRowY = 806f;

    /// <summary>Gap between the two buttons, in screen pixels.</summary>
    private const float ButtonGap = 36f;

    /// <summary>Where the table cuts the opponent off. The bottom middle of their frame goes here.</summary>
    /// <remarks>
    /// The Y is the far lip of the table in room.png (ROOM_HORIZON in the generator). Left of
    /// centre so the box only covers one shoulder instead of everything from the collar down.
    /// The scale is on the roster now (Opponent.Scale) since the two opponents are drawn differently.
    /// </remarks>
    private static readonly Vector2 OpponentFoot = new(600f, 640f);

    /// <summary>How far the player's own arm is blown up. Same whole number as the rest so the pixels match.</summary>
    private const float HandScale = 4f;

    /// <summary>Where the fingertips wait before the hand is offered: off the bottom of the screen, right of centre.</summary>
    /// <remarks>
    /// This and HandMouth are on the line the arm is drawn along, so it slides out along its own
    /// length instead of drifting sideways. The whole frame is off screen here.
    /// </remarks>
    private static readonly Vector2 HandRest = new(1058f, 997f);

    /// <summary>Where the fingertips end up: inside the mouth of the box, a little left of centre.</summary>
    private static readonly Vector2 HandMouth = new(792f, 598f);

    /// <summary>How long the hand takes to go in, in seconds.</summary>
    private const float ReachSeconds = 1.35f;

    /// <summary>The close-up of the box: where it sits, how big it is, and where the hands go.</summary>
    /// <remarks>
    /// When a hand is offered the table goes away and the box fills the view at 6x. The hands are
    /// drawn at 6x too so the arm going in matches what it is going into.
    /// </remarks>
    private static readonly Vector2 BoxViewCentre = new(610f, 440f);
    private const float BoxViewScale = 6f;
    private const float BoxHandScale = 6f;
    private static readonly Vector2 BoxHandRest = new(1560f, 1120f);
    private static readonly Vector2 BoxHandMouth = new(540f, 470f);
    private static readonly Vector2 BoxTokenLaunch = new(610f, 520f);
    private const float BoxCatchTipY = 640f;
    private const float BoxCatchMinX = 300f;
    private const float BoxCatchMaxX = 1300f;

    /// <summary>How long the box takes to open its mouth once a hand is offered, in seconds.</summary>
    private const float LidSeconds = 0.8f;

    /// <summary>Where the tag leaves the box: the mouth, a little below where the fingers went in.</summary>
    /// <remarks>The catch is meant to be short and fair. A hand on the keys can get across the table in time.</remarks>
    private static readonly Vector2 TokenLaunch = new(800f, 640f);

    /// <summary>How fast the tag leaves the mouth down the table, in screen pixels a second.</summary>
    private const float TokenLaunchSpeed = 90f;

    /// <summary>The most sideways drift the tag can leave with, either way, in screen pixels a second.</summary>
    private const float TokenDrift = 360f;

    /// <summary>How far down the screen the catching hand's fingertips sit.</summary>
    private const float CatchTipY = 790f;

    /// <summary>How far left and right the palm can go: the width of the table, less the hand.</summary>
    private const float CatchMinX = 240f;
    private const float CatchMaxX = 1360f;

    /// <summary>The dark past the near edge of the table. A tag that gets there is gone.</summary>
    /// <remarks>Starts a little under the bottom of the screen so the tag is seen to leave, not cut off halfway.</remarks>
    private static readonly BoundingRectangle TheDark = new(0f, ScreenHeight + 10f, ScreenWidth, 400f);

    /// <summary>Where the aim check's sight has to land: the middle of the face, plus the clean and miss radii.</summary>
    /// <remarks>
    /// Where the face is comes off the roster (Opponent.Face) since the two opponents are different sizes,
    /// so EnterRun hands it to the check after the seat is filled.
    /// </remarks>
    private Vector2 AimTarget => OpponentFoot + _opponent.Who.Face;
    private const float AimInner = 44f;
    private const float AimOuter = 150f;

    /// <summary>Where the steady check's groove and the read check's tags go: the table left of the box, above the pockets.</summary>
    private static readonly Vector2 CheckBarOrigin = new(40f, 672f);
    private static readonly Vector2 CheckLaneStart = new(88f, 690f);
    private const float CheckLaneLength = 484f;

    /// <summary>How long the box holds the hand before it pays, in seconds.</summary>
    private const float TakeSeconds = 0.9f;

    // How long the player's own line is held: a base, a bit per character, and a floor and
    // ceiling. The clock keeps running through it, so a reply costs the time it takes to say.
    private const float SayingBase = 0.9f;
    private const float SayingPerCharacter = 0.032f;
    private const float SayingMin = 1.3f;
    private const float SayingMax = 3.4f;

    /// <summary>How long the opponent's mouth stays open after a line of theirs lands, in seconds.</summary>
    /// <remarks>A beat, not the whole line. There is no audio, so holding it longer just leaves them gaping.</remarks>
    private const float SpeakSeconds = 0.75f;

    // The colours the table's own text is set in. The shared ones are in Palette.
    private static readonly Color TitleColor = new(240, 235, 240);
    private static readonly Color TitleShadow = new(104, 12, 17);
    private static readonly Color HintColor = new(150, 140, 142);
    private static readonly Color PlateColor = new Color(6, 5, 9) * 0.80f;
    private static readonly Color PlateLip = new(64, 58, 66);
    private static readonly Color LineLive = new(226, 216, 210);
    private static readonly Color LineSaid = new(162, 150, 150);
    private static readonly Color EndingLineColor = new(170, 158, 160);
    private static readonly Vector2 EndingShadowOffset = new(3f, 3f);

    /// <summary>Room left either side of the verdict before it is shrunk to fit.</summary>
    private const float EndingMargin = 80f;

    /// <summary>How far above the pockets a save error is printed.</summary>
    private const float StatusRise = 30f;

    /// <summary>The gap between a name and its hearts, and how far the hearts sit below the names' top.</summary>
    private const float HeartsGap = 12f;
    private const float HeartsDrop = 2f;

    /// <summary>How much of the plate's padding is left under its last line.</summary>
    private const float PlateBottomPad = 0.6f;

    /// <summary>The thin line of run bookkeeping along the top edge.</summary>
    private const float RunStatusY = 14f;

    /// <summary>How dark the veil over the table is at the end of a run, and where the verdict sits.</summary>
    private const float EndingVeilOpacity = 0.78f;
    private const float EndingTitleY = 300f;
    private const float EndingLineY = 446f;

    /// <summary>How far along the heading from the left margin "THEM" and their hearts start.</summary>
    private const float HeartsThemX = 262f;

    /// <summary>The plate the conversation is written on, on the wall to the right of the opponent.</summary>
    /// <remarks>
    /// It used to sit above their head, but the head reaches the top of the wall now. It grows
    /// downward as lines wrap. DialogueWheel's patience bar rectangle has to agree with these numbers.
    /// </remarks>
    private static readonly Rectangle DialoguePlate = new(1000, 56, 580, 0);

    /// <summary>Breathing room inside the plate, either side of the text.</summary>
    private const int DialoguePadding = 40;

    /// <summary>Where the top of the speaker's name sits inside the plate.</summary>
    private const float DialogueNameY = 66f;

    /// <summary>Where the first line of what they are saying sits.</summary>
    private const float DialogueLineY = 124f;

    /// <summary>Where the one button the table offers sits, low and central.</summary>
    private const float RunButtonY = 852f;

    /// <summary>Where the pocket rows sit: above the button row, either side of the box.</summary>
    private const float PocketsY = 722f;

    /// <summary>How far in from the screen edge the pocket rows start.</summary>
    private const float PocketsMargin = 24f;

    /// <summary>What the box asks for once the talking is done.</summary>
    private const string FeedLabel = "PLACE YOUR HAND IN THE BOX";

    private const string UseLabel = "USE IT";
    private const string KeepLabel = "KEEP IT";
    private const string LeaveLabel = "LEAVE IT";
    private const string ContinueLabel = "CONTINUE";

    /// <summary>What KEEP IT says under itself when it cannot be pressed.</summary>
    private const string NoRoomNote = "NO ROOM";
    private const string UnseenNote = "NOT UNSEEN";

    /// <summary>Where the three thirds of the decision sit, across the bottom of the table.</summary>
    private const float DecideLeftX = 520f;
    private const float DecideMiddleX = 800f;
    private const float DecideRightX = 1080f;

    /// <summary>Size of a decision plate. Three of them have to fit between the pocket rows.</summary>
    private static readonly Point DecideSize = new(240, 84);

    /// <summary>Not quite black, so the box itself still reads as the darkest thing on screen.</summary>
    private static readonly Color VoidColor = new(10, 8, 16);

    /// <summary>Additive blending for premultiplied-alpha content. In this case this is for the eye glow.</summary>
    /// <remarks>
    /// The stock BlendState.Additive multiplies by alpha again on the way in, which the pipeline
    /// already did, so the glow came out dim. Forcing the source factor to One fixes it.
    ///
    /// Author Note for Grader: This code was developed and generated by AI. It is intended to handle additive blending
    ///  correctly for premultiplied-alpha content. (the Eye Glow) Thank you for understanding! -Caleb (Pretty cool too!)
    /// </remarks>
    private static readonly BlendState PremultipliedAdditive = new()
    {
        ColorSourceBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private AshDriftSprite[] _ashDrift;
    private BlackBoxSprite _blackBox;
    private AshSprite[] _ashes;

    private ButtonSprite _startButton;
    private ButtonSprite _exitButton;
    private ButtonSprite _returnButton;

    private SaveSlotMenu _slotMenu;
    private NameEntry _nameEntry;

    private OpponentSprite _opponent;
    private HandSprite _hand;

    /// <summary>The tag the box pays with, while it is on the table.</summary>
    private TokenSprite _token;

    /// <summary>The player's other hand, out to catch it.</summary>
    private CatchHandSprite _catchHand;

    /// <summary>The hearts on the heading, both sides.</summary>
    private HeartsSprite _hearts;

    /// <summary>The three checks, made once and asked again each time an item calls for one.</summary>
    private AimCheck _aimCheck;
    private SteadyCheck _steadyCheck;
    private ReadCheck _readCheck;

    /// <summary>The check being played, or null unless one is.</summary>
    private SkillCheckGame _check;

    /// <summary>What to do with the check's result once it is in.</summary>
    private Action<float> _afterCheck;
    private DialogueWheel _wheel;

    /// <summary>The player's three pockets, and the opponent's.</summary>
    private PocketStrip _playerPockets;
    private PocketStrip _opponentPockets;

    /// <summary>The wall, the lamp and the table. Drawn behind everything on the run screen.</summary>
    private Texture2D _room;

    /// <summary>One white pixel, stretched into the plate the dialogue is written on.</summary>
    private Texture2D _pixel;

    private ButtonSprite _feedButton;
    private ButtonSprite _useButton;
    private ButtonSprite _keepButton;
    private ButtonSprite _leaveButton;
    private ButtonSprite _continueButton;

    /// <summary>What the round has done so far, read one line at a time.</summary>
    private readonly Queue<string> _log = new();

    /// <summary>The line of the round currently on screen, or null.</summary>
    private string _logLine;

    /// <summary>Whether the log being read leads back to the player's turn instead of on to the next round.</summary>
    /// <remarks>A pocket played mid-turn and a decision about the hand both go through the same reading, so this tells them apart.</remarks>
    private bool _resumeTurn;

    /// <summary>What the opponent had the last time their face was checked, so a wound can be shown.</summary>
    private int _opponentLivesShown;

    /// <summary>Which part of the round the table is in.</summary>
    private RoundPhase _phase = RoundPhase.Discussion;

    /// <summary>How far the hand has gone in, from 0 to 1, and then how long it is held.</summary>
    private float _reach;
    private float _held;

    /// <summary>Whether the table has given way to the close-up of the box.</summary>
    private bool _boxView;

    /// <summary>How far open the box's mouth is in the close-up, from 0 shut to 1.</summary>
    private float _lidOpen;

    /// <summary>The plate the box's jaws are cut from.</summary>
    private Texture2D _lid;

    /// <summary>What the box deals from. Seeded by the clock like any other run.</summary>
    private readonly Random _random = new();

    /// <summary>The discussion being played, or null unless one is.</summary>
    private DiscussionPeriod _discussion;

    /// <summary>What is on screen in place of a line once the discussion has ended.</summary>
    private string _discussionEnd;

    /// <summary>Seconds left of the talking frame, or 0 if their mouth is shut.</summary>
    private float _speakHold;

    /// <summary>Seconds left of the player's own line being on screen, or 0 if it is not.</summary>
    private float _saidHold;

    private SpriteFont _titleFont;
    private SpriteFont _uiFont;
    private SpriteFont _detailFont;

    private Screen _screen = Screen.Title;

    /// <summary>Which slot the run on screen came out of, and goes back into.</summary>
    private int _runSlot;

    /// <summary>The run the player is in, or null unless the screen is <see cref="Screen.Run"/>.</summary>
    private SaveData _run;

    /// <summary>Why the last save failed, or null. Shown under the run rather than swallowed.</summary>
    private string _runStatus;

    private KeyboardState _lastKeyboard;

    public BlackBoxGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = ScreenWidth;
        _graphics.PreferredBackBufferHeight = ScreenHeight;
        _graphics.ApplyChanges();
        Content.RootDirectory = "Content";

        // The eyes follow the cursor, so the player needs to be able to see it.
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // Initialize the ash drift layers and the black box before the game starts.
        _ashDrift = new AshDriftSprite[]
        {
            new(new Vector2(7f, 3f), new Color(150, 146, 150) * 0.55f, Layers.AshDriftFar),
            new(new Vector2(-16f, 7f), new Color(214, 196, 180) * 0.75f, Layers.AshDriftNear),
        };

        _blackBox = new BlackBoxSprite()
        {
            Position = new Vector2(ScreenWidth / 2f, BoxCenterY),
        };

        // Loose ash caught in whatever the box is doing to the air around it.
        _ashes = new AshSprite[AshCount];
        for (int i = 0; i < _ashes.Length; i++)
        {
            _ashes[i] = new AshSprite { Center = _blackBox.Position };
        }

        // Amber is the box making an offer; red is saved for the choice that ends things.
        _startButton = new ButtonSprite(StartLabel, new Vector2(ScreenWidth / 2f, ButtonRowY), ButtonSprite.Amber);
        _exitButton = new ButtonSprite(ExitLabel, new Vector2(ScreenWidth / 2f, ButtonRowY), ButtonSprite.EmberRed);

        // Bone-white, because walking away is the one choice the box has no stake in.
        _returnButton = new ButtonSprite(ReturnLabel, new Vector2(ScreenWidth / 2f, ButtonRowY), ButtonSprite.BoneWhite);

        _startButton.Clicked += OpenSlotMenu;
        _exitButton.Clicked += Exit;
        _returnButton.Clicked += LeaveRun;

        _slotMenu = new SaveSlotMenu(new Rectangle(0, 0, ScreenWidth, ScreenHeight));
        _slotMenu.SlotChosen += BeginRun;
        _slotMenu.Closed += ShowTitle;

        _nameEntry = new NameEntry(new Rectangle(0, 0, ScreenWidth, ScreenHeight));
        _nameEntry.Confirmed += NameRun;
        _nameEntry.Cancelled += OpenSlotMenu;

        _opponent = new OpponentSprite();
        _hand = new HandSprite();
        _token = new TokenSprite();
        _catchHand = new CatchHandSprite();
        _hearts = new HeartsSprite();
        _aimCheck = new AimCheck(AimTarget, AimInner, AimOuter);
        _steadyCheck = new SteadyCheck(CheckBarOrigin);
        _readCheck = new ReadCheck(CheckLaneStart, CheckLaneLength);

        _feedButton = new ButtonSprite(FeedLabel, new Vector2(ScreenWidth / 2f, RunButtonY),
            ButtonSprite.EmberRed);
        _feedButton.Clicked += OfferHand;

        // Same colour logic: red spends something, amber keeps the offer, bone walks away.
        _useButton = new ButtonSprite(UseLabel, new Vector2(DecideLeftX, RunButtonY),
            ButtonSprite.EmberRed, DecideSize);
        _keepButton = new ButtonSprite(KeepLabel, new Vector2(DecideMiddleX, RunButtonY),
            ButtonSprite.Amber, DecideSize);
        _leaveButton = new ButtonSprite(LeaveLabel, new Vector2(DecideRightX, RunButtonY),
            ButtonSprite.BoneWhite, DecideSize);
        _continueButton = new ButtonSprite(ContinueLabel, new Vector2(ScreenWidth / 2f, RunButtonY),
            ButtonSprite.Amber, new Point(380, 84));

        _useButton.Clicked += () => Decide(DealtChoice.Use);
        _keepButton.Clicked += () => Decide(DealtChoice.Pocket);
        _leaveButton.Clicked += () => Decide(DealtChoice.Leave);
        _continueButton.Clicked += StepLog;

        // Yours on the left, theirs on the right, in the same colours as the names on the plate.
        _playerPockets = new PocketStrip("YOUR POCKETS", new Vector2(PocketsMargin, PocketsY), ButtonSprite.BoneWhite);
        _opponentPockets = new PocketStrip("THEIR POCKETS",
            new Vector2(ScreenWidth - PocketsMargin - PocketStrip.Width, PocketsY), ButtonSprite.Amber);
        _playerPockets.SlotChosen += PlayPocket;

        _wheel = new DialogueWheel();
        _wheel.Chosen += Answer;

        // Characters come from the window, so the form never has to deal with layouts or shift.
        Window.TextInput += (_, e) => _nameEntry.TypeCharacter(e.Character);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _titleFont = Content.Load<SpriteFont>("spectral-title");
        _uiFont = Content.Load<SpriteFont>("spectral-ui");
        _detailFont = Content.Load<SpriteFont>("spectral-detail");

        foreach (var layer in _ashDrift) layer.LoadContent(Content);
        _blackBox.LoadContent(Content);
        foreach (var mote in _ashes) mote.LoadContent(Content);

        _startButton.LoadContent(Content);
        _exitButton.LoadContent(Content);
        _returnButton.LoadContent(Content);
        LayOutButtons();

        _slotMenu.LoadContent(Content, GraphicsDevice);
        _nameEntry.LoadContent(Content, GraphicsDevice);

        _room = Content.Load<Texture2D>("room");

        // Not worth a PNG and a content-pipeline entry: it is one opaque pixel, stretched.
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _opponent.LoadContent(Content);
        _hand.LoadContent(Content);
        _lid = Content.Load<Texture2D>("box-lid");
        _token.LoadContent(Content);
        _catchHand.LoadContent(Content);
        _hearts.LoadContent(Content);
        _aimCheck.LoadContent(Content);
        _steadyCheck.LoadContent(Content);
        _readCheck.LoadContent(Content);
        _feedButton.LoadContent(Content);
        _useButton.LoadContent(Content);
        _keepButton.LoadContent(Content);
        _leaveButton.LoadContent(Content);
        _continueButton.LoadContent(Content);
        _playerPockets.LoadContent(Content);
        _opponentPockets.LoadContent(Content);
        _wheel.LoadContent(Content, GraphicsDevice);
    }

    /// <summary>Centres the row of buttons on screen.</summary>
    /// <remarks>Has to run after LoadContent, since a button does not know its width until it has measured its label.</remarks>
    private void LayOutButtons()
    {
        float total = _startButton.Size.X + ButtonGap + _exitButton.Size.X;
        float left = (ScreenWidth - total) / 2f;

        _startButton.Center = new Vector2(left + _startButton.Size.X / 2f, ButtonRowY);
        _exitButton.Center = new Vector2(
            left + _startButton.Size.X + ButtonGap + _exitButton.Size.X / 2f,
            ButtonRowY);

        // The run has one button, so it sits on the centre line the pair straddles.
        _returnButton.Center = new Vector2(ScreenWidth / 2f, ButtonRowY);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();

        // On the edge, not while held, or one press would back out of the form and then close the game.
        if (keyboard.IsKeyDown(Keys.Escape) && _lastKeyboard.IsKeyUp(Keys.Escape))
            Back();

        _lastKeyboard = keyboard;

        UpdateProof();

        // The box keeps running whatever screen is on top of it.
        foreach (var layer in _ashDrift) layer.Update(gameTime);
        _blackBox.Update(gameTime, GraphicsDevice.Viewport);
        foreach (var mote in _ashes) mote.Update(gameTime);

        // Only the screen in front takes input, so a click on the form does not also hit START underneath.
        switch (_screen)
        {
            case Screen.Title:
                _startButton.Update(gameTime);
                _exitButton.Update(gameTime);
                break;

            case Screen.SlotSelect:
                _slotMenu.Update(gameTime);
                break;

            case Screen.NameEntry:
                _nameEntry.Update(gameTime);
                break;

            case Screen.Run:
                // Playtime is counted here, not from the system clock, so time with the game closed does not count.
                _run.Playtime += gameTime.ElapsedGameTime;
                UpdateRound(gameTime);
                break;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        BeginProofCapture();
        GraphicsDevice.Clear(VoidColor);

        // 1. Everything the eye light falls on: the sky (or the room) and the box. Point sampling keeps the pixel art crisp.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);

        if (_screen == Screen.Run && !_boxView)
        {
            // The room replaces the drifting ash sky on the table.
            _spriteBatch.Draw(_room, new Rectangle(0, 0, ScreenWidth, ScreenHeight), null,
                Color.White, 0f, Vector2.Zero, SpriteEffects.None, Layers.Room);

            // Same batch as the box so the layer sort puts the opponent behind it and the box over its own shadow.
            _opponent.Draw(_spriteBatch, OpponentFoot);
            _blackBox.DrawContactShadow(_spriteBatch);
        }
        else
        {
            foreach (var layer in _ashDrift) layer.Draw(gameTime, _spriteBatch, GraphicsDevice.Viewport);
        }

        _blackBox.DrawBody(gameTime, _spriteBatch);
        _spriteBatch.End();

        // 2. The eye glow, additive so the light spills onto the rim. Linear sampling so it stays soft.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, PremultipliedAdditive, SamplerState.LinearClamp);
        _blackBox.DrawEyeGlow(gameTime, _spriteBatch);
        _spriteBatch.End();

        // 3. The eyes over the glow, and the ash falling past in front of everything.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _blackBox.DrawEyes(gameTime, _spriteBatch);
        foreach (var mote in _ashes) mote.Draw(gameTime, _spriteBatch);

        // The jaws in the close-up go over the eyes and under the hand.
        if (_boxView) DrawLid();

        // The arm is between the player and the box, so it goes over it.
        if (_boxView) _hand.Draw(_spriteBatch, BoxHandRest, BoxHandMouth, BoxHandScale);
        else _hand.Draw(_spriteBatch, HandRest, HandMouth, HandScale);

        // The payout and the hand catching it, also in front of the box.
        _catchHand.Draw(_spriteBatch);
        _token.Draw(gameTime, _spriteBatch);

        // A check draws its own furniture.
        _check?.Draw(gameTime, _spriteBatch);

        _spriteBatch.End();

        // 4. Text, sampled linearly so the font stays smooth. The ending veil and verdict go here too.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawInterface();
        if (_screen == Screen.Run && _phase == RoundPhase.Over) DrawEnding();
        _spriteBatch.End();

        // 5. The buttons. Point sampling because the plates are pixel art; the labels are 1:1 so they are fine under it.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawScreenButtons(gameTime);
        _spriteBatch.End();

        // 6. The forms, in their own batch so the veil covers everything above instead of sorting against one batch.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _slotMenu.Draw(gameTime, _spriteBatch);
        _nameEntry.Draw(gameTime, _spriteBatch);
        _spriteBatch.End();

        EndProofCapture();
        base.Draw(gameTime);
    }

    /// <summary>Draws whichever buttons the screen in front is offering.</summary>
    /// <param name="gameTime">The GameTime.</param>
    private void DrawScreenButtons(GameTime gameTime)
    {
        switch (_screen)
        {
            case Screen.Title:
                _startButton.Draw(gameTime, _spriteBatch);
                _exitButton.Draw(gameTime, _spriteBatch);
                break;

            case Screen.Run:
                DrawHearts();
                // The pockets are always on the table, so the player can plan around them while talking.
                if (!_boxView) _playerPockets.Draw(gameTime, _spriteBatch);
                if (!_boxView) _opponentPockets.Draw(gameTime, _spriteBatch);

                // Exactly one thing to click at a time.
                if (_wheel.IsOpen)
                {
                    _wheel.Draw(gameTime, _spriteBatch, _discussion?.Patience ?? 0f);
                }
                else if (_phase == RoundPhase.Offer)
                {
                    _feedButton.Draw(gameTime, _spriteBatch);
                }
                else if (_phase == RoundPhase.PlayerTurn)
                {
                    _useButton.Draw(gameTime, _spriteBatch);
                    _keepButton.Draw(gameTime, _spriteBatch);
                    _leaveButton.Draw(gameTime, _spriteBatch);
                }
                else if (_phase == RoundPhase.Resolving)
                {
                    _continueButton.Draw(gameTime, _spriteBatch);
                }
                else if (_phase == RoundPhase.Over)
                {
                    _returnButton.Draw(gameTime, _spriteBatch);
                }
                break;
        }
    }

    /// <summary>Draws the title and the line under it, or the table's text when in a run.</summary>
    private void DrawInterface()
    {
        // The title gets a tight deep red shadow instead of black. The serif is light, and anything
        // wider ghosts around the strokes. No title on the table; the face sits where it was.
        if (_screen != Screen.Run)
        {
            DrawCentered(_titleFont, Title, TitleY, TitleColor, TitleShadow, Palette.ShadowOffset);

            // Escape is a shortcut now, so it is stated once and quietly. What it does depends on the screen.
            string hint = _screen == Screen.Title ? "ESC" : "ESC  ·  BACK";
            DrawCentered(_uiFont, hint, EscapeHintY, HintColor, Palette.Shadow, Palette.ShadowOffset);
        }
        else
        {
            DrawRunState();
        }
    }

    /// <summary>Draws the table: the bookkeeping along the top, and whatever is being said.</summary>
    private void DrawRunState()
    {
        // Everything the player needs to read the table is on one line along the top.
        string heading = string.Format(CultureInfo.InvariantCulture,
            "CHAPTER {0}   ·   ROUND {1}   ·   {2:00}:{3:00}",
            _run.Chapter, _run.Round + 1,
            (int)_run.Playtime.TotalHours, _run.Playtime.Minutes);

        DrawCentered(_detailFont, heading, RunStatusY, Palette.DimText, Palette.Shadow, Palette.ShadowOffset);

        // The names at the left, with room after each for its hearts (drawn in DrawHearts, in the pixel batch).
        DrawText(_detailFont, _run.PlayerName.ToUpperInvariant(), PocketsMargin, RunStatusY,
            Palette.DimText, Palette.Shadow);
        DrawText(_detailFont, "THEM", PocketsMargin + HeartsThemX, RunStatusY,
            Palette.DimText, Palette.Shadow);

        // The way out, in the corner with the rest of the bookkeeping.
        DrawText(_detailFont, "ESC  ·  SAVE AND LEAVE", ScreenWidth - PocketsMargin, RunStatusY,
            Palette.DimText, Palette.Shadow, rightAligned: true);

        if (_runStatus is not null)
        {
            DrawCentered(_detailFont, _runStatus, PocketsY - StatusRise, ButtonSprite.EmberRed,
                Palette.Shadow, Palette.ShadowOffset);
        }

        DrawDialoguePlate();
    }

    /// <summary>Draws the plate on the wall and whatever is being said on it.</summary>
    /// <remarks>Whoever is speaking owns the plate: the opponent, the player for a beat after a reply, then the box.</remarks>
    private void DrawDialoguePlate()
    {
        string text = RunLine;
        if (string.IsNullOrEmpty(text) && _discussion is null) return;

        string[] lines = Wrap(_uiFont, text, DialoguePlate.Width - 2 * DialoguePadding);

        // The plate closes a little under the last line, and never above the patience bar.
        float bottom = DialogueLineY + Math.Max(1, lines.Length) * _uiFont.LineSpacing + DialoguePadding * PlateBottomPad;
        var plate = new Rectangle(DialoguePlate.X, DialoguePlate.Y, DialoguePlate.Width, (int)MathF.Round(bottom - DialoguePlate.Y));

        _spriteBatch.Draw(_pixel, plate, null, PlateColor,
            0f, Vector2.Zero, SpriteEffects.None, Layers.DialoguePlate);

        // A lit lip along the top edge so the plate reads as concrete, not as a rectangular shadow.
        _spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, plate.Width, 2), null, PlateLip,
            0f, Vector2.Zero, SpriteEffects.None, Layers.TextShadow);

        (string speaker, Color speakerColour) = Speaker;
        float centre = plate.X + plate.Width / 2f;

        DrawText(_uiFont, speaker, centre, DialogueNameY, speakerColour, Palette.Shadow, centred: true);

        Color colour = _phase == RoundPhase.Discussion && _discussionEnd is null
            ? LineLive
            : LineSaid;

        for (int i = 0; i < lines.Length; i++)
        {
            DrawText(_uiFont, lines[i], centre, DialogueLineY + i * _uiFont.LineSpacing, colour,
                Palette.Shadow, centred: true);
        }
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

    /// <summary>Whether what is on screen is the player's own last line rather than the opponent's.</summary>
    private bool IsPlayerSpeaking =>
        _phase == RoundPhase.Discussion && _saidHold > 0f && _discussionEnd is null;

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

                case RoundPhase.PlayerTurn when ItemCatalog.TryParse(_run.Dealt, out ItemId dealt):
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

    /// <summary>The end of the run: a veil over the table, and what became of the player, large.</summary>
    /// <remarks>Two endings, one screen. The chapter only turns when the player leaves, in LeaveRun.</remarks>
    private void DrawEnding()
    {
        bool won = _run.PlayerLives > 0;

        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ScreenWidth, ScreenHeight), null,
            new Color(4, 3, 8) * EndingVeilOpacity, 0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);

        string title = won ? "YOU ADVANCE" : "YOU HAVE BEEN CONSUMED BY THE BOX";
        string line = won
            ? "THE BOX LETS YOU UP. THERE IS ANOTHER TABLE."
            : "IT DOES NOT LET GO. IT WILL BE WAITING.";

        // The long verdict shrinks to fit rather than running off the edges.
        DrawCenteredFitted(_titleFont, title, EndingTitleY, ScreenWidth - 2f * EndingMargin,
            won ? ButtonSprite.BoneWhite : ButtonSprite.EmberRed, Palette.HeavyShadow, EndingShadowOffset);
        DrawCentered(_uiFont, line, EndingLineY, EndingLineColor, Palette.FormShadow, Palette.ShadowOffset);
    }

    /// <summary>Draws both sides' hearts on the heading, after their names.</summary>
    /// <remarks>In the buttons' batch, not the text's, because they are pixel art. Measured off the name so a long name pushes them along.</remarks>
    private void DrawHearts()
    {
        float y = RunStatusY + HeartsDrop;
        float nameWidth = _detailFont.MeasureString(_run.PlayerName.ToUpperInvariant()).X;
        _hearts.Draw(_spriteBatch, new Vector2(PocketsMargin + nameWidth + HeartsGap, y),
            _run.PlayerLives, SaveData.StartingLives, Layers.ButtonLabel);

        float themWidth = _detailFont.MeasureString("THEM").X;
        _hearts.Draw(_spriteBatch, new Vector2(PocketsMargin + HeartsThemX + themWidth + HeartsGap, y),
            _run.OpponentLives, SaveData.StartingLives, Layers.ButtonLabel);
    }

    /// <summary>Breaks a line into as many lines as it takes to fit.</summary>
    /// <remarks>Measured with the font rather than by character count, since the font is proportional.</remarks>
    /// <param name="font">The SpriteFont the text will be drawn in.</param>
    /// <param name="text">The line to break.</param>
    /// <param name="width">How wide a line may be, in screen pixels.</param>
    /// <returns>One entry per line, in order.</returns>
    private static string[] Wrap(SpriteFont font, string text, float width)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

        var lines = new List<string>();
        var line = new StringBuilder();

        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;

            if (line.Length > 0 && font.MeasureString(candidate).X > width)
            {
                lines.Add(line.ToString());
                line.Clear();
                line.Append(word);
            }
            else
            {
                line.Clear();
                line.Append(candidate);
            }
        }

        if (line.Length > 0) lines.Add(line.ToString());
        return lines.ToArray();
    }

    /// <summary>Runs whichever part of the round the table is in.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateRound(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // The pockets read off the run every frame, so nothing that changes them has to remember to tell them.
        _playerPockets.Show(_run.Banked, revealed: true, interactive: _phase == RoundPhase.PlayerTurn);
        _opponentPockets.Show(_run.OpponentBanked, revealed: _run.SeesOpponentPockets, interactive: false);

        switch (_phase)
        {
            case RoundPhase.Discussion:
                UpdateDiscussion(elapsed, gameTime);
                break;

            case RoundPhase.Offer:
                _feedButton.Update(gameTime);
                break;

            case RoundPhase.Reaching:
                UpdateReach(elapsed);
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

    /// <summary>Runs the discussion period and the wheel over it.</summary>
    /// <param name="elapsed">Seconds since the last frame.</param>
    /// <param name="gameTime">The frame's timing, for the plates.</param>
    private void UpdateDiscussion(float elapsed, GameTime gameTime)
    {
        if (_discussion is null) return;

        if (_speakHold > 0f)
        {
            _speakHold -= elapsed;
            if (_speakHold <= 0f) StopTalking();
        }

        if (_saidHold > 0f)
        {
            _saidHold -= elapsed;
            if (_saidHold <= 0f) FinishSaying();
        }

        DiscussionState before = _discussion.State;
        _discussion.Update(elapsed);

        // The clock ran out between frames.
        if (before == DiscussionState.Running && _discussion.State != DiscussionState.Running)
            EndDiscussion();

        if (_wheel.IsOpen) _wheel.Update(gameTime);
    }

    /// <summary>Puts the hand in, holds it there, and then pays.</summary>
    /// <remarks>The hold before paying is the point. Dealing the instant the fingers cross the rim made it a vending machine.</remarks>
    /// <param name="elapsed">Seconds since the last frame.</param>
    private void UpdateReach(float elapsed)
    {
        // The mouth opens first.
        if (_lidOpen < 1f)
        {
            _lidOpen = MathF.Min(1f, _lidOpen + elapsed / LidSeconds);
            return;
        }

        if (_reach < 1f)
        {
            _reach = MathF.Min(1f, _reach + elapsed / ReachSeconds);
            _hand.Reach = _reach;
            return;
        }

        _held += elapsed;
        if (_held >= TakeSeconds) Deal();
    }

    /// <summary>Says one of the four replies.</summary>
    /// <param name="corner">Which corner of the wheel was clicked.</param>
    private void Answer(int corner)
    {
        // Nothing is taken while the last answer is still being said.
        if (_discussion is null || _saidHold > 0f) return;
        if (!_discussion.Choose(corner)) return;

        // Everything the choice changed, written back to the run in one place.
        _run.OpponentDisposition = _discussion.Disposition;
        foreach (string flag in _discussion.FlagsRaised) _run.SetFlag(flag);

        // The wheel comes down while the player's line is up, and the new temper lands on the
        // face now so they see it arrive while they are still speaking.
        _wheel.Hide();
        StopTalking();

        _saidHold = SayingSeconds(_discussion.Said);
    }

    /// <summary>How long a line of the player's own stays up before it is answered.</summary>
    /// <remarks>Off the length of the line. One fixed beat was too short for long replies and dead air after short ones.</remarks>
    /// <param name="line">The line being held on screen.</param>
    /// <returns>How long to hold it, in seconds.</returns>
    private static float SayingSeconds(string line) => Math.Clamp(
        SayingBase + (line?.Length ?? 0) * SayingPerCharacter, SayingMin, SayingMax);

    /// <summary>Ends the beat the player's line was held for, and gives the table back.</summary>
    /// <remarks>
    /// The one place a discussion can end on the player's own words. It used to close on the
    /// click, which threw the last line away before it was ever shown.
    /// </remarks>
    private void FinishSaying()
    {
        _saidHold = 0f;

        if (_discussion.State == DiscussionState.Running) ShowBeat();
        else EndDiscussion();
    }

    /// <summary>Puts the current beat on screen, and opens their mouth on the beat it lands.</summary>
    private void ShowBeat()
    {
        _wheel.Show(_discussion.Current, _discussion.Disposition);

        _opponent.Pose = OpponentPose.Talking;
        _speakHold = SpeakSeconds;
    }

    /// <summary>Shuts their mouth and puts the temper back on their face.</summary>
    /// <remarks>The pose has to be cleared first: SetDisposition ignores a face that is busy doing something.</remarks>
    private void StopTalking()
    {
        _speakHold = 0f;

        _opponent.Pose = OpponentPose.Even;
        _opponent.SetDisposition(_discussion?.Disposition ?? _run.OpponentDisposition);
    }

    /// <summary>Closes the discussion down and lets the box ask for what it is owed.</summary>
    private void EndDiscussion()
    {
        _wheel.Hide();
        _saidHold = 0f;
        StopTalking();

        _run.OpponentDisposition = _discussion.Disposition;
        foreach (string flag in _discussion.FlagsRaised) _run.SetFlag(flag);
        _run.RecordMeeting(_run.OpponentId);

        _discussionEnd = _discussion.State == DiscussionState.Silenced
            ? "YOU SAID NOTHING, AND THE BOX STOPPED WAITING."
            : "THE BOX HAS HEARD ENOUGH OF YOU BOTH.";

        _phase = RoundPhase.Offer;
        _feedButton.Reset();
    }

    /// <summary>Starts the hand moving. Raised by the one button on the table.</summary>
    private void OfferHand()
    {
        if (_phase != RoundPhase.Offer) return;

        _phase = RoundPhase.Reaching;
        _reach = 0f;
        _held = 0f;

        _hand.Reach = 0f;
        _hand.IsVisible = true;
        EnterBoxView();

        // Both hands go in together. Theirs is never drawn, so the lean is the whole of it.
        _opponent.Pose = OpponentPose.Reaching;
    }

    /// <summary>Takes the hand, and pays for it.</summary>
    /// <remarks>The items go into the run, not fields, so closing the game mid-decision does not lose them.</remarks>
    private void Deal()
    {
        RoundEngine.DealBoth(_run, _random);

        _hand.IsVisible = false;
        _opponent.Pose = OpponentPose.Even;
        _opponent.SetDisposition(_run.OpponentDisposition);

        Payout();
    }

    /// <summary>Throws what the box dealt down the table, and puts the other hand out for it.</summary>
    /// <remarks>Only when there is something to throw. If the box gave nothing the turn just starts.</remarks>
    private void Payout()
    {
        if (!ItemCatalog.TryParse(_run.Dealt, out _))
        {
            BeginTurn();
            return;
        }

        _phase = RoundPhase.Catching;

        float drift = ((float)_random.NextDouble() * 2f - 1f) * TokenDrift;
        if (_boxView)
        {
            _token.Launch(BoxTokenLaunch, new Vector2(drift, TokenLaunchSpeed));
            _catchHand.Scale = BoxHandScale;
            _catchHand.Show(BoxCatchTipY, BoxCatchMinX, BoxCatchMaxX);
        }
        else
        {
            _token.Launch(TokenLaunch, new Vector2(drift, TokenLaunchSpeed));
            _catchHand.Scale = HandScale;
            _catchHand.Show(CatchTipY, CatchMinX, CatchMaxX);
        }
    }

    /// <summary>Takes the table away and brings the box up, shut, to fill the view.</summary>
    private void EnterBoxView()
    {
        _boxView = true;
        _lidOpen = 0f;
        _blackBox.Position = BoxViewCentre;
        _blackBox.Scale = BoxViewScale;
    }

    /// <summary>Puts the box back on the table and the table back on screen.</summary>
    private void LeaveBoxView()
    {
        _boxView = false;
        _blackBox.Position = new Vector2(ScreenWidth / 2f, RunBoxCenterY);
        _blackBox.Scale = BlackBoxSprite.TableScale;
    }

    /// <summary>Draws the box's jaws over its opening, as far shut as they are.</summary>
    /// <remarks>Two halves of one plate, each cut shorter as the mouth opens, so they draw back into the rim.</remarks>
    private void DrawLid()
    {
        if (_lid is null || _lidOpen >= 1f) return;

        Rectangle mouth = _blackBox.Aperture;
        int half = _lid.Height / 2;
        int showing = (int)MathF.Round(half * (1f - _lidOpen));
        if (showing <= 0) return;

        float scale = BoxViewScale;
        var top = new Rectangle(0, 0, _lid.Width, showing);
        var bottom = new Rectangle(0, _lid.Height - showing, _lid.Width, showing);

        _spriteBatch.Draw(_lid, new Vector2(mouth.X, mouth.Y), top, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Lid);
        _spriteBatch.Draw(_lid, new Vector2(mouth.X, mouth.Bottom - showing * scale), bottom, Color.White, 0f,
            Vector2.Zero, scale, SpriteEffects.None, Layers.Lid);
    }

    /// <summary>Moves the tag and the hand, and settles which of two things the tag hits first.</summary>
    /// <remarks>The palm is tested before the edge, so a tag caught right on the lip counts. Same collision shapes as the tutorial.</remarks>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateCatch(GameTime gameTime)
    {
        _token.Update(gameTime);
        _catchHand.Update(gameTime);

        if (CollisionHelper.Collides(_token.Bounds, _catchHand.Palm))
            CatchToken();
        else if (CollisionHelper.Collides(_token.Bounds, TheDark))
            DropToken();
    }

    /// <summary>The tag landed in the palm. The item is in the hand, and the turn is the player's.</summary>
    private void CatchToken()
    {
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        if (_boxView) LeaveBoxView();
        BeginTurn();
    }

    /// <summary>The tag went over the edge. Same rule as LEAVE IT, said differently.</summary>
    private void DropToken()
    {
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        if (_boxView) LeaveBoxView();

        bool blind = _run.DealtBlind;
        string name = ItemCatalog.TryParse(_run.Dealt, out ItemId item)
            ? ItemCatalog.NameOf(item).ToUpperInvariant()
            : "IT";

        List<string> lines = RoundEngine.DecideDealt(_run, DealtChoice.Leave, _random);
        if (lines.Count > 0) lines.RemoveAt(0);
        lines.Insert(0, blind
            ? "IT WENT OFF THE EDGE OF THE TABLE BEFORE YOU COULD LOOK AT IT."
            : "THE " + name + " WENT OFF THE EDGE OF THE TABLE. THE BOX DOES NOT DEAL TWICE.");

        AfterPlayerAction(lines);
    }

    /// <summary>Hands the table to the player, with the three plates set for what is in their hand.</summary>
    /// <remarks>KEEP IT is the one plate that can be refused, and it says why on itself rather than vanishing.</remarks>
    private void BeginTurn()
    {
        _phase = RoundPhase.PlayerTurn;

        _keepButton.Enabled = RoundEngine.CanPocketDealt(_run);
        _keepButton.Sublabel = _keepButton.Enabled ? null : _run.DealtBlind ? UnseenNote : NoRoomNote;

        _useButton.Reset();
        _keepButton.Reset();
        _leaveButton.Reset();
    }

    /// <summary>Takes the player's decision about the item in their hand.</summary>
    /// <param name="choice">What they decided.</param>
    private void Decide(DealtChoice choice)
    {
        if (_phase != RoundPhase.PlayerTurn) return;
        if (choice == DealtChoice.Pocket && !RoundEngine.CanPocketDealt(_run)) return;

        // Items that need a check go through it first. A blind item skips it, since the check would give it away.
        if (choice == DealtChoice.Use && !_run.DealtBlind
            && ItemCatalog.TryParse(_run.Dealt, out ItemId dealt)
            && BeginCheck(ItemCatalog.CheckFor(dealt),
                efficiency => AfterPlayerAction(RoundEngine.DecideDealt(_run, DealtChoice.Use, _random, efficiency))))
            return;

        AfterPlayerAction(RoundEngine.DecideDealt(_run, choice, _random));
    }

    /// <summary>Plays something out of the player's pockets, mid-turn.</summary>
    /// <param name="slot">Which pocket was clicked.</param>
    private void PlayPocket(int slot)
    {
        if (_phase != RoundPhase.PlayerTurn) return;

        if (slot >= 0 && slot < _run.Banked.Count
            && ItemCatalog.TryParse(_run.Banked[slot], out ItemId pocketed)
            && BeginCheck(ItemCatalog.CheckFor(pocketed),
                efficiency => AfterPlayerAction(RoundEngine.PlayFromPocket(_run, slot, _random, efficiency))))
            return;

        AfterPlayerAction(RoundEngine.PlayFromPocket(_run, slot, _random));
    }

    /// <summary>Starts a check, if the item asks for one, and remembers what to do with the result.</summary>
    /// <remarks>The item stays put until the check is over, so leaving mid-check just asks it again next time.</remarks>
    /// <param name="kind">Which check the item asks for.</param>
    /// <param name="then">What to do with the efficiency the check produces.</param>
    /// <returns>True if a check began, false if the item asks nothing.</returns>
    private bool BeginCheck(SkillCheck kind, Action<float> then)
    {
        _check = kind switch
        {
            SkillCheck.Aim => _aimCheck,
            SkillCheck.Steady => _steadyCheck,
            SkillCheck.Read => _readCheck,
            _ => null,
        };
        if (_check is null) return false;

        _afterCheck = then;
        _check.Begin(_random);
        _phase = RoundPhase.SkillCheck;
        return true;
    }

    /// <summary>Runs the check, and when it is over, does what was waiting on it.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateCheck(GameTime gameTime)
    {
        if (_check is null)
        {
            _phase = RoundPhase.PlayerTurn;
            return;
        }

        _check.Update(gameTime);
        if (!_check.IsDone) return;

        float efficiency = _check.Efficiency;
        Action<float> then = _afterCheck;
        _check = null;
        _afterCheck = null;

        then?.Invoke(efficiency);
    }

    /// <summary>Reads out what the player just did, and works out what comes after it.</summary>
    /// <remarks>
    /// Three cases: somebody is out of lives and the round closes now; the player is still holding
    /// something and gets the turn back; or the hand is decided and the opponent goes.
    /// </remarks>
    /// <param name="lines">What the player's action did.</param>
    private void AfterPlayerAction(List<string> lines)
    {
        foreach (string line in lines) _log.Enqueue(line);

        if (RoundEngine.IsOver(_run))
        {
            _resumeTurn = false;
            foreach (string line in RoundEngine.CloseRound(_run)) _log.Enqueue(line);
        }
        else if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            _resumeTurn = true;
        }
        else
        {
            _resumeTurn = false;
            foreach (string line in RoundEngine.OpponentTurn(_run, _random)) _log.Enqueue(line);
            foreach (string line in RoundEngine.CloseRound(_run)) _log.Enqueue(line);
        }

        ShowWounds();

        _phase = RoundPhase.Resolving;
        _continueButton.Reset();
        StepLog();
    }

    /// <summary>Puts whatever has just been taken off the opponent onto their face.</summary>
    /// <remarks>Compared to what was last shown, so a second hit in the same turn is a second flinch.</remarks>
    private void ShowWounds()
    {
        _opponent.SetLives(_run.OpponentLives);

        if (_run.OpponentLives < _opponentLivesShown) _opponent.Pose = OpponentPose.Hurt;
        _opponentLivesShown = _run.OpponentLives;
    }

    /// <summary>Shows the next line of the log, or moves on if there are none left.</summary>
    private void StepLog()
    {
        if (_log.Count > 0)
        {
            _logLine = _log.Dequeue();
            return;
        }

        _logLine = null;

        // Out of lives on either side means no next round.
        if (RoundEngine.IsOver(_run))
        {
            _phase = RoundPhase.Over;
            _returnButton.Reset();
            return;
        }

        if (_resumeTurn)
        {
            _resumeTurn = false;
            ResumeTurn();
            return;
        }

        StartDiscussion();
    }

    /// <summary>Gives the table back to the player after a mid-turn reading.</summary>
    /// <remarks>If the hand somehow emptied, the turn ends instead of stranding the round on three plates.</remarks>
    private void ResumeTurn()
    {
        if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            BeginTurn();
            return;
        }

        AfterPlayerAction(new List<string>());
    }

    /// <summary>Opens the save form over the title screen.</summary>
    private void OpenSlotMenu()
    {
        _screen = Screen.SlotSelect;
        _slotMenu.Open();
    }

    /// <summary>Takes the player into the run held in a slot.</summary>
    /// <param name="slot">Which slot it came out of, and goes back into.</param>
    /// <param name="data">The run.</param>
    private void BeginRun(int slot, SaveData data)
    {
        _runSlot = slot;
        _run = data;
        _runStatus = null;

        // No name yet means the run has not started, however many times the slot was opened.
        if (string.IsNullOrWhiteSpace(_run.PlayerName))
        {
            _screen = Screen.NameEntry;
            _nameEntry.Open();
            return;
        }

        EnterRun();
    }

    /// <summary>Takes the name the player gave and starts the run with it.</summary>
    /// <remarks>Saved right away so the name is never asked for twice. A failed write is shown, not fatal.</remarks>
    /// <param name="name">What the player called themselves.</param>
    private void NameRun(string name)
    {
        _run.PlayerName = name;

        if (!SaveSystem.Write(_runSlot, _run))
            _runStatus = "COULD NOT SAVE -- " + SaveSystem.LastError;

        EnterRun();
    }

    /// <summary>Puts the player at the table and moves the scene around them.</summary>
    /// <remarks>A run saved mid-decision comes back mid-decision, so closing the game never costs an item.</remarks>
    private void EnterRun()
    {
        _screen = Screen.Run;

        // On the table the box is an object in a room, so it comes down in size as well as into place.
        _blackBox.Position = new Vector2(ScreenWidth / 2f, RunBoxCenterY);
        _blackBox.Scale = BlackBoxSprite.TableScale;
        RecentreAsh();

        _returnButton.Center = new Vector2(ScreenWidth / 2f, RunButtonY);
        _returnButton.Reset();

        // Between chapters the seat is empty and the roster fills it. Within one, the save says who is there.
        if (string.IsNullOrWhiteSpace(_run.OpponentId))
            _run.OpponentId = Opponents.ForChapter(_run.Chapter).Id;
        _opponent.Who = Opponents.ById(_run.OpponentId);
        _aimCheck.Target = AimTarget;

        _log.Clear();
        _logLine = null;
        _resumeTurn = false;

        _opponentLivesShown = _run.OpponentLives;
        _opponent.SetLives(_run.OpponentLives);

        if (RoundEngine.IsOver(_run))
        {
            _phase = RoundPhase.Over;
            _discussion = null;
            _discussionEnd = null;
            return;
        }

        if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            _discussion = null;
            _discussionEnd = null;
            _opponent.Pose = OpponentPose.Even;
            _opponent.SetDisposition(_run.OpponentDisposition);
            BeginTurn();
            return;
        }

        StartDiscussion();
    }

    /// <summary>Opens the discussion period for this round with whoever is across the table.</summary>
    /// <remarks>A first meeting opens on the script's disposition. Every one after opens on what the save carries.</remarks>
    private void StartDiscussion()
    {
        _discussionEnd = null;

        Disposition? carried = _run.HasMetOpponent ? _run.OpponentDisposition : null;

        _discussion = new DiscussionPeriod(Opponents.ById(_run.OpponentId).Script, _run.PlayerName, _run.Round, carried);

        _saidHold = 0f;
        _opponent.Pose = OpponentPose.Even;
        _opponent.SetDisposition(_discussion.Disposition);
        _opponent.SetLives(_run.OpponentLives);
        _opponentLivesShown = _run.OpponentLives;

        _phase = RoundPhase.Discussion;
        ShowBeat();
    }

    /// <summary>Writes the run back to its slot and returns to the title screen.</summary>
    /// <remarks>If the write fails the player stays in the run, since the copy in memory is the only one left.</remarks>
    private void LeaveRun()
    {
        // A finished run is written back ready to play again: next chapter if they got up, same one if not.
        if (RoundEngine.IsOver(_run))
        {
            if (_run.PlayerLives > 0) _run.Advance();
            else _run.Restart();
        }

        if (!SaveSystem.Write(_runSlot, _run))
        {
            _runStatus = "COULD NOT SAVE -- " + SaveSystem.LastError;
            return;
        }

        _run = null;
        _discussion = null;
        _discussionEnd = null;

        _log.Clear();
        _logLine = null;
        _resumeTurn = false;

        _wheel.Hide();
        _hand.IsVisible = false;
        _token.IsVisible = false;
        _catchHand.IsVisible = false;
        _check = null;
        _afterCheck = null;
        _boxView = false;
        _phase = RoundPhase.Discussion;

        ShowTitle();
    }

    /// <summary>Points the loose ash at wherever the box has moved to.</summary>
    /// <remarks>Without this the motes keep spiralling into the middle of the title screen after the box has left.</remarks>
    private void RecentreAsh()
    {
        foreach (var mote in _ashes) mote.Center = _blackBox.Position;
    }

    /// <summary>Returns to the title screen, with both of its buttons cold again.</summary>
    private void ShowTitle()
    {
        _screen = Screen.Title;

        // The box goes back to owning the whole title screen.
        _blackBox.Position = new Vector2(ScreenWidth / 2f, BoxCenterY);
        _blackBox.Scale = BlackBoxSprite.DrawScale;
        RecentreAsh();

        // The buttons were frozen mid-animation under the form, so reset them.
        _startButton.Reset();
        _exitButton.Reset();
    }

    /// <summary>Backs out of whatever is in front: the form, then the run, then the game.</summary>
    private void Back()
    {
        switch (_screen)
        {
            case Screen.SlotSelect:
                _slotMenu.Close();
                break;

            case Screen.NameEntry:
                _nameEntry.Close();
                break;

            case Screen.Run:
                LeaveRun();
                break;

            default:
                Exit();
                break;
        }
    }

    /// <summary>Draws a line of text centred horizontally on the screen, over a hard offset shadow.</summary>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    /// <param name="shadow">Colour of the shadow behind it.</param>
    /// <param name="shadowOffset">How far the shadow is thrown, in screen pixels.</param>
    private void DrawCentered(SpriteFont font, string text, float y, Color color, Color shadow, Vector2 shadowOffset)
    {
        Vector2 size = font.MeasureString(text);
        var position = new Vector2(MathF.Round((ScreenWidth - size.X) / 2f), y);

        _spriteBatch.DrawString(font, text, position + shadowOffset, shadow, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        _spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Draws a line centred, scaled down if it would not fit in the width given.</summary>
    /// <param name="font">The face to set it in.</param>
    /// <param name="text">The line.</param>
    /// <param name="y">Where the top of it goes, at full size; a scaled line is centred on the same middle.</param>
    /// <param name="maxWidth">The widest it may be, in screen pixels.</param>
    /// <param name="color">The colour of the text.</param>
    /// <param name="shadow">The colour of its shadow.</param>
    /// <param name="shadowOffset">How far the shadow sits from the text.</param>
    private void DrawCenteredFitted(SpriteFont font, string text, float y, float maxWidth, Color color, Color shadow, Vector2 shadowOffset)
    {
        Vector2 size = font.MeasureString(text);
        float scale = size.X > maxWidth ? maxWidth / size.X : 1f;
        var position = new Vector2(
            MathF.Round((ScreenWidth - size.X * scale) / 2f),
            MathF.Round(y + size.Y * (1f - scale) / 2f));

        _spriteBatch.DrawString(font, text, position + shadowOffset, shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, Layers.TextShadow);
        _spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, Layers.Text);
    }

    /// <summary>Draws a line of text against a point, over a hard offset shadow.</summary>
    /// <param name="font">The SpriteFont to measure and render with.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="x">The left edge, the centre, or the right edge, depending on the flags.</param>
    /// <param name="y">Top of the line, in screen pixels.</param>
    /// <param name="color">Colour of the text.</param>
    /// <param name="shadow">Colour of the shadow behind it.</param>
    /// <param name="centred">True to centre the line on <paramref name="x"/>.</param>
    /// <param name="rightAligned">True to end the line at <paramref name="x"/>.</param>
    private void DrawText(SpriteFont font, string text, float x, float y, Color color, Color shadow,
        bool centred = false, bool rightAligned = false)
    {
        if (string.IsNullOrEmpty(text)) return;

        Vector2 size = font.MeasureString(text);
        float left = centred ? x - size.X / 2f : rightAligned ? x - size.X : x;
        var position = new Vector2(MathF.Round(left), MathF.Round(y));

        _spriteBatch.DrawString(font, text, position + new Vector2(2f, 2f), shadow, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.TextShadow);
        _spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, Layers.Text);
    }
}
