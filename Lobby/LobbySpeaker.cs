using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Lobby;

/// <summary>
/// Who is talking in the lobby's dialogue box: the name on it, its colour, the picture beside it,
/// and how their voice blips.
/// </summary>
/// <param name="Name">The name on the box.</param>
/// <param name="NameColor">The colour it is set in. Amber for the opponents and red for the box, same as the table's plate.</param>
/// <param name="Portrait">A sheet to cut their picture from, or null for no picture.</param>
/// <param name="PortraitSource">Which part of the sheet.</param>
/// <param name="PortraitScale">How far the picture is blown up. A whole number, so the pixels stay square.</param>
/// <param name="BlipPitch">How high or low their text blips, -1 to 1.</param>
public sealed record LobbySpeaker(
    string Name, Color NameColor, Texture2D Portrait, Rectangle PortraitSource, float PortraitScale, float BlipPitch);
