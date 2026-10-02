using System;
using Microsoft.Xna.Framework;
using TheBlackBox.Characters;
using TheBlackBox.Collisions;

namespace TheBlackBox.Lobby;

/// <summary>
/// Someone waiting in the lobby: where they stand, who they are on the dialogue box, and how they
/// pass the time.
/// </summary>
/// <remarks>
/// They do not walk. They stand, look about now and then, and turn to face the player when spoken
/// to. One of them watches the player across the room the whole time, which is the point of her.
/// </remarks>
public class LobbyNpc
{
    /// <summary>How close the player has to be to talk to them, in world pixels.</summary>
    public const float TalkReach = 150f;

    /// <summary>How close the player has to be before somebody who watches turns to follow them.</summary>
    private const float WatchReach = 520f;

    /// <summary>How long somebody looks one way before looking another, give or take.</summary>
    private const float GlanceSeconds = 3.2f;

    /// <summary>The patch of floor they stand on, wide and deep, in world pixels.</summary>
    private static readonly Vector2 FootprintSize = new(42f, 18f);

    /// <summary>Where they look when they are glancing about. Never away; a figure facing the wall reads as a mistake.</summary>
    private static readonly Direction[] Glances = { Direction.Down, Direction.Left, Direction.Down, Direction.Right };

    private readonly Random _random;
    private float _glanceClock;

    /// <summary>Who they are: an opponent's id, or the other conscript's.</summary>
    public string Id { get; }

    /// <summary>The name and picture on the dialogue box, and their voice.</summary>
    public LobbySpeaker Speaker { get; }

    /// <summary>The figure.</summary>
    public WalkerSprite Walker { get; }

    /// <summary>Whether they turn to follow the player around the room.</summary>
    public bool Watches { get; init; }

    /// <summary>The floor they stand on, which the player cannot walk through.</summary>
    public BoundingRectangle Footprint => new(
        Walker.Position.X - FootprintSize.X / 2f, Walker.Position.Y - FootprintSize.Y, FootprintSize.X, FootprintSize.Y);

    /// <summary>Puts someone in the room.</summary>
    /// <param name="id">Who they are.</param>
    /// <param name="speaker">How they appear on the dialogue box.</param>
    /// <param name="walker">Their figure, already given its sheet.</param>
    /// <param name="position">Where they stand, in world pixels.</param>
    public LobbyNpc(string id, LobbySpeaker speaker, WalkerSprite walker, Vector2 position)
    {
        Id = id;
        Speaker = speaker;
        Walker = walker;
        Walker.Position = position;

        // Seeded off the id so the room is the same every time it is entered. Not string.GetHashCode,
        // which .NET changes every run.
        int seed = 17;
        foreach (char c in id) seed = seed * 31 + c;
        _random = new Random(seed);
        _glanceClock = (float)_random.NextDouble() * GlanceSeconds;
    }

    /// <summary>Glances about, or watches the player if this one watches.</summary>
    /// <param name="gameTime">The frame's timing.</param>
    /// <param name="player">Where the player's feet are.</param>
    public void Update(GameTime gameTime, Vector2 player)
    {
        if (Watches && Vector2.Distance(player, Walker.Position) < WatchReach)
        {
            Walker.Face(player);
        }
        else
        {
            _glanceClock -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_glanceClock <= 0f)
            {
                _glanceClock = GlanceSeconds * (0.6f + (float)_random.NextDouble() * 0.8f);
                Walker.Facing = Glances[_random.Next(Glances.Length)];
            }
        }

        Walker.Update(gameTime);
    }

    /// <summary>Whether the player is close enough to talk to them.</summary>
    /// <param name="player">Where the player's feet are.</param>
    public bool InReach(Vector2 player) => Vector2.Distance(player, Walker.Position) < TalkReach;
}
