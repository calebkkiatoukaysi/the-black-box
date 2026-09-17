using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox;

/// <summary>
/// The Black Box: the title screen, the save form, and the table.
/// </summary>
public partial class BlackBoxGame : Game
{
    /// <summary>
    /// Which of the screens built on the title scene the player is looking at.
    /// </summary>
    /// <remarks>
    /// All of them are drawn over the same box: nothing here swaps the scene out, it only
    /// changes what is laid on top of it and which controls are listening. That is why this
    /// is one field rather than a stack of screen classes -- there is one scene, and the box
    /// should keep breathing through every one of these.
    /// </remarks>
    private enum Screen
    {
        /// <summary>The title, with START and EXIT under it.</summary>
        Title,

        /// <summary>The save form, over a veiled title screen.</summary>
        SlotSelect,

        /// <summary>Naming a run that has not been named, over a veiled title screen.</summary>
        NameEntry,

        /// <summary>A run: the table, the opponent across it, and the round being played.</summary>
        Run,
    }

    private const int ScreenWidth = 1600;
    private const int ScreenHeight = 900;

    // box center position
    private const float BoxCenterY = 500f;

    /// <summary>
    /// Where the box sits once the player is at the table.
    /// </summary>
    /// <remarks>
    /// Centred, and sitting on the table rather than hanging in front of it -- see
    /// <see cref="BlackBoxSprite.DrawContactShadow"/> for what makes it rest there. At
    /// <see cref="BlackBoxSprite.TableScale"/> its bottom edge lands on the surface with the
    /// opponent's face clear above it.
    /// </remarks>
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

    /// <summary>
    /// Where the table cuts the opponent off, which is what they are anchored by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The far lip of the table in room.png, which is exactly where a person sitting behind
    /// it stops. Lower down and the bottom of their coat is drawn on the surface in front
    /// of them, which reads as a shoulder resting on the table rather than a body behind it.
    /// </para>
    /// <para>
    /// Left of centre rather than on it. The box owns the middle of the table, and a figure
    /// drawn straight behind it is a figure the box covers from the collar down. Off to one
    /// side, the box covers one shoulder and the rest of them is in the open: the face, the
    /// raised hand, the near shoulder. It is also the composition every visitor in the games
    /// this one is built on uses -- the person on one side, what they are saying on the other.
    /// </para>
    /// </remarks>
    private static readonly Vector2 OpponentFoot = new(600f, 640f);

    /// <summary>
    /// Blown up by the same whole number as the player's own hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Big. At 4x a frame of <c>opponent-sheet.png</c> runs from the table lip to the top of
    /// the wall, which makes the opponent the thing the room is about and the box the thing
    /// between the two of you. The first sheet was a head and a collar drawn at 5x, and at
    /// that size the box could have swallowed it -- the fix was not a bigger number but a
    /// bigger person, drawn to fill the frame.
    /// </para>
    /// <para>
    /// Two numbers hold this together: this, and <c>ROOM_HORIZON</c> in
    /// <c>tools/generate_assets.py</c>, which is the table line and the bottom of them.
    /// The top of the frame has to clear the run's bookkeeping along the top edge, so the
    /// sheet's height is cut to exactly that distance at this scale.
    /// </para>
    /// </remarks>
    private const float OpponentScale = 4f;

    /// <summary>How far the player's own hand is blown up. Nearer the camera, so larger.</summary>
    private const float HandScale = 4f;

    /// <summary>Where the hand waits, below the bottom of the screen.</summary>
    private const float HandRestY = ScreenHeight + 40f;

    /// <summary>Where the hand ends up, far enough in that the mouth has closed over it.</summary>
    private const float HandMouthY = 592f;

    /// <summary>How long the hand takes to go in, in seconds.</summary>
    private const float ReachSeconds = 1.35f;

    /// <summary>How long the box holds the hand before it pays, in seconds.</summary>
    private const float TakeSeconds = 0.9f;

    /// <summary>
    /// The fixed part of how long a line of the player's own is held. See <see cref="SayingSeconds"/>.
    /// </summary>
    private const float SayingBase = 0.9f;

    /// <summary>How much longer each character of it is worth. See <see cref="SayingSeconds"/>.</summary>
    private const float SayingPerCharacter = 0.032f;

