using System;
using System.Collections.Generic;

namespace TheBlackBox;

/// <summary>
/// What happens when an item is used, and what to say about it.
/// </summary>
/// <remarks>
/// The one place that knows what an item does. ItemCatalog only holds names and weights.
/// Everything returns lines instead of printing them, because the round is read back one
/// click at a time.
/// </remarks>
public static class ItemResolver
{
    /// <summary>Uses one item, applies everything it does, and says what happened.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="item">What is being used.</param>
    /// <param name="byPlayer">True if the player used it, false if the opponent did.</param>
    /// <param name="random">The coin for anything the item flips.</param>
    /// <param name="efficiency">How well the skill check went, 0 to 1. The opponent and simulator pass 1, which is the item working as normal.</param>
    /// <returns>One line per thing that happened, in order.</returns>
    public static List<string> Use(SaveData run, ItemId item, bool byPlayer, Random random, float efficiency = 1f)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(random);

        var log = new List<string>();
        string who = byPlayer ? run.PlayerName.ToUpperInvariant() : "THEY";

        log.Add(who + " USED " + ItemCatalog.NameOf(item).ToUpperInvariant() + ".");
        Resolve(run, item, byPlayer, random, log, Math.Clamp(efficiency, 0f, 1f));

        // A wild card copies the last thing used, so it must not record itself or two in a row loop forever.
        if (item != ItemId.WildCard) run.LastUsed = ItemCatalog.ToSaveId(item);

