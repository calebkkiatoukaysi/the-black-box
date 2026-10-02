using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheBlackBox.Lobby;

/// <summary>
/// Who's talking in the dialogue box: their name, its colour, their portrait, and their blip pitch.
/// </summary>
/// <param name="Name">The name on the box.</param>
/// <param name="NameColor">The colour of the name. Amber for opponents and red for the box, same as at the table.</param>
/// <param name="Portrait">The sheet their portrait comes from, or null for none.</param>
/// <param name="PortraitSource">Which part of the sheet.</param>
/// <param name="PortraitScale">How much the portrait is scaled up. Keep it a whole number so the pixels stay square.</param>
/// <param name="BlipPitch">How high or low their text blips, -1 to 1.</param>
public sealed record LobbySpeaker(
    string Name, Color NameColor, Texture2D Portrait, Rectangle PortraitSource, float PortraitScale, float BlipPitch);