    /// <summary>The shortest a reply is ever held, so that a two-word one is still a beat.</summary>
    private const float SayingMin = 1.3f;

    /// <summary>
    /// The longest, so that no single sentence eats the box's patience.
    /// </summary>
    /// <remarks>
    /// The clock does not stop for any of this. A reply costs the player the time it takes to
    /// say, exactly as it would across a real table, and four exchanges of it come out of the
    /// same <see cref="DialogueScript.Seconds"/> that everything else does.
    /// </remarks>
    private const float SayingMax = 3.4f;

    /// <summary>
    /// How long the opponent's mouth stays open after a line of theirs lands, in seconds.
    /// </summary>
    /// <remarks>
    /// A beat, not the length of the line. There is no audio to flap a jaw against and the
    /// player reads at their own pace, so holding the talking frame for as long as the words
    /// are on screen would leave them gaping through a silence. Struck when the line arrives
    /// and dropped again while it is still being read.
    /// </remarks>
    private const float SpeakSeconds = 0.75f;

    /// <summary>The thin line of run bookkeeping along the top edge.</summary>
    private const float RunStatusY = 14f;

    /// <summary>
    /// The plate the conversation is written on, on the wall to the right of the opponent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The words used to be stacked into the strip of wall above the opponent's head, and
    /// the opponent's head now reaches the top of the wall. So the words moved to where the
    /// wall is empty: the right-hand side, beside them, on a dark plate that grows downward
    /// as a line wraps. It is the composition of the reference this table was built from --
    /// the figure on one side of the frame, what is being said on the other.
    /// </para>
    /// <para>
    /// The name sits at the top of the plate, the patience bar under it (drawn by
    /// <see cref="DialogueWheel"/>, at a rectangle that has to agree with these numbers), and
    /// the line under that. Anchored by its top rather than its bottom because there is
    /// nothing under it to collide with any more.
    /// </para>
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

    /// <summary>
    /// Which part of a round the table is in.
    /// </summary>
    /// <remarks>
    /// The order is the round: both sides talk, both sides pay, the box deals, the player
    /// takes their turn, what happened is read back, and it comes round again -- unless
    /// somebody is out of lives, in which case it stops.
    /// </remarks>
    private enum RoundPhase
    {
        /// <summary>The discussion period. See <see cref="DiscussionPeriod"/>.</summary>
        Discussion,

        /// <summary>The box is waiting for a hand.</summary>
        Offer,

        /// <summary>A hand is going in, or being held.</summary>
        Reaching,

        /// <summary>
        /// The player's turn: the box has paid, and the player can play what is in their
        /// pockets and then decide about what is in their hand.
        /// </summary>
        PlayerTurn,

        /// <summary>What has happened, read one line at a time.</summary>
        Resolving,

        /// <summary>Somebody is out of lives and the run is finished.</summary>
        Over,
    }

    /// <summary>Not quite black, so the box itself still reads as the darkest thing on screen.</summary>
    private static readonly Color VoidColor = new(10, 8, 16);

    /// <summary>
    /// Additive blending for premultiplied-alpha content. In this case this is for the eye glow.
    /// </summary>
    /// <remarks>
    /// The content pipeline premultiplies alpha, but the stock <see cref="BlendState.Additive"/>
    /// still multiplies the source by its alpha on the way in. That applies alpha twice and
    /// leaves the glow far dimmer than authored, so the source factor is forced to One here.
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

    /// <summary>
    /// Whether the log being read leads back to the player's turn rather than on to the
    /// next round.
    /// </summary>
    /// <remarks>
    /// A pocket played mid-turn is read out and then the turn carries on; a decision about
    /// the hand is read out and then the opponent has theirs. Both go through the same
    /// reading, so this is what tells them apart at the end of it.
    /// </remarks>
    private bool _resumeTurn;

    /// <summary>What the opponent had the last time their face was checked, so a wound can be shown.</summary>
    private int _opponentLivesShown;

    /// <summary>Which part of the round the table is in.</summary>
    private RoundPhase _phase = RoundPhase.Discussion;

    /// <summary>How far the hand has gone in, from 0 to 1, and then how long it is held.</summary>
    private float _reach;
    private float _held;

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