        return log;
    }

    /// <summary>Applies one item. Split out so <see cref="ItemId.WildCard"/> can re-enter it.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="item">What is being used.</param>
    /// <param name="byPlayer">True if the player used it.</param>
    /// <param name="random">The coin.</param>
    /// <param name="log">Where to write what happened.</param>
    /// <param name="efficiency">How well the check went. See <see cref="Use"/>.</param>
    private static void Resolve(SaveData run, ItemId item, bool byPlayer, Random random, List<string> log, float efficiency)
    {
        switch (item)
        {
            case ItemId.Cinder:
            case ItemId.SpentShell:
                log.Add("NOTHING HAPPENS. IT WAS NEVER GOING TO.");
                break;

            // Aimed. A clean shot lands, a bad one is a weighted coin, a miss is a miss.
            case ItemId.Revolver:
                if (Lands(random, efficiency)) Damage(run, !byPlayer, log);
                else log.Add("THE SHOT GOES WIDE. IT COSTS THEM NOTHING.");
                break;

            case ItemId.Pact:
                log.Add("IT TAKES FROM BOTH SIDES OF THE TABLE.");
                Damage(run, byPlayer, log);
                if (Lands(random, efficiency)) Damage(run, !byPlayer, log);
                else log.Add("THE HALF THAT WAS THEIRS GOES WIDE. YOURS DID NOT.");
                break;

            // Fair coin at full efficiency, the player pays every time at zero.
            case ItemId.Wager:
            case ItemId.HighCard:
            {
                bool playerPays = random.NextDouble() < 1.0 - 0.5 * efficiency;
                log.Add(item == ItemId.HighCard
                    ? "YOU BOTH CUT THE DECK."
                    : "THE BOX DECIDES WHICH OF YOU PAYS.");
                Damage(run, playerPays, log);
                break;
            }

            // Held. A guard held steadily settles; one that was let slip may not.
            case ItemId.Tourniquet:
                if (Lands(random, efficiency)) Heal(run, byPlayer, log);
                else log.Add("IT DOES NOT HOLD. THE BLOOD KEEPS COMING.");
                break;

            case ItemId.Rotgut:
                Heal(run, byPlayer, log);
                SetBlind(run, byPlayer);
                log.Add(byPlayer
                    ? "THE NEXT THING THE BOX GIVES YOU, YOU WILL NOT SEE."
                    : "THEY WILL TAKE THE NEXT ONE BLIND.");
                break;

            case ItemId.LastCall:
                SetBlind(run, true);
                SetBlind(run, false);
                log.Add("NEITHER OF YOU WILL SEE WHAT COMES NEXT.");
                break;

            case ItemId.AshVeil:
            case ItemId.Mirror:
                if (!Lands(random, efficiency))
                {
                    log.Add("YOUR HANDS SHAKE. IT DOES NOT SETTLE, AND IT IS SPENT.");
                    break;
                }
                SetWard(run, byPlayer, item);
                log.Add(item == ItemId.AshVeil
                    ? "THE NEXT THING USED AGAINST THEM WILL NOT LAND.".Replace("THEM", byPlayer ? "YOU" : "THEM")
                    : "THE NEXT THING USED AGAINST THEM WILL HAPPEN TO ITS SENDER.".Replace("THEM", byPlayer ? "YOU" : "THEM"));
                break;

            // Read. A reading is right or it is nothing.
            case ItemId.Lens:
                if (byPlayer && efficiency < 0.5f)
                {
                    log.Add("YOU LOOK AT THE WRONG THING. THEY ARE HOLDING SOMETHING, AND YOU DO NOT KNOW WHAT.");
                }
                else if (byPlayer)
                {
                    run.SeesOpponentItem = true;
                    log.Add(ItemCatalog.TryParse(run.OpponentDealt, out ItemId theirs)
                        ? "THEY ARE HOLDING " + ItemCatalog.NameOf(theirs).ToUpperInvariant() + "."
                        : "THEY ARE HOLDING NOTHING.");
                }
                else
                {
                    log.Add("THEY LOOK AT WHAT YOU ARE HOLDING. THEY DO NOT REACT.");
                }
                break;

            // How many pockets are full is already visible, so a tally has to say what is in them.
            case ItemId.Tally:
                if (byPlayer)
                {
                    run.SeesOpponentPockets = true;
                    log.Add(run.OpponentBanked.Count == 0
                        ? "THEIR POCKETS ARE EMPTY."
                        : "THEY ARE CARRYING " + Names(run.OpponentBanked) + ".");
                }
                else
                {
                    log.Add("THEY LOOK AT WHAT YOU ARE CARRYING, AND SAY NOTHING.");
                }
                break;

            case ItemId.MarkedDeck:
            {
                // Rolled now and stored, so what the deck shows is what actually gets dealt.
                ItemId next = ItemCatalog.Deal(random);
                run.NextDeal = ItemCatalog.ToSaveId(next);
                log.Add(byPlayer && efficiency < 0.5f
                    ? "YOU LOSE YOUR PLACE IN THE DECK. THE BOX DEALS WHAT IT DEALS."
                    : byPlayer
                        ? "THE BOX DEALS " + ItemCatalog.NameOf(next).ToUpperInvariant() + " NEXT. IT DOES NOT SAY TO WHOM."
                        : "THEY READ THE DECK, AND SAY NOTHING.");
                break;
            }

            case ItemId.Confession:
                log.Add(byPlayer
                    ? TruthFrom(run.OpponentDisposition)
                    : "THEY ASK YOU SOMETHING, AND WAIT.");
                break;

            // Hand first, then a pocket if the hand is empty. The opponent plays second, when
            // the player's hand is always empty, so a hand-only levy would be a blank for them.
            case ItemId.Levy:
            {
                string burned = byPlayer ? run.OpponentDealt : run.Dealt;
                List<string> pockets = byPlayer ? run.OpponentBanked : run.Banked;

                if (ItemCatalog.TryParse(burned, out ItemId lost))
                {
                    if (byPlayer) { run.OpponentDealt = string.Empty; run.OpponentDealtBlind = false; }
                    else { run.Dealt = string.Empty; run.DealtBlind = false; }

                    log.Add(byPlayer
                        ? "THEIR " + ItemCatalog.NameOf(lost).ToUpperInvariant() + " BURNS IN THEIR HAND."
                        : "YOUR " + ItemCatalog.NameOf(lost).ToUpperInvariant() + " BURNS IN YOUR HAND.");
                }
                else if (pockets.Count > 0)
                {
                    int slot = random.Next(pockets.Count);
                    ItemCatalog.TryParse(pockets[slot], out ItemId pocketed);
                    pockets.RemoveAt(slot);

                    log.Add(byPlayer
                        ? "THE " + ItemCatalog.NameOf(pocketed).ToUpperInvariant() + " IN THEIR POCKET BURNS."
                        : "THE " + ItemCatalog.NameOf(pocketed).ToUpperInvariant() + " IN YOUR POCKET BURNS.");
                }
                else
                {
                    log.Add(byPlayer
                        ? "THEY HAVE NOTHING ON THEM TO BURN."
                        : "THEY FIND NOTHING ON YOU TO BURN.");
                }
                break;
            }

            case ItemId.SecondHand:
            {
                ItemId again = TakeNextDeal(run, random, byPlayer ? RoundRules.PlayerFavour : 0f);
                run.HandsFed++;

                log.Add(byPlayer
                    ? "THE BOX TAKES AGAIN, AND GIVES YOU " + ItemCatalog.NameOf(again).ToUpperInvariant() + "."
                    : "THE BOX TAKES FROM THEM AGAIN, AND PAYS THEM AGAIN.");

                Receive(run, again, byPlayer, log);
                break;
            }

            case ItemId.WildCard:
                // Never copies another wild card. Can't happen in play, but a hand-edited save
                // could do it and that would recurse forever.
                if (ItemCatalog.TryParse(run.LastUsed, out ItemId copied) && copied != ItemId.WildCard)
                {
                    log.Add("IT BECOMES " + ItemCatalog.NameOf(copied).ToUpperInvariant() + ".");
                    Resolve(run, copied, byPlayer, random, log, efficiency);
                }
                else
                {
                    log.Add("NOTHING HAS BEEN USED AT THIS TABLE YET. IT IS A BLANK.");
                }
                break;

            default:
                log.Add("NOTHING HAPPENS.");
                break;
        }
    }

    /// <summary>Whether a thing done this well comes off.</summary>
    /// <remarks>A coin weighted by efficiency, but 1 always lands and 0 never does, so a clear miss cannot get lucky.</remarks>
    /// <param name="random">The coin.</param>
    /// <param name="efficiency">How well it was done, 0 to 1.</param>
    private static bool Lands(Random random, float efficiency)
    {
        if (efficiency >= 1f) return true;
        if (efficiency <= 0f) return false;
        return random.NextDouble() < efficiency;
    }

    /// <summary>Takes a life off one side, unless a guard is in the way.</summary>
    /// <remarks>A guard is spent whether or not it was worth spending. That is what makes holding one a guess.</remarks>
    /// <param name="run">The run to change.</param>
    /// <param name="onPlayer">True to take it off the player, false off the opponent.</param>
    /// <param name="log">Where to write what happened.</param>
    private static void Damage(SaveData run, bool onPlayer, List<string> log)
    {
        string ward = onPlayer ? run.PlayerWard : run.OpponentWard;

        if (ItemCatalog.TryParse(ward, out ItemId guard))
        {
            ClearWard(run, onPlayer);

            if (guard == ItemId.AshVeil)
            {
                log.Add((onPlayer ? "THE VEIL TAKES IT FOR YOU." : "THE VEIL TAKES IT FOR THEM.")
                    + " NOTHING LANDS.");
                return;
            }

            if (guard == ItemId.Mirror)
            {
                log.Add("THE MIRROR SENDS IT BACK.");
                Damage(run, !onPlayer, log);
                return;
            }
        }

        if (onPlayer)
        {
            run.PlayerLives--;
            log.Add("IT COSTS YOU A LIFE. YOU HAVE " + Math.Max(0, run.PlayerLives) + " LEFT.");
        }
        else
        {
            run.OpponentLives--;
            log.Add("IT COSTS THEM A LIFE. THEY HAVE " + Math.Max(0, run.OpponentLives) + " LEFT.");
        }
    }

    /// <summary>Gives a life back, never above what the run started with.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="toPlayer">True to heal the player.</param>
    /// <param name="log">Where to write what happened.</param>
    private static void Heal(SaveData run, bool toPlayer, List<string> log)
    {
        int lives = toPlayer ? run.PlayerLives : run.OpponentLives;

        if (lives >= SaveData.StartingLives)
        {
            log.Add(toPlayer ? "THERE IS NOTHING OF YOURS TO GIVE BACK." : "THEY HAD NOTHING TO GET BACK.");
            return;
        }

        if (toPlayer)
        {
            run.PlayerLives = lives + 1;
            log.Add("IT GIVES YOU A LIFE BACK. YOU HAVE " + run.PlayerLives + ".");
        }
        else
        {
            run.OpponentLives = lives + 1;
            log.Add("IT GIVES THEM A LIFE BACK. THEY HAVE " + run.OpponentLives + ".");
        }
    }

    /// <summary>Puts a guard up in front of one side, replacing whatever was there.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="forPlayer">True to guard the player.</param>
    /// <param name="ward">Which guard it is.</param>
    private static void SetWard(SaveData run, bool forPlayer, ItemId ward)
    {
        if (forPlayer) run.PlayerWard = ItemCatalog.ToSaveId(ward);
        else run.OpponentWard = ItemCatalog.ToSaveId(ward);
    }

    /// <summary>Takes a spent guard back down.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="forPlayer">True to clear the player's.</param>
    private static void ClearWard(SaveData run, bool forPlayer)
    {
        if (forPlayer) run.PlayerWard = string.Empty;
        else run.OpponentWard = string.Empty;
    }

    /// <summary>Marks one side as taking the next deal without being allowed to look.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="forPlayer">True for the player.</param>
    private static void SetBlind(SaveData run, bool forPlayer)
    {
        if (forPlayer) run.NextDealBlind = true;
        else run.OpponentNextDealBlind = true;
    }

    /// <summary>Puts an item into a hand, and collects any blindness owed on it.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="item">What was dealt.</param>
    /// <param name="toPlayer">True if it was dealt to the player.</param>
    public static void PutInHand(SaveData run, ItemId item, bool toPlayer)
    {
        string id = ItemCatalog.ToSaveId(item);

        if (toPlayer)
        {
            run.Dealt = id;
            run.DealtBlind = run.NextDealBlind;
            run.NextDealBlind = false;
        }
        else
        {
            run.OpponentDealt = id;
            run.OpponentDealtBlind = run.OpponentNextDealBlind;
            run.OpponentNextDealBlind = false;
        }
    }

    /// <summary>Puts a second deal wherever there is room: the hand, then a pocket, else it burns.</summary>
    /// <param name="run">The run to change.</param>
    /// <param name="item">What the box just dealt.</param>
    /// <param name="toPlayer">True if it was dealt to the player.</param>
    /// <param name="log">Where to write what happened.</param>
    private static void Receive(SaveData run, ItemId item, bool toPlayer, List<string> log)
    {
        string id = ItemCatalog.ToSaveId(item);
        string held = toPlayer ? run.Dealt : run.OpponentDealt;
        List<string> pockets = toPlayer ? run.Banked : run.OpponentBanked;

        if (!ItemCatalog.TryParse(held, out _))
        {
            PutInHand(run, item, toPlayer);
            return;
        }

        if (pockets.Count < RoundRules.PocketSlots)
        {
            pockets.Add(id);
            log.Add(toPlayer ? "IT GOES STRAIGHT INTO YOUR POCKET." : "IT GOES STRAIGHT INTO THEIRS.");
            return;
        }

        log.Add(toPlayer ? "YOUR HANDS AND POCKETS ARE FULL. IT BURNS." : "THEY HAVE NOWHERE TO PUT IT. IT BURNS.");
    }

    /// <summary>Takes whatever the box has already decided to deal, or rolls a fresh one.</summary>
    /// <param name="run">The run holding a promised deal, if there is one.</param>
    /// <param name="random">The draw when nothing was promised.</param>
    /// <param name="favour">How kind the box is to this side. See <see cref="ItemCatalog.Deal"/>.</param>
    /// <returns>The item the box parts with.</returns>
    public static ItemId TakeNextDeal(SaveData run, Random random, float favour = 0f)
    {
        // A promised deal is dealt as promised, favour or not.
        if (ItemCatalog.TryParse(run.NextDeal, out ItemId promised))
        {
            run.NextDeal = string.Empty;
            return promised;
        }

        return ItemCatalog.Deal(random, favour);
    }

    /// <summary>The names of everything in a pocket, as one readable list.</summary>
    /// <param name="ids">The item ids being carried.</param>
    private static string Names(List<string> ids)
    {
        var names = new List<string>();
        foreach (string id in ids)
        {
            if (ItemCatalog.TryParse(id, out ItemId item)) names.Add(ItemCatalog.NameOf(item).ToUpperInvariant());
        }

        if (names.Count <= 1) return string.Concat(names);
        return string.Join(", ", names.GetRange(0, names.Count - 1)) + " AND " + names[^1];
    }

    /// <summary>What a bound opponent admits to, which is how they actually feel.</summary>
    /// <param name="disposition">What they think of the player.</param>
    private static string TruthFrom(Disposition disposition) => disposition.Band switch
    {
        Warmth.Hostile => "THEY SAY: \"I WANT YOU TO LOSE. I HAVE WANTED IT SINCE YOU SAT DOWN.\"",
        Warmth.Open => "THEY SAY: \"I DO NOT WANT TO BE THE ONE WHO DOES IT TO YOU.\"",
        _ => "THEY SAY: \"I HAVE NOT DECIDED ABOUT YOU YET.\"",
    };
}
