using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace TheBlackBox.Audio;

/// <summary>
/// Every song and sound effect in the game, and the three volumes they play at.
/// </summary>
/// <remarks>
/// Same calls as the audio tutorial (Content.Load of a Song and a SoundEffect, MediaPlayer with
/// IsRepeating, SoundEffect.Play), just in one place instead of in the Game class. It is a game
/// service, like the achievement service in the services tutorial, so any screen can get it
/// without it being passed around. MediaPlayer only plays one song at a time, so changing track
/// fades the old one out and the new one in rather than cutting.
/// </remarks>
public class AudioManager : GameComponent
{
    /// <summary>How long a song takes to fade out, and the next one to fade in, in seconds.</summary>
    private const float FadeSeconds = 0.6f;

    /// <summary>The folders under Content the sounds are built into.</summary>
    private const string SfxFolder = "Sfx/";
    private const string ItemFolder = "Sfx/Items/";

    private readonly Dictionary<Track, Song> _songs = new();
    private readonly Dictionary<Sfx, SoundEffect> _effects = new();
    private readonly Dictionary<ItemId, SoundEffect> _items = new();

    /// <summary>The song playing (or fading in), and the one waiting for it to fade out.</summary>
    private Track? _current;
    private Track? _next;

    /// <summary>How far up the current song is, 0 silent to 1 full.</summary>
    private float _fade;

    /// <summary>Whether the current song is on its way out.</summary>
    private bool _fadingOut;

    /// <summary>The volumes, saved between sessions. See <see cref="Settings"/>.</summary>
    public Settings Settings { get; }

    /// <summary>When true nothing is heard, but every cue is still written to <see cref="Log"/>.</summary>
    /// <remarks>For the proof run, which should not play music at whoever is at the desk.</remarks>
    public bool Silent { get; set; }

    /// <summary>Every cue asked for while <see cref="Silent"/>, in order, so the proof run can check them.</summary>
    public List<string> Log { get; } = new();

    /// <summary>Creates the manager with the settings it should play at.</summary>
    /// <param name="game">The game it belongs to.</param>
    /// <param name="settings">The volumes.</param>
    public AudioManager(Game game, Settings settings) : base(game)
    {
        Settings = settings;
    }

    /// <summary>Loads every song and every sound effect.</summary>
    /// <remarks>All of them up front. They are small, and a sound loading mid-game would hitch the frame it is first heard on.</remarks>
    /// <param name="content">The content manager to load with.</param>
    public void LoadContent(ContentManager content)
    {
        foreach (Track track in Enum.GetValues<Track>())
            _songs[track] = content.Load<Song>(SongName(track));

        foreach (Sfx sfx in Enum.GetValues<Sfx>())
            _effects[sfx] = content.Load<SoundEffect>(SfxFolder + Kebab(sfx.ToString()));

        foreach (ItemId item in Enum.GetValues<ItemId>())
            _items[item] = content.Load<SoundEffect>(ItemFolder + Kebab(item.ToString()));

        MediaPlayer.IsRepeating = true;
    }

    /// <summary>Starts a song, fading out whatever is playing first. Asking for the one already playing does nothing.</summary>
    /// <param name="track">The song to play.</param>
    public void PlaySong(Track track)
    {
        // Already the song, even if it was on its way out: bring it back up rather than restart it.
        if (_current == track)
        {
            _next = null;
            _fadingOut = false;
            return;
        }

        if (_next == track) return;

        if (Silent) Log.Add("song " + track);

        if (_current is null)
        {
            Start(track);
            return;
        }

        _next = track;
        _fadingOut = true;
    }

    /// <summary>Fades the song out and leaves silence. The verdict at the end of a run uses it, so its stinger is heard alone.</summary>
    public void StopSong()
    {
        if (_current is null) return;

        if (Silent) Log.Add("song stop");

        _next = null;
        _fadingOut = true;
    }

    /// <summary>Plays one sound effect.</summary>
    /// <param name="sfx">Which one.</param>
    /// <param name="volume">How loud, before the settings, 0 to 1.</param>
    /// <param name="pitch">Up or down, -1 to 1 (an octave either way).</param>
    /// <param name="pan">Left or right, -1 to 1.</param>
    public void Play(Sfx sfx, float volume = 1f, float pitch = 0f, float pan = 0f) =>
        Play(sfx.ToString(), _effects.GetValueOrDefault(sfx), volume, pitch, pan);

    /// <summary>Plays an item's own sound, for picking it up or using it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="volume">How loud, before the settings, 0 to 1.</param>
    public void PlayItem(ItemId item, float volume = 1f) =>
        Play(item.ToString(), _items.GetValueOrDefault(item), volume, 0f, 0f);

    /// <summary>Runs the fades.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public override void Update(GameTime gameTime)
    {
        float step = (float)gameTime.ElapsedGameTime.TotalSeconds / FadeSeconds;

        if (_fadingOut)
        {
            _fade = MathF.Max(0f, _fade - step);

            if (_fade <= 0f)
            {
                _fadingOut = false;
                MediaPlayer.Stop();
                _current = null;

                if (_next is Track next)
                {
                    _next = null;
                    Start(next);
                }
            }
        }
        else if (_current is not null)
        {
            _fade = MathF.Min(1f, _fade + step);
        }

        MediaPlayer.Volume = Silent ? 0f : Settings.MasterVolume * Settings.MusicVolume * _fade;

        base.Update(gameTime);
    }

    /// <summary>Starts a song from silence. Update brings it up.</summary>
    /// <param name="track">The song.</param>
    private void Start(Track track)
    {
        _current = track;
        _fade = 0f;
        MediaPlayer.Volume = 0f;
        MediaPlayer.Play(_songs[track]);
    }

    /// <summary>Plays a loaded effect at the settings' volume, or writes it to the log when silent.</summary>
    private void Play(string name, SoundEffect effect, float volume, float pitch, float pan)
    {
        if (Silent)
        {
            Log.Add("sfx " + name);
            return;
        }

        float level = Math.Clamp(volume * Settings.MasterVolume * Settings.SfxVolume, 0f, 1f);
        if (effect is null || level <= 0f) return;

        effect.Play(level, Math.Clamp(pitch, -1f, 1f), Math.Clamp(pan, -1f, 1f));
    }

    /// <summary>The content name of a song. Named for the song, not the screen, so they are spelled out here.</summary>
    /// <param name="track">The song.</param>
    private static string SongName(Track track) => track switch
    {
        Track.Title => "Music/it-is-watching",
        Track.Lobby => "Music/holding",
        _ => "Music/place-your-hand",
    };

    /// <summary>Turns an enum name into the file name it is built from: MenuMove to menu-move.</summary>
    /// <param name="name">The PascalCase name.</param>
    private static string Kebab(string name)
    {
        var kebab = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) kebab.Append('-');
            kebab.Append(char.ToLowerInvariant(name[i]));
        }
        return kebab.ToString();
    }
}