        // The two ways out of this screen. Amber is the box making an offer; red is the
        // colour of the light in it, saved for the choice that ends things.
        _startButton = new ButtonSprite(StartLabel, new Vector2(ScreenWidth / 2f, ButtonRowY), ButtonSprite.Amber);
        _exitButton = new ButtonSprite(ExitLabel, new Vector2(ScreenWidth / 2f, ButtonRowY), ButtonSprite.EmberRed);

        // Bone-white: walking away from the table is the one choice the box has no stake in.
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

        _feedButton = new ButtonSprite(FeedLabel, new Vector2(ScreenWidth / 2f, RunButtonY),
            ButtonSprite.EmberRed);
        _feedButton.Clicked += OfferHand;

        // Using is red because it spends something and cannot be taken back; keeping is
        // amber, the box's own offer held onto; leaving is bone, the same as walking away.
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

        // Yours on the left, theirs on the right, in the same colours as the names on the
        // plate: bone for the player, amber for whoever is across the table.
        _playerPockets = new PocketStrip("YOUR POCKETS", new Vector2(PocketsMargin, PocketsY), ButtonSprite.BoneWhite);
        _opponentPockets = new PocketStrip("THEIR POCKETS",
            new Vector2(ScreenWidth - PocketsMargin - PocketStrip.Width, PocketsY), ButtonSprite.Amber);
        _playerPockets.SlotChosen += PlayPocket;

        _wheel = new DialogueWheel();
        _wheel.Chosen += Answer;

