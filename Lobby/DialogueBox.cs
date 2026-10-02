using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheBlackBox.Audio;

namespace TheBlackBox.Lobby;

/// <summary>
/// The box along the bottom of the lobby that conversations are typed out in, a letter at a time,
/// with the speaker's picture and name beside it and a blip for their voice.
/// </summary>
/// <remarks>
/// Pressing on while a page is still typing finishes it; pressing on a finished page turns it.
/// The blip is one sound re-pitched per speaker, every other letter, with a little wobble so a
/// long line does not sound like a machine gun.
/// </remarks>
public class DialogueBox
{
    /// <summary>Where the box is on screen.</summary>
    private static readonly Rectangle Box = new(60, 596, 1480, 280);

    /// <summary>The square the picture sits in, inside the box on the left.</summary>
    private static readonly Rectangle PortraitFrame = new(Box.X + 18, Box.Y + 18, 300, Box.Height - 36);

    /// <summary>Where the words go, right of the picture.</summary>
    private const int TextPadding = 32;
    private const float NameY = 616f;
    private const float LinesY = 664f;
    private const float HintRise = 34f;

    /// <summary>How fast a line types out, in letters a second, and how often it blips.</summary>
    private const float LettersPerSecond = 44f;
    private const int BlipEvery = 2;
    private const float BlipVolume = 0.75f;
    private const float BlipWobble = 0.04f;

    private static readonly Color BoxColor = new Color(6, 5, 9) * 0.88f;
    private static readonly Color Lip = new(64, 58, 66);
    private static readonly Color FrameColor = new(14, 12, 18);
    private static readonly Color LineColor = new(226, 216, 210);
    private static readonly Color HintColor = new(122, 112, 114);

    private readonly Random _random = new();

    private AudioManager _audio;
    private SpriteFont _font;
    private SpriteFont _detailFont;
    private Texture2D _pixel;

    private LobbySpeaker _speaker;
    private string[] _pages;
    private int _page;
    private string[] _lines;
    private int _letters;
    private float _typed;
    private Action _finished;

    /// <summary>Whether a conversation is on screen.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Whether the page on screen has finished typing.</summary>
    private bool PageDone => _letters >= TotalLetters;

    private int TotalLetters
    {
        get
        {
            int total = 0;
            foreach (string line in _lines) total += line.Length;
            return total;
        }
    }

