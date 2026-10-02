using System;
using Microsoft.Xna.Framework;
using TheBlackBox.Characters;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// Someone waiting in the lobby: where they stand, how they show up in the dialogue box, and what they do.
/// </summary>
/// <remarks>
/// They don't walk around. They stand there, look around every few seconds, and face you when you talk to them.
/// </remarks>
public class LobbyNpc
{
    /// <summary>How close the player has to be to talk to them, in world pixels.</summary>
    public const float TalkReach = 150f;

    /// <summary>About how long they look one way before turning.</summary>
    private const float GlanceSeconds = 3.2f;

    /// <summary>The size of their feet box, in world pixels.</summary>
    private static readonly Vector2 FootprintSize = new(42f, 18f);

    /// <summary>Which ways they look. Never up, since someone facing the wall looks like a bug.</summary>
    private static readonly Direction[] Glances = { Direction.Down, Direction.Left, Direction.Down, Direction.Right };

    private readonly Random _random;
    private float _glanceClock;

    /// <summary>Who they are: Serenity's id or the conscript's.</summary>
    public string Id { get; }

    /// <summary>Their name, portrait and voice for the dialogue box.</summary>
    public LobbySpeaker Speaker { get; }

    /// <summary>The figure.</summary>
    public WalkerSprite Walker { get; }

    /// <summary>Their feet box. The player can't walk through it.</summary>
    public BoundingRectangle Footprint => new(
        Walker.Position.X - FootprintSize.X / 2f, Walker.Position.Y - FootprintSize.Y, FootprintSize.X, FootprintSize.Y);

    /// <summary>Puts someone in the room.</summary>
    /// <param name="id">Who they are.</param>
    /// <param name="speaker">How they show up in the dialogue box.</param>
    /// <param name="walker">Their sprite, with its sheet already set.</param>
    /// <param name="position">Where they stand, in world pixels.</param>
    public LobbyNpc(string id, LobbySpeaker speaker, WalkerSprite walker, Vector2 position)
    {
        Id = id;
        Speaker = speaker;
        Walker = walker;
        Walker.Position = position;

        // Seeded from the id so they act the same every time. Can't use string.GetHashCode since
        // .NET changes it every run.
        int seed = 17;
        foreach (char c in id) seed = seed * 31 + c;
        _random = new Random(seed);
        _glanceClock = (float)_random.NextDouble() * GlanceSeconds;
    }

    /// <summary>Looks around every so often.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    public void Update(GameTime gameTime)
    {
        _glanceClock -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_glanceClock <= 0f)
        {
            _glanceClock = GlanceSeconds * (0.6f + (float)_random.NextDouble() * 0.8f);
            Walker.Facing = Glances[_random.Next(Glances.Length)];
        }

        Walker.Update(gameTime);
    }

    /// <summary>Whether the player is close enough to talk to them.</summary>
    /// <param name="player">Where the player's feet are.</param>
    public bool InReach(Vector2 player) => Vector2.Distance(player, Walker.Position) < TalkReach;
}
