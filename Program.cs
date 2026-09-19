using TheBlackBox;

// Two dev switches. --simulate plays the table headless and prints who wins (for tuning
// RoundRules). --proof <dir> renders screenshots into <dir> and quits. Anything else starts the game.
if (args.Length > 0 && args[0] == "--simulate")
{
    RoundSimulator.Run(args.Length > 1 && int.TryParse(args[1], out int runs) ? runs : 5000);
    return;
}

using var game = new BlackBoxGame
{
    ProofDirectory = args.Length > 1 && args[0] == "--proof" ? args[1] : null,
};
game.Run();