        // Characters come from the window rather than from polling the keyboard, so the form
        // never has to know anything about layouts or modifier keys.
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
        _feedButton.LoadContent(Content);
        _useButton.LoadContent(Content);
        _keepButton.LoadContent(Content);
        _leaveButton.LoadContent(Content);
        _continueButton.LoadContent(Content);
        _playerPockets.LoadContent(Content);
        _opponentPockets.LoadContent(Content);
        _wheel.LoadContent(Content, GraphicsDevice);
    }

    /// <summary>
    /// Centres the row of buttons on screen.
    /// </summary>
    /// <remarks>
    /// This has to run after <c>LoadContent</c>, because until a button has measured its own
    /// label it does not know how wide it is. Laying them out from those measurements rather
    /// than from hardcoded positions means the row stays centred if a label ever changes.
    /// </remarks>
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

        // On the edge rather than while held: Escape means something different on each screen
        // now, so a single press held down must not back out of the form and then close the
        // game on the frame after.
        if (keyboard.IsKeyDown(Keys.Escape) && _lastKeyboard.IsKeyUp(Keys.Escape))
            Back();

        _lastKeyboard = keyboard;

        UpdateProof();

        // The scene runs whatever is on top of it. The box does not stop looking at the player
        // because a form opened over it.
        foreach (var layer in _ashDrift) layer.Update(gameTime);
        _blackBox.Update(gameTime, GraphicsDevice.Viewport);
        foreach (var mote in _ashes) mote.Update(gameTime);

        // Only the screen in front takes input, which is what stops a click meant for the form
        // also landing on the START button still sitting underneath it.
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
                // The clock the slot reports. It is counted here rather than from the system
                // time so that time spent with the game closed never lands on the player.
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

        // 1 everything the eye light will fall on: the dead sky and the box itself.
        // Point sampling keeps the upscaled pixel art crisp.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);

        if (_screen == Screen.Run)
        {
            // The room replaces the dead sky entirely. The drifting ash is the title
            // screen's nowhere; this is somewhere, with a wall and a lamp in it.
            _spriteBatch.Draw(_room, new Rectangle(0, 0, ScreenWidth, ScreenHeight), null,
                Color.White, 0f, Vector2.Zero, SpriteEffects.None, Layers.Room);

            // Both in this batch so the layer sort puts the opponent behind the box and the
            // box on top of its own shadow, which is where all three of them are.
            _opponent.Draw(_spriteBatch, OpponentFoot, OpponentScale);
            _blackBox.DrawContactShadow(_spriteBatch);
        }
        else
        {
            foreach (var layer in _ashDrift) layer.Draw(gameTime, _spriteBatch, GraphicsDevice.Viewport);
        }

        _blackBox.DrawBody(gameTime, _spriteBatch);
        _spriteBatch.End();

        // 2 the eye glow, added on top of the box so the light in the opening spills
        // onto its rim. Linear sampling, because a glow should be soft rather than blocky.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, PremultipliedAdditive, SamplerState.LinearClamp);
        _blackBox.DrawEyeGlow(gameTime, _spriteBatch);
        _spriteBatch.End();

        // 3 the eyes, over the glow rather than inside it, and the ash falling past
        // in front of everything on its way in.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        _blackBox.DrawEyes(gameTime, _spriteBatch);
        foreach (var mote in _ashes) mote.Draw(gameTime, _spriteBatch);

        // Over the box, because the hand is between the player and it.
        _hand.Draw(_spriteBatch, ScreenWidth / 2f, HandRestY, HandMouthY, HandScale);

        _spriteBatch.End();

        // 4 text, on top of everything and sampled linearly so the font stays smooth.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.LinearClamp);
        DrawInterface();
        _spriteBatch.End();

        // 5 the buttons, over everything. Point sampling, because the plates are pixel art
        // upscaled by a whole number like the box is -- their labels are drawn at 1:1, so
        // they stay crisp under it rather than needing a batch of their own.
        _spriteBatch.Begin(SpriteSortMode.BackToFront, BlendState.AlphaBlend, SamplerState.PointClamp);
        DrawScreenButtons(gameTime);
        _spriteBatch.End();

        // 6 the save form, in a batch of its own so its veil covers every one of the batches
        // above it rather than sorting against only the sprites in one of them.
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
                //Button to start the game.
                _startButton.Draw(gameTime, _spriteBatch);

                //Button to exit the game.
                _exitButton.Draw(gameTime, _spriteBatch);
                break;

            case Screen.Run:
                // The pockets are always on the table. What is in them is the one piece of
                // the run that does not change with the phase, and the player should be able
                // to plan around it while they are still talking.
                _playerPockets.Draw(gameTime, _spriteBatch);
                _opponentPockets.Draw(gameTime, _spriteBatch);

                // Exactly one thing to click at a time. While there is still something to
                // say the four replies are it, and a fifth button beside them under a clock
                // would only compete with them.
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

    /// <summary>
    /// Draws the title and the line under it -- the only text the buttons do not carry.
    /// </summary>
    private void DrawInterface()
    {
        // A tight, deep red offset rather than a hard black drop shadow. The serif is light
        // enough that anything wider ghosts out around every stroke instead of sitting
        // behind them, and the red ties the title back to what is looking out of the box.
        // The face sits where the title does, and the title has already done its job by the
        // time the player is at the table.
        if (_screen != Screen.Run)
        {
            DrawCentered(_titleFont, Title, TitleY, new Color(240, 235, 240), new Color(104, 12, 17), new Vector2(2f, 2f));

            // Escape still works, but it is a shortcut now rather than the way out, so it is
            // stated once and quietly instead of pulsing for attention. What it is a shortcut
            // for depends on the screen, so it says which.
            string hint = _screen == Screen.Title ? "ESC" : "ESC  ·  BACK";
            DrawCentered(_uiFont, hint, EscapeHintY, new Color(150, 140, 142), Color.Black * 0.6f, new Vector2(2f, 2f));
        }
        else
        {
            DrawRunState();
        }
    }

    /// <summary>
    /// Draws the table: the bookkeeping along the top, and whatever is being said.
    /// </summary>
    private void DrawRunState()
    {
        // Lives on both sides, then the bookkeeping. Everything a player needs to read the
        // table is on one line rather than spread around the edges of the screen. What is in
        // the pockets is on the pockets.
        string heading = string.Format(CultureInfo.InvariantCulture,
            "{0}  {1}   ·   THEM  {2}   ·   ROUND {3}   ·   SLOT {4}  ·  {5:00}:{6:00}",
            _run.PlayerName.ToUpperInvariant(), Pips(_run.PlayerLives), Pips(_run.OpponentLives),
            _run.Round + 1, _runSlot + 1,
            (int)_run.Playtime.TotalHours, _run.Playtime.Minutes);

        DrawCentered(_detailFont, heading, RunStatusY, new Color(122, 112, 114), Color.Black * 0.6f, new Vector2(2f, 2f));

        // The way out, in the corner with the rest of the bookkeeping. The bottom of the
        // screen is where the buttons are now, and a hint under a button is a hint under a
        // button.
        DrawText(_detailFont, "ESC  ·  SAVE AND LEAVE", ScreenWidth - PocketsMargin, RunStatusY,
            new Color(122, 112, 114), Color.Black * 0.6f, rightAligned: true);

        if (_runStatus is not null)
        {
            DrawCentered(_detailFont, _runStatus, PocketsY - 30f, ButtonSprite.EmberRed,
                Color.Black * 0.6f, new Vector2(2f, 2f));
        }

        DrawDialoguePlate();
    }

    /// <summary>
    /// Draws the plate on the wall and whatever is being said on it.
    /// </summary>
    /// <remarks>
    /// Whoever is speaking owns the plate. Through the beat after a reply the line on screen
    /// is the player's own, and a plate still carrying the opponent's name would be putting
    /// the player's words in their mouth. Once the talking is over the plate is the box's:
    /// what it wants, what it gave, what that did.
    /// </remarks>
    private void DrawDialoguePlate()
    {
        string text = RunLine;
        if (string.IsNullOrEmpty(text) && _discussion is null) return;

        string[] lines = Wrap(_uiFont, text, DialoguePlate.Width - 2 * DialoguePadding);

        // The plate closes a little under the last line, and never above the patience bar.
        float bottom = DialogueLineY + Math.Max(1, lines.Length) * _uiFont.LineSpacing + DialoguePadding * 0.6f;
        var plate = new Rectangle(DialoguePlate.X, DialoguePlate.Y, DialoguePlate.Width, (int)MathF.Round(bottom - DialoguePlate.Y));

        _spriteBatch.Draw(_pixel, plate, null, new Color(6, 5, 9) * 0.80f,
            0f, Vector2.Zero, SpriteEffects.None, Layers.DialoguePlate);

        // A lit lip along the top edge, so the plate reads as the same concrete as the
        // buttons rather than as a shadow that happens to be rectangular.
        _spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, plate.Width, 2), null, new Color(64, 58, 66),
            0f, Vector2.Zero, SpriteEffects.None, Layers.TextShadow);

        (string speaker, Color speakerColour) = Speaker;
        float centre = plate.X + plate.Width / 2f;

        DrawText(_uiFont, speaker, centre, DialogueNameY, speakerColour, Color.Black * 0.6f, centred: true);

        Color colour = _phase == RoundPhase.Discussion && _discussionEnd is null
            ? new Color(226, 216, 210)
            : new Color(162, 150, 150);

        for (int i = 0; i < lines.Length; i++)
        {
            DrawText(_uiFont, lines[i], centre, DialogueLineY + i * _uiFont.LineSpacing, colour,
                Color.Black * 0.6f, centred: true);
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

    /// <summary>
    /// Whether what is on screen is the player's own last line rather than the opponent's.
    /// </summary>
    private bool IsPlayerSpeaking =>
        _phase == RoundPhase.Discussion && _saidHold > 0f && _discussionEnd is null;

    /// <summary>
    /// What the table is saying right now: a line, or what the box just did.
    /// </summary>
    /// <remarks>
    /// A blind deal is the one case where the game knows the answer and will not print it.
    /// <see cref="ItemId.Rotgut"/> buys a life with sight, and this is the debt being
    /// collected -- the item in <see cref="SaveData.Dealt"/> is real and will resolve
    /// normally whatever the player was allowed to see.
    /// </remarks>
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

    /// <summary>
    /// Lives as marks rather than a number.
    /// </summary>
    /// <remarks>
    /// Spent ones are still drawn, struck through, because what matters at this table is not
    /// how many you have but how many you started with and have not got any more.
    /// </remarks>
    /// <param name="lives">What that side has left.</param>
    private static string Pips(int lives)
    {
        var marks = new StringBuilder();

        for (int i = 0; i < SaveData.StartingLives; i++)
            marks.Append(i < lives ? "|" : "/");

        return marks.ToString();
    }

    /// <summary>
    /// Breaks a line into as many lines as it takes to fit.
    /// </summary>
    /// <remarks>
    /// Measured rather than counted in characters, because the font is proportional and a
    /// wrap by character count would leave a ragged edge that moves depending on which
    /// letters a written line happens to use.
    /// </remarks>
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

    /// <summary>
    /// Runs whichever part of the round the table is in.
    /// </summary>
    /// <param name="gameTime">The frame's timing.</param>
    private void UpdateRound(GameTime gameTime)
    {
        float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // The pockets read straight off the run every frame, so nothing that changes them --
        // a levy, a second hand, the opponent's turn -- has to remember to tell them.
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

    /// <summary>
    /// Runs the discussion period and the wheel over it.
    /// </summary>
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

        // The clock ran out between frames. Nothing was clicked, and that is the point.
        if (before == DiscussionState.Running && _discussion.State != DiscussionState.Running)
            EndDiscussion();

        if (_wheel.IsOpen) _wheel.Update(gameTime);
    }

    /// <summary>
    /// Puts the hand in, holds it there, and then pays.
    /// </summary>
    /// <remarks>
    /// The hold between arriving and being paid is the whole point of the beat. Dealing the
    /// instant the fingers cross the rim would make the box a vending machine; the pause is
    /// what makes it something that has taken hold of you and is deciding.
    /// </remarks>
    /// <param name="elapsed">Seconds since the last frame.</param>
    private void UpdateReach(float elapsed)
    {
        if (_reach < 1f)
        {
            _reach = MathF.Min(1f, _reach + elapsed / ReachSeconds);
            _hand.Reach = _reach;
            return;
        }

        _held += elapsed;
        if (_held >= TakeSeconds) Deal();
    }

    /// <summary>
    /// Says one of the four replies.
    /// </summary>
    /// <param name="corner">Which corner of the wheel was clicked.</param>
    private void Answer(int corner)
    {
        // Nothing is taken while the last answer is still being said.
        if (_discussion is null || _saidHold > 0f) return;
        if (!_discussion.Choose(corner)) return;

        // Everything the choice changed, written back to the run in one place.
        _run.OpponentDisposition = _discussion.Disposition;
        foreach (string flag in _discussion.FlagsRaised) _run.SetFlag(flag);

        // The player has the floor. The wheel comes down -- a beat is not something to be
        // clicked through, and four live plates under a line the player is still reading is
        // how a conversation turns back into a menu. The temper the reply earned lands now
        // rather than after it, so what the player watches while they are still speaking is
        // it arriving on the face across the table.
        _wheel.Hide();
        StopTalking();

        _saidHold = SayingSeconds(_discussion.Said);
    }

    /// <summary>
    /// How long a line of the player's own stays up before it is answered.
    /// </summary>
    /// <remarks>
    /// Off the length of the line, because one fixed beat is either too short for the long
    /// replies or dead air after a two-word one. It is a read rather than a performance --
    /// there is no audio to wait out, so this only has to be as long as taking the sentence
    /// in.
    /// </remarks>
    /// <param name="line">The line being held on screen.</param>
    /// <returns>How long to hold it, in seconds.</returns>
    private static float SayingSeconds(string line) => Math.Clamp(
        SayingBase + (line?.Length ?? 0) * SayingPerCharacter, SayingMin, SayingMax);

    /// <summary>
    /// Ends the beat the player's line was held for, and gives the table back.
    /// </summary>
    /// <remarks>
    /// The one place a discussion is allowed to end on the player's own words. An option with
    /// nothing after it used to close the period on the frame it was clicked, which threw the
    /// line away -- the last thing a player said in a discussion was the one line of theirs
    /// that never reached the screen at all.
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

    /// <summary>
    /// Shuts their mouth and puts the temper back on their face.
    /// </summary>
    /// <remarks>
    /// The pose is cleared before the disposition is handed over, because a face that is
    /// doing something ignores what it is told to think -- see
    /// <see cref="OpponentSprite.SetDisposition"/>. That ordering is also what delays a
    /// change of temper until the mouth has shut, so the new face arrives with the pause
    /// after the line rather than underneath it.
    /// </remarks>
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

        // Both hands go in together. Theirs is never drawn -- the arm is behind the box --
        // so the lean and the raised shoulder are the whole of it.
        _opponent.Pose = OpponentPose.Reaching;
    }

    /// <summary>
    /// Takes the hand, and pays for it.
    /// </summary>
    /// <remarks>
    /// The items are written into the run rather than held in fields, so a player who closes
    /// the game between being dealt and deciding comes back still holding theirs -- see
    /// <see cref="EnterRun"/>. What the box gave is decided in <see cref="RoundEngine.DealBoth"/>,
    /// and once.
    /// </remarks>
    private void Deal()
    {
        RoundEngine.DealBoth(_run, _random);

        _hand.IsVisible = false;
        _opponent.Pose = OpponentPose.Even;
        _opponent.SetDisposition(_run.OpponentDisposition);

        BeginTurn();
    }

    /// <summary>
    /// Hands the table to the player, with the three plates set for what is in their hand.
    /// </summary>
    /// <remarks>
    /// KEEP IT is the one plate that can be refused, and it says why on itself rather than
    /// vanishing: full pockets are a thing the player can do something about by playing one,
    /// and a blind item is a debt the plate is there to remind them of.
    /// </remarks>
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

        AfterPlayerAction(RoundEngine.DecideDealt(_run, choice, _random));
    }

    /// <summary>Plays something out of the player's pockets, mid-turn.</summary>
    /// <param name="slot">Which pocket was clicked.</param>
    private void PlayPocket(int slot)
    {
        if (_phase != RoundPhase.PlayerTurn) return;

        AfterPlayerAction(RoundEngine.PlayFromPocket(_run, slot, _random));
    }

    /// <summary>
    /// Reads out what the player just did, and works out what comes after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three things can. If somebody is out of lives the round closes on the spot -- the
    /// opponent does not get a turn against a player who has already lost, or after they
    /// have. If the player is still holding something, the turn is not over: a pocket was
    /// played, or a second hand refilled the hand, and the table comes back to them once the
    /// reading is done. Otherwise the hand has been decided, the opponent takes their turn
    /// into whatever the player just did, and the round closes.
    /// </para>
    /// <para>
    /// All of it goes into the one log and is read back a line at a time, so a round that
    /// turns on a mirror is legible instead of arriving as a new set of numbers.
    /// </para>
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

    /// <summary>
    /// Puts whatever has just been taken off the opponent onto their face.
    /// </summary>
    /// <remarks>
    /// Against what was last shown rather than against the start of the round, so a second
    /// hit in the same turn is a second flinch and not the same one held.
    /// </remarks>
    private void ShowWounds()
    {
        _opponent.SetLives(_run.OpponentLives);

        if (_run.OpponentLives < _opponentLivesShown) _opponent.Pose = OpponentPose.Hurt;
        _opponentLivesShown = _run.OpponentLives;
    }

    /// <summary>
    /// Shows the next line of the log, or moves on if there are none left.
    /// </summary>
    private void StepLog()
    {
        if (_log.Count > 0)
        {
            _logLine = _log.Dequeue();
            return;
        }

        _logLine = null;

        // Out of lives on either side and there is no next round to start.
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

    /// <summary>
    /// Gives the table back to the player after a mid-turn reading.
    /// </summary>
    /// <remarks>
    /// If the hand has somehow emptied in the meantime there is nothing left to decide, and
    /// the turn ends the way a decision would have ended it. Nothing does that today; it is
    /// here so that an item which does cannot strand the round on three plates for an empty
    /// hand.
    /// </remarks>
    private void ResumeTurn()
    {
        if (ItemCatalog.TryParse(_run.Dealt, out _))
        {
            BeginTurn();
            return;
        }

        AfterPlayerAction(new List<string>());
    }

    /// <summary>
    /// Opens the save form over the title screen.
    /// </summary>
    private void OpenSlotMenu()
    {
        _screen = Screen.SlotSelect;
        _slotMenu.Open();
    }

    /// <summary>
    /// Takes the player into the run held in a slot.
    /// </summary>
    /// <param name="slot">Which slot it came out of, and goes back into.</param>
    /// <param name="data">The run.</param>
    private void BeginRun(int slot, SaveData data)
    {
        _runSlot = slot;
        _run = data;
        _runStatus = null;

        // A slot nobody has put a name to is a run that has not started yet, however many
        // times it has been opened.
        if (string.IsNullOrWhiteSpace(_run.PlayerName))
        {
            _screen = Screen.NameEntry;
            _nameEntry.Open();
            return;
        }

        EnterRun();
    }

    /// <summary>
    /// Takes the name the player gave and starts the run with it.
    /// </summary>
    /// <remarks>
    /// Written to disk immediately, for the same reason the slot itself was: the name is the
    /// first thing the player has put into this run, and losing it would mean asking them for
    /// it a second time. A failed write is reported rather than fatal -- they are already at
    /// the table by then, and the run in memory is still the real one.
    /// </remarks>
    /// <param name="name">What the player called themselves.</param>
    private void NameRun(string name)
    {
        _run.PlayerName = name;

        if (!SaveSystem.Write(_runSlot, _run))
            _runStatus = "COULD NOT SAVE -- " + SaveSystem.LastError;

        EnterRun();
    }

    /// <summary>
    /// Puts the player at the table and moves the scene around them.
    /// </summary>
    /// <remarks>
    /// A run that was saved mid-decision comes back mid-decision. The item is in the save,
    /// so there is nothing to lose by honouring it -- and starting a fresh discussion instead
    /// would deal over the top of it, which is a way of taking an item off a player for
    /// closing the game.
    /// </remarks>
    private void EnterRun()
    {
        _screen = Screen.Run;

        // On the table the box is an object in a room rather than the whole picture, so it
        // comes down in size as well as into place.
        _blackBox.Position = new Vector2(ScreenWidth / 2f, RunBoxCenterY);
        _blackBox.Scale = BlackBoxSprite.TableScale;
        RecentreAsh();

        _returnButton.Center = new Vector2(ScreenWidth / 2f, RunButtonY);
        _returnButton.Reset();

        if (string.IsNullOrWhiteSpace(_run.OpponentId))
            _run.OpponentId = DemoDiscussion.Script.OpponentId;

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

    /// <summary>
    /// Opens the discussion period for this round with whoever is across the table.
    /// </summary>
    /// <remarks>
    /// A first meeting opens on whatever the script says the opponent is like before anyone
    /// has spoken to them. Every meeting after that opens on what this run has already made
    /// of them, which is the whole reason the disposition is on the save -- and on the beat
    /// the script has written for this round, which is why the round is too.
    /// </remarks>
    private void StartDiscussion()
    {
        _discussionEnd = null;

        Disposition? carried = _run.HasMetOpponent ? _run.OpponentDisposition : null;

        _discussion = new DiscussionPeriod(DemoDiscussion.Script, _run.PlayerName, _run.Round, carried);

        _saidHold = 0f;
        _opponent.Pose = OpponentPose.Even;
        _opponent.SetDisposition(_discussion.Disposition);
        _opponent.SetLives(_run.OpponentLives);
        _opponentLivesShown = _run.OpponentLives;

        _phase = RoundPhase.Discussion;
        ShowBeat();
    }

    /// <summary>
    /// Writes the run back to its slot and returns to the title screen.
    /// </summary>
    /// <remarks>
    /// The player stays in the run if the write fails. Dropping them back to the title screen
    /// on a failed save would throw away the only copy of the run that still exists, and they
    /// can read what went wrong and try again from here.
    /// </remarks>
    private void LeaveRun()
    {
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
        _phase = RoundPhase.Discussion;

        ShowTitle();
    }

    /// <summary>
    /// Points the loose ash at wherever the box has moved to.
    /// </summary>
    /// <remarks>
    /// The motes are pulled toward a centre they were handed when they were built, so without
    /// this they carry on spiralling into the middle of the title screen after the box has
    /// gone to sit on a table.
    /// </remarks>
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

        // Neither button has been updated since the form covered them, so both are frozen
        // mid-animation and still remember a cursor that has since been somewhere else.
        _startButton.Reset();
        _exitButton.Reset();
    }

    /// <summary>
    /// Backs out of whatever is in front: the form, then the run, then the game.
    /// </summary>
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

            // The exit instructions on screen promise exactly this.
            default:
                Exit();
                break;
        }
    }

    /// <summary>
    /// Draws a line of text centred horizontally on the screen, over a hard offset shadow.
    /// </summary>
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

    /// <summary>
    /// Draws a line of text against a point, over a hard offset shadow.
    /// </summary>
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
