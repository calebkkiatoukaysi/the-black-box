using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace TheBlackBox.Audio;

/// <summary>
/// Every song and sound effect in the game, and the three volume settings.
/// </summary>
/// <remarks>
/// Same calls as the audio tutorial (loading a Song and SoundEffects, MediaPlayer with IsRepeating,
/// SoundEffect.Play), just all in one place. It's a game service like the achievement service from
/// the services tutorial, so any screen can grab it. MediaPlayer can only play one song at a time,
/// so switching songs fades the old one out and the new one in.
/// </remarks>
public class AudioManager : GameComponent
{
    /// <summary>How long a song takes to fade out or in, in seconds.</summary>
    private const float FadeSeconds = 0.6f;

    /// <summary>The folders under Content where the sounds are.</summary>
    private const string SfxFolder = "Sfx/";
    private const string ItemFolder = "Sfx/Items/";

    private readonly Dictionary<Track, Song> _songs = new();
    private readonly Dictionary<Sfx, SoundEffect> _effects = new();
    private readonly Dictionary<ItemId, SoundEffect> _items = new();

    /// <summary>The song that's playing, and the one waiting for it to finish fading out.</summary>
    private Track? _current;
    private Track? _next;

    /// <summary>How faded in the current song is, 0 is silent and 1 is full.</summary>
    private float _fade;

    /// <summary>Whether the current song is fading out.</summary>
    private bool _fadingOut;

    /// <summary>The volumes. These get saved between sessions.</summary>
    public Settings Settings { get; }

    /// <summary>When true nothing plays, but every sound still gets written to Log.</summary>
    /// <remarks>For the proof run, so it doesn't blast music while it runs.</remarks>
    public bool Silent { get; set; }

    /// <summary>Every sound asked for while Silent is on, in order, so the proof run can check them.</summary>
    public List<string> Log { get; } = new();

    /// <summary>Creates the manager with the volume settings.</summary>
    /// <param name="game">The game it belongs to.</param>
    /// <param name="settings">The volumes.</param>
    public AudioManager(Game game, Settings settings) : base(game)
    {
        Settings = settings;
    }

    /// <summary>Loads every song and sound effect.</summary>
    /// <remarks>All up front. They're small, and loading a sound in the middle of the game would hitch the frame.</remarks>
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

    /// <summary>Starts a song, fading out whatever's playing first. Asking for the song that's already playing does nothing.</summary>
    /// <param name="track">The song to play.</param>
    public void PlaySong(Track track)
    {
        // Already playing this one (even if it was fading out), so just bring it back up instead of restarting it.
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

    /// <summary>Fades the song out to silence. Used for the verdict so the win/lose sound plays on its own.</summary>
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
    /// <param name="pitch">Up or down, -1 to 1 (one octave either way).</param>
    /// <param name="pan">Left or right, -1 to 1.</param>
    public void Play(Sfx sfx, float volume = 1f, float pitch = 0f, float pan = 0f) =>
        Play(sfx.ToString(), _effects.GetValueOrDefault(sfx), volume, pitch, pan);

    /// <summary>Plays an item's sound, for picking it up or using it.</summary>
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

    /// <summary>Starts a song at 0 volume. Update fades it in.</summary>
    /// <param name="track">The song.</param>
    private void Start(Track track)
    {
        _current = track;
        _fade = 0f;
        MediaPlayer.Volume = 0f;
        MediaPlayer.Play(_songs[track]);
    }

    /// <summary>Plays an effect at the settings' volume, or logs it when Silent is on.</summary>
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

    /// <summary>The content name for a song. The files are named after the song, not the screen, so they're spelled out here.</summary>
    /// <param name="track">The song.</param>
    private static string SongName(Track track) => track switch
    {
        Track.Title => "Music/it-is-watching",
        Track.Lobby => "Music/holding",
        _ => "Music/place-your-hand",
    };

    /// <summary>Turns an enum name into its file name, like MenuMove to menu-move.</summary>
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
