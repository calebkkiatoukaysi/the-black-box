namespace TheBlackBox.Screens;

/// <summary>
/// The customization screen's part of the proof run: a shot when it opens, then with different picks and a name typed in.
/// </summary>
/// <remarks>The changes go through the same calls that left, right and typing use, so the shots actually test the recolour and the name field.</remarks>
public partial class CustomizationScreen
{
    /// <summary>How many frames the screen gets to fade in before the first shot.</summary>
    private const int Settled = 40;

    /// <summary>The last frame of this schedule. SIT DOWN gets pressed on it.</summary>
    internal const int ProofEnd = Settled + 64;

    /// <summary>The name to type. It's as long as the field allows so the heading gets tested at its widest later.</summary>
    private const string ProofName = "Proof Of Concept";

    /// <summary>Sets up whatever this frame needs, and returns a file name if this frame should be saved.</summary>
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