    /// <summary>Loads the fonts and the pixel the box is drawn with, and remembers where the blips go.</summary>
    /// <param name="content">The content manager to load with.</param>
    /// <param name="graphicsDevice">The device, for the pixel.</param>
    /// <param name="audio">The sounds.</param>
    public void LoadContent(ContentManager content, GraphicsDevice graphicsDevice, AudioManager audio)
    {
        _audio = audio;
        _font = content.Load<SpriteFont>("spectral-ui");
        _detailFont = content.Load<SpriteFont>("spectral-detail");

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    /// <summary>Starts a conversation.</summary>
    /// <param name="speaker">Who is talking.</param>
    /// <param name="pages">What they say, a page a press.</param>
    /// <param name="finished">What to do once the last page is closed, or null.</param>
    public void Open(LobbySpeaker speaker, string[] pages, Action finished = null)
    {
        _speaker = speaker;
        _pages = pages;
        _finished = finished;
        _page = 0;
        IsOpen = true;
        ShowPage();
    }

    /// <summary>Types the page out, and moves on when the player presses.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="pressed">Whether the player pressed on this frame.</param>
    public void Update(GameTime gameTime, bool pressed)
    {
        if (!IsOpen) return;

        if (pressed)
        {
            if (!PageDone)
            {
                _letters = TotalLetters;
                _typed = _letters;
            }
            else if (_page < _pages.Length - 1)
            {
                _page++;
                ShowPage();
                _audio.Play(Sfx.MenuMove);
            }
            else
            {
                IsOpen = false;
                _audio.Play(Sfx.MenuMove);
                _finished?.Invoke();
            }
            return;
        }

        if (PageDone) return;

        _typed += LettersPerSecond * (float)gameTime.ElapsedGameTime.TotalSeconds;
        int letters = Math.Min(TotalLetters, (int)_typed);

        // Blip on every other letter that is not a space, as it appears.
        for (int i = _letters; i < letters; i++)
        {
            if (i % BlipEvery == 0 && !char.IsWhiteSpace(LetterAt(i)))
            {
                float wobble = ((float)_random.NextDouble() * 2f - 1f) * BlipWobble;
                _audio.Play(Sfx.TextBlip, BlipVolume, _speaker.BlipPitch + wobble);
                break;
            }
        }

        _letters = letters;
    }

    /// <summary>Draws the box, the picture frame and the picture. The point-sampled batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in screen space.</param>
    /// <param name="opacity">How far the screen has faded in, 0 to 1.</param>
    public void DrawFrame(SpriteBatch spriteBatch, float opacity)
    {
        if (!IsOpen) return;

        // Its own batch, so the box, the frame and the picture sort among themselves: box at the back, picture at the front.
        spriteBatch.Draw(_pixel, Box, null, BoxColor * opacity, 0f, Vector2.Zero, SpriteEffects.None, Layers.Veil);
        spriteBatch.Draw(_pixel, new Rectangle(Box.X, Box.Y, Box.Width, 2), null, Lip * opacity,
            0f, Vector2.Zero, SpriteEffects.None, Layers.PanelPlate);

        if (_speaker.Portrait is null) return;

        spriteBatch.Draw(_pixel, PortraitFrame, null, FrameColor * opacity, 0f, Vector2.Zero, SpriteEffects.None, Layers.PanelPlate);

        // The picture stands on the bottom of its frame and is cut off by it, like the opponent is by the table.
        Rectangle source = _speaker.PortraitSource;
        float scale = _speaker.PortraitScale;
        int visible = Math.Min(source.Height, (int)(PortraitFrame.Height / scale));
        source = new Rectangle(source.X, source.Bottom - visible, source.Width, visible);
        var at = new Vector2(
            MathF.Round(PortraitFrame.Center.X - source.Width * scale / 2f),
            MathF.Round(PortraitFrame.Bottom - visible * scale));
        spriteBatch.Draw(_speaker.Portrait, at, source, Color.White * opacity, 0f, Vector2.Zero, scale, SpriteEffects.None, Layers.ButtonAccent);
    }

    /// <summary>Draws the name, the words typed so far, and the hint. The linear-sampled text batch.</summary>
    /// <param name="spriteBatch">The SpriteBatch to render with, in screen space.</param>
    /// <param name="opacity">How far the screen has faded in, 0 to 1.</param>
    public void DrawText(SpriteBatch spriteBatch, float opacity)
    {
        if (!IsOpen) return;

        float left = TextLeft;
        Text.Draw(spriteBatch, _font, _speaker.Name, left, NameY, _speaker.NameColor * opacity, Palette.Shadow * opacity);

        int remaining = _letters;
        for (int i = 0; i < _lines.Length && remaining > 0; i++)
        {
            string line = _lines[i];
            string shown = remaining >= line.Length ? line : line[..remaining];
            remaining -= line.Length;
            Text.Draw(spriteBatch, _font, shown, left, LinesY + i * _font.LineSpacing, LineColor * opacity, Palette.Shadow * opacity);
        }

        if (!PageDone) return;

        string hint = _page < _pages.Length - 1 ? "E  ·  NEXT" : "E  ·  CLOSE";
        Text.Draw(spriteBatch, _detailFont, hint, Box.Right - TextPadding, Box.Bottom - HintRise,
            HintColor * opacity, Palette.Shadow * opacity, rightAligned: true);
    }

    /// <summary>Where the words start: right of the picture, or at the box's edge if there is none.</summary>
    private float TextLeft => (_speaker.Portrait is null ? Box.X : PortraitFrame.Right) + TextPadding;

    /// <summary>Wraps the page for the room the box has and starts typing it.</summary>
    private void ShowPage()
    {
        float width = Box.Right - TextPadding - TextLeft;
        _lines = Text.Wrap(_font, _pages[_page], width);
        _letters = 0;
        _typed = 0f;
    }

    /// <summary>The letter at a position, counting through the wrapped lines.</summary>
    private char LetterAt(int index)
    {
        foreach (string line in _lines)
        {
            if (index < line.Length) return line[index];
            index -= line.Length;
        }
        return ' ';
    }
}
