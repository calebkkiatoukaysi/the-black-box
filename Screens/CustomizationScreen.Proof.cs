namespace TheBlackBox.Screens;

/// <summary>
/// The customization screen's part of the proof run: as it opens, then dressed differently with a name typed in.
/// </summary>
/// <remarks>Changes go through the same calls left, right and typing make, so the shots prove the recolour and the name field.</remarks>
public partial class CustomizationScreen
{
    /// <summary>How many frames the screen is given to come up out of the fade.</summary>
    private const int Settled = 40;

    /// <summary>The last frame of the screen's schedule. SIT DOWN is pressed on it.</summary>
    internal const int ProofEnd = Settled + 64;

    /// <summary>The name typed in, as long as the field allows, so the heading is proved at its widest later on.</summary>
    private const string ProofName = "Proof Of Concept";

    /// <summary>Sets up whatever this frame calls for, and names the file if the frame is to be kept.</summary>
    /// <param name="frame">Which drawn frame this is, counted from the screen's first.</param>
    /// <returns>The file the frame should be written to, or null.</returns>
    internal string ProofStep(int frame)
    {
        switch (frame)
        {
            case Settled:
                return "customize.png";

            // The other conscript, in other colours, with a scarf, and named.
            case Settled + 2:
                _conscript.OnAdjust(1);
                _hair.OnAdjust(1);
                _hair.OnAdjust(1);
                _outfit.OnAdjust(-1);
                _accent.OnAdjust(1);
                _wearing.OnAdjust(1);
                SelectedEntry = MenuEntries.IndexOf(_nameRow);
                return null;

            case Settled + 4:
                foreach (char c in ProofName) HandleTextInput(c);
                return null;

            case Settled + 30:
                return "customize-changed.png";

            // The first conscript again, in a cap.
            case Settled + 32:
                _conscript.OnAdjust(1);
                _wearing.OnAdjust(1);
                return null;

            case Settled + 50:
                return "customize-cap.png";

            case Settled + 64:
                Begin();
                return null;

            default:
                return null;
        }
    }
}
