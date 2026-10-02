# The Black Box

A MonoGame project for CIS 580 - Foundations of Game Programming, Kansas State University. (For now ;))

The Black Box is a game set in a dystopian world. The premise is simple, the main character is locked into a deadly game with other characters. Powered by the black box, the MC can get a random item assisting them in defeating their opponent. The MC can talk to their opponent, and after each dialog, the two players (MC and a character) will have the box consume their hand, the box will give them an item, and it will be each character's decision to take action or not. There are future plans for this game including characters, antagonists, different endings based on your decisions.



## Running it

The window is 1600x900 (for now!). Everything on screen is sized off that, and the box art is upscaled
by a whole number (4x), so changing the resolution means changing `ScreenWidth`/`ScreenHeight`
and `BlackBoxSprite.DrawScale` together to keep the pixel art crisp.

```
dotnet run
```

**START GAME** opens the save form. A slot that has never been named goes to the
customization screen first, where you pick your character, their look and their name; a slot that
has been picks up where it was -- in the lobby between tables, or back at the table, mid-decision
if you left with something in your hand. **OPTIONS** has the three volumes. **EXIT** and **Esc**
close the game. Past the title screen, Esc is "back" on a form and the pause menu everywhere else.
The eyes follow your mouse cursor; leave it alone for a couple of seconds and the box goes back
to looking around the room on its own.

### Controls

| Where | Keys |
| --- | --- |
| Menus and forms | **Arrows** or **WASD** to move, **Enter** or **Space** to pick, **Esc** to go back. The mouse works too |
| Options | **Left** and **right** turn the selected volume down or up |
| Customization | **Up** and **down** to move between rows, **left** and **right** to change a row, type on the name row (only the arrows leave it), **Enter** on SIT DOWN |
| The lobby | **WASD** or the **arrows** to walk, **E**, **Space** or **Enter** to talk, to turn a page of dialogue, and to try the door, **Esc** to pause |
| The table | The mouse, as before; the checks also take the keyboard and a pad. **Esc** to pause |

A gamepad works anywhere the keyboard does: the d-pad or left stick, A, B, and Start to pause.

Two switches exist for working on the game, and neither is the game:

```
dotnet run -- --simulate 5000     # play the table headless and print who wins
dotnet run -- --proof shots       # play every screen, save a picture of each to shots/, and quit
```

The first is how the numbers in `RoundRules` were chosen (see [A round](#a-round)). The second
is how the layout is checked after a change, and how the pictures for a release get taken. It
goes the whole way round the way a player would -- title, options, save form, customization,
the lobby (walking, talking to all three, picking something up, the door), the table, both
verdicts, back to the lobby, and out through the pause menu -- on a scratch run that never
touches a save slot. It is silent: every sound it would have played is written to
`shots/audio.log` with the frame it was asked for on, which is how the sound cues are checked.

## Screens

Every screen is a `GameScreen` on a `ScreenManager`, the way the game state management tutorial
does it (`StateManagement/` is that tutorial's code, adapted). Each has its own
`TransitionOnTime` and `TransitionOffTime`, so everything fades instead of cutting.

| Screen | What it is |
| --- | --- |
| `BoxBackgroundScreen` | The box and the ash behind the title and every form. It doesn't transition off when something covers it, so it stays up behind all of them |
| `TitleScreen` | The title, with START GAME, OPTIONS and EXIT |
| `OptionsScreen` | Master, music and sound volume, on a form over the title. Saved to `settings.json` |
| `SaveSlotScreen` | The save form, over the title |
| `CustomizationScreen` | Who you are, your colours, what you wear, and your name, with a preview walking in place |
| `LobbyScreen` | The room you wait in between tables |
| `TableScreen` | The table: the Black Box arena. Everything under [The table](#the-table) |
| `PauseMenuScreen` | RESUME or RETURN TO TITLE, over the lobby or the table. Nothing under it moves while it is up |
| `ResultsScreen` | The verdict, how the run went, and RETURN TO THE LOBBY or RETURN TO TITLE |
| `LoadingScreen` | From the tutorial. Waits for everything to fade out, then adds the next screens. Going through the door shows a line of text on the black screen in between |

A run goes title -> save form -> customization (new slots only) -> lobby -> through the door ->
the table -> results -> back to the lobby (next chapter if you won) or the title.

## Customization

You are one of two conscripts: the one in the jacket, or the one in the long coat. Each has
three colour channels -- hair, outfit, and an accent that colours the shirt, the belt, the
collar and whatever is worn on top -- with six presets each, and three things to wear on top
(nothing, a scarf, a cap). The preview turns through all four directions while it walks, so you
can see a change from every side.

The recolouring is a palette swap. `tools/generate_characters.py` paints the two player sheets in
key colours (the first preset of each channel) and writes every preset into
`character-palettes.png`, one row each. `CharacterPalettes.Recolour` reads the keys out of that
PNG, swaps each one for the same shade of the chosen ramp with `GetData`/`SetData`, and hands
back a new texture. It runs once per change, not per frame. A shader would have been the other
way to do it, but nothing in the course has used one yet, and pre-baking every combination would
have been 2 x 6 x 6 x 6 x 3 sheets.

The choice is kept on the save (`SaveData.Look`), and the lobby recolours the player from it.

## The lobby

The lobby is the room you wait in between tables, seen from slightly above. It is
2304x1440 on screen, bigger than the window both ways, and a `Camera2D` follows the player: it
eases after them, stops at the walls so it never shows past the edge of the room, and rounds its
translation to whole pixels. Its `Transform` is what `SpriteBatch.Begin` gets, the same way the
parallax tutorial scrolls.

- **The room** is an ASCII map in `LobbyMap`, tiles off `lobby-tiles.png`, with the furniture
  (lockers, cots, a bench, columns, crates, a sink, two cameras) placed by hand in the same file.
  The light is not painted in: `LobbyMap` builds a texture of the dark with the lamps' pools cut
  out of it and lays it over the whole room, so somebody walking out of the light goes dark too.
- **Walking** is WASD or the arrows, with four-direction walk cycles. The feet are a
  `BoundingRectangle` (the collision tutorial's), moved one axis at a time and pushed back out of
  walls, furniture and people, so a wall stops you one way and lets you slide along it the other.
  Everything on the floor is sorted by how far down the room it is.
- **People.** Serenity and whichever conscript you did not pick are waiting in here. Walk up to
  one and press E: the dialogue box types their lines out a letter at a time, with a blip pitched
  for their voice, their picture (Serenity's is her table picture; the conscript gets a close crop
  of their walking sheet) and their name. Everything they say is in `LobbyLines`.
- **The challenge.** Your opponent for the chapter has to challenge you first -- for now that
  is always Serenity, the only one on the roster. Until
  they do, the red door at the far end does not open, and the box tells you so. Once they have, the
  lamp over the door comes on, the floor in front of it is painted, and walking into it -- or
  pressing E at it -- goes through. The door shuts behind you and the table comes up.
- **Things on the floor.** A few items are lying around each chapter. Walk over one and it
  goes in your pockets, with its sound, and it comes with you to the table. Four of them and
  three pockets, so not everything can be taken; none of them takes a life. What has been
  picked up is kept on the save (`SaveData.LobbyTaken`) until you restart the table.

## Music and sound

None of it was recorded or downloaded. `tools/generate_audio.py` builds every song and sound
from sines, wavetables, noise and a few filters, in CPython (3.12 or newer) and nothing else:

```
python tools/generate_audio.py                 # everything (about two minutes)
python tools/generate_audio.py music           # the three songs
python tools/generate_audio.py sfx             # every sound effect
python tools/generate_audio.py holding revolver
```

Three songs, all in D minor with its flat second, so they sound like they go together: slow,
sparse and dark, with a lot of empty space. I went for the kind of unsettling, minimal scoring
in Chainsaw Man and horror films like Obsession -- detuned piano, sub-bass, bowed drones,
dissonant clusters -- but every note is original and nothing is taken from either. Each one is an exact number of bars long and wraps its own echoes back round to the start, so it loops under
`MediaPlayer.IsRepeating` without a seam.

| Song | Where | What it is |
| --- | --- | --- |
| *It Is Watching* | Title, options, customization | 52 BPM. A broken music box that never resolves, over a bowed bass, slow string swells rubbing a semitone against the root, a faint choir, a far-off heartbeat that skips, and two deep impacts |
| *Holding* | The lobby | 58 BPM. A felt piano, slightly detuned, rolling slow minor chords, one high note knocking on the same beats every bar, strings under it, a soft pulse, reversed chords pulling into the next section, and the hum of a fluorescent tube |
| *Place Your Hand* | The table | 92 BPM in half time. A distorted sub kick, steel clanging on the backbeat, the clock ticking, low strings sawing a tritone, a choir cluster, a breakdown where a detuned piano plays over a held tritone, and a stuttering roll back into the top |

Sound effects are `SoundEffect`s, balanced against each other in the script so the game plays
them all at one volume. Every item has its own, heard when it is picked up in the lobby and
when it is used at the table (a revolver's cylinder and shot, a mirror's shimmer, a tally of
five chalk strokes, a rotgut's glugs...). Beyond those: menu move, confirm and back, a value
being changed, a key typed, the pause menu; footsteps, tied to the frames a foot lands on so
they cannot come faster than the feet; the dialogue blip; the door rattling shut, unlocking,
and slamming behind you; and at the table, the box's jaws, the hand going in, the tag being
thrown out, caught or dropped, a check done well or badly, a hit, a wound, a life given back,
a guard stopping something, and the two verdicts.

`Audio/AudioManager` is where all of it lives: the same `Content.Load<Song>`, `MediaPlayer` and
`SoundEffect.Play` calls as the audio tutorial, in one game service any screen can reach, with
a master, a music and a sound volume. Each screen asks for its song as it comes on, and the
manager fades the old one out and the new one in. At the table, the sounds go with the lines of
the round as they show up: the opponent's revolver goes off when THEY USED REVOLVER shows up
(`TableCues`), not when you clicked the button before it.

## What is on screen

| Sprite | Source | Motion |
| --- | --- | --- |
| `ash-drift.png` | `AshDriftSprite` | Two layers tiling and drifting at different speeds for parallax |
| `black-box.png` | `BlackBoxSprite` | Slow idle bob |
| `eye-sheet.png` | `EyeSprite` | 18 eyes, 5-frame blink driven by a state machine |
| `pupil.png` | `EyeSprite` | Tracks a shared gaze point; squashes as the lids close |
| `glow.png` | `EyeSprite` | Additive red light bleeding out of the opening |
| `mote.png` | `AshSprite` | Ash spiralling into the mouth, accelerating as it is consumed |
| `button.png` | `ButtonSprite` | Nine-sliced plate; idle smoulder, kindles on hover, sinks on press |
| `panel.png` | `FormPanel` | Nine-sliced slab the forms are built on: the save form, the options, the pause menu and the customization screen |
| `room.png` | `TableScreen` | The room, drawn at twice the resolution of the rest of the table so it reads as a photograph: poured concrete under three sizes of noise, damp streaks and bloom, a rusting steel door, pipes, a vent, a camera, tally marks, and the one lamp with its haze. `ROOM_HORIZON` is where the opponent is cut off |
| `opponent-second-sheet.png` | `OpponentSprite` | Serenity: her picture cleaned up, paled and sampled to art pixels at 4x, five columns of the one still |
| `hand-sheet.png` | `HandSprite`, `CatchHandSprite` | 5 frames of your own arm, 87x115, curled through open, cut from my picture of it (`tools/source/hand.png`); the open frame is also the hand that catches the payout |
| `token-sheet.png` | `TokenSprite` | 8 frames of the steel tag the box pays with, spinning; slides down the table under its own physics |
| `hearts.png` | `HeartsSprite` | A heart, full and hollow, for both sides' lives on the heading |
| `item-sheet.png` | `ItemSprite` | A picture of every item, 18 of them in `ItemId` order; on the pocket plates and beside the dealt item's line |
| `box-lid.png` | `BoxLidSprite` | The box's jaws in the close-up: the plate over its mouth that draws back into the rim |
| `sight-sheet.png` | `AimCheck`, `ReadCheck` | 4 frames of the sight's ticks turning; wanders over her on a tremor, or steps along the tags |
| `ember-sheet.png` | `SteadyCheck` | 4 frames of the ember breathing; blown along the groove and pushed back |
| `steady-bar.png` | `SteadyCheck` | The groove and the band of light the ember has to be held in |
| `button.png` again | `PocketStrip` | Three pocket plates a side, live on your turn |
| `conscript-first.png`, `conscript-second.png` | `WalkerSprite` | The two people you can be, 32x48: five frames across (standing, four steps) and four directions down, three times over (nothing on top, a scarf, a cap). Painted in key colours and recoloured to your choices |
| `character-palettes.png` | `CharacterPalettes` | Every hair, outfit and accent preset, one ramp a row. The first of each is the key the sheets are painted in |
| `serenity-walker.png` | `WalkerSprite` | Serenity as she stands around the lobby, same layout, cut from her character sheet (`tools/source/serenity-sheet.png`). Not recoloured |
| `lobby-tiles.png` | `LobbyMap` | The lobby's floor, walls and the painted threshold, 16x16 tiles at 3x |
| `Lobby/*.png` | `LobbyProp` | The furniture, one picture each: lockers, cots, a bench, a chair, columns, crates, a bucket, a sink, two blinking cameras, and the door to the box (locked, lit, open) |

Text is drawn with `spectral-title` (Spectral Light, 92pt), `spectral-ui` (Spectral Medium,
26pt), `spectral-detail` (Spectral Light, 17pt, for the line under a save slot) and
`spectral-small` (Spectral Medium, 14pt, for an item name on a pocket), all letterspaced. The
`.spritefont` files name their TTF by relative path, so the build always uses the font shipped
in `Content/Spectral/` rather than whatever happens to be installed on the machine.

The box has no lid, seam or latch. Its front face is not a surface, it is an opening, and the
eyes hang at different depths inside it - the deep ones small, dim and crowded toward the
centre, the near ones large and bright out by the mouth.

## Regenerating the art (Thank you Claude!)

Every PNG in `Content/` is produced by a script rather than painted by hand:

```
python tools/generate_assets.py       # the box, the table, Serenity, the hand, the items
python tools/generate_characters.py   # the walking sheets and character-palettes.png
python tools/generate_lobby.py        # lobby-tiles.png and everything in Content/Lobby
```

The walking sheets are 32x48 frames, five across (standing, then four steps) and one row per
direction, the same five columns Serenity's table sheet has. The two conscripts are built out
of rounded shapes, lit from the lamp's side, and cut down to pixels with a dark edge on the
shadow side. Serenity's is cut from the character sheet I made of her: her standing and her four
walking frames each way, keyed off its backdrop and sampled down to the same frames, feet on the
same row as everybody else's. The sheet only has her walking right, so left is that mirrored.

The player's arm at the table is cut from my picture of it the same way: the picture is pixel art
that was blown up about 5.4 times, so it is sampled back down to its own pixels, and the five
hands are lined up by where the forearm leaves the picture so the arm stays put while the hand
opens.

It needs nothing but CPython - the PNG encoder is built into the script, and so is the decoder
for the pictures it reads rather than draws (Serenity, her sheet and the hand). Every
drawing is seeded, so running it again produces the same bytes; only the sprite you changed
changes.

## The table

A run is played in a room with one lamp in it, at a table, with the box centred on it and the
opponent seated behind and a little to the left. The box is drawn at `BlackBoxSprite.TableScale`
rather than the title screen's `DrawScale`: on the title it is the whole picture, and on the
table it is an object in a room with somebody behind it to be in front of. It casts a contact
shadow, without which it reads as hanging in front of the table rather than resting on it.

A round runs `Discussion` -> `Offer` -> `Reaching` -> `Catching` -> `PlayerTurn` (with a `SkillCheck` inside it whenever an item asks for one) -> `Resolving`, and then
either the next round or `Over`. The talking ends, the box asks for a hand, the hand goes in,
the box holds it for a moment, and then it pays. The hold is the point of the beat: dealing the
instant the fingers cross the rim would make the box a vending machine.

### The end of a run

Two endings and one screen. When somebody is out of lives the round closes on the log as it
always did, and then a veil comes down over the table and the verdict is set large across it:
**YOU ADVANCE** if the player is still standing, **YOU HAVE BEEN CONSUMED BY THE BOX** if they
are not, with one line under it, how the run went, and two plates: RETURN TO THE LOBBY and
RETURN TO TITLE. That is `ResultsScreen`, which came down over the table where the one plate,
LEAVE THE TABLE, used to be. Leaving either way is what turns the
page: a run that has ended is written back to its slot ready to be played again
(`SaveData.Advance` if the player got up, which turns the chapter; `SaveData.Restart` if they
did not, which clears the same table), so the slot opens on a table and not on a verdict. What
is kept either way is what the run is -- the name, the chapter, the opponent, what they think
of the player, the flags the story has raised, and the clock. The chapter is on the heading.


### The plate

Everything that is said is written on a dark plate on the right-hand side of the wall, beside
the opponent: the speaker's name at the top, the patience bar under it while there is talking
to do, and the line under that, growing downward as it wraps. The words used to be stacked
into the strip of wall above the opponent's head, and the opponent's head now reaches the top
of the wall. It is the composition of the reference this table was built from -- the figure on
one side of the frame, what is being said on the other.

Whoever is speaking owns the plate. Through the beat after a reply it carries the player's own
name and line; the rest of the time it is the opponent's; and once the talking is over it is
the box's -- what it wants, what it gave, what that did. While the player is deciding about
something the box dealt, the item's picture sits on the plate beside its line.

### The heading

The line along the top is the run's bookkeeping: the chapter, the round and the clock in the
middle, your name and hearts on the left over your pockets, THEM and their hearts on the right
over theirs, and the way out in the corner. Everything on it is measured off its neighbour --
the first draft put THEM at a fixed offset, and a long name pushed the player's hearts under
it. Yours grow rightward from the margin and theirs are right-aligned against the hint, so the
two cannot meet whatever the name is.

## The shape of an exchange

A beat of a discussion is three states, not one. The opponent's line is up and the four plates
are live; the player clicks one; the plates come down and **the player's own line replaces
theirs on the plate**, under the player's name, for as long as it takes to read; then the
answer lands and the mouth is open on the frame it arrives.

That middle state is the one that was missing. `DialogueOption.Line` -- the sentence the player
actually says -- was authored for every option and never drawn: `Answer` chose, advanced the
node and repopulated the wheel inside a single frame, so the player picked a three-word label
and went straight to the reply without ever seeing what they had said. `DiscussionPeriod.Said`
is what the screen reads it from now.

The clock does not stop for any of it. A line costs the time it takes to say, the same as it
would across a real table, which is why `SerenityDiscussion.Seconds` has room in it for what a
played discussion spends on speech -- the player is left with the deliberation time they had
before, and the pressure still comes from the box rather than from the reading.

## Talking

A round opens with a **discussion period**: the opponent says something, you get four ways to
answer, and the box allows the whole exchange a fixed number of seconds. The clock does not
stop while you read, so working out what all four options mean costs the time it takes.

The four replies always sit in the same places and always carry the same tones -- warm and
level on the left, probing and cutting on the right. Fallout 4's wheel is the model and also
the warning: it showed a two-word paraphrase and then said something else. The paraphrase here
may still surprise you; where it sits never will. `DialogueScript.Validate` refuses to load a
script that puts a tone in the wrong corner.

Running the clock out is not a failure. It produces `Tone.Silence`, which is never offered as
an option but is heard like any other answer -- colder than being plain, warier than being
warm.

**The conversation moves on.** A script has one opening node per round -- `Openings` -- and
each round's discussion starts on the next one, so the first night is an introduction, the
second is after you have both felt the box take hold, the third is where people start counting,
the fourth is the quiet before something lands, and every round after that is the two of you
running out of things to say. What the player said last round is carried in the
`Disposition`, not in the script, so each beat is a fresh one that the temper colours: the
writing does not branch on the past, it only has to sound like it remembers it.

| File | What it is |
| --- | --- |
| `Tone` | How a line is said, rather than what it says. Four are offered; `Silence` is only ever arrived at |
| `Disposition` | What one opponent thinks of you, on two axes. **Warmth** picks which version of a line they say; **Guard** decides whether the line has anything in it |
| `DialogueNode` | One beat: what they say in up to three tempers, and the four ways to answer |
| `DialogueScript` | One opponent's whole conversation, how long the box allows each round of it, and where each round opens |
| `DiscussionPeriod` | One round's discussion being played. Draws nothing, so it can be tested with no window open |
| `SerenityDiscussion` | Serenity, the first person across the table: polite, shy, worn down by the games, and on your side as far as anyone here can be. Her hostile lines are her going quiet, not cruel |

The two axes are the point. One affection meter cannot express the opponent who is perfectly
friendly and tells you nothing. It also means you cannot probe your way to candour -- asking
directly raises their guard, so the question that needs them relaxed is exactly the one a
direct question closes.

A disposition outlives the discussion it was earned in. It is written to the save, so an
opponent opens the second night remembering how the first one went -- and it is the input to
how they play, not flavour between rounds. Across the simulated table a player the opponent
has warmed to wins about five points more often than one they have turned on.

## Items

`ItemId` names every item the box can deal; `ItemCatalog` is the table behind it, holding what
each is called, what it claims to do and how heavily the box favours it. Every item has a
picture on `item-sheet.png`, in `ItemId` order so the frame is the enum value: a few shapes
each, lit from the lamp's side and averaged down like the tag, drawn on a pocket plate you are
allowed to read and beside the dealt item's line on the wall. Weights are relative
and happen to total 100, so they read as percentages while the game is being balanced. As it
stands a dealt item is worthless 10% of the time and costs somebody a life 36% of the time,
which is the dial for how cruel the box is. It was 14% and 26% before the pockets arrived, and
at those numbers a run took twenty rounds: with a pocket to keep a tourniquet in, healing
cancelled the damage and the table stalled.

`Round/ItemResolver` is the only place that knows what an item does, in a switch the compiler
checks. `ItemCatalog` stays inert on purpose -- an effect needs two sets of lives, two hands and
two pairs of pockets, and none of that belongs in a table.

Two items changed meaning when the pockets went on screen. How many of the opponent's pockets
are full is on the table for anyone to see, so a **Tally** now shows what is in them rather
than how many. And a **Levy** burns what the opponent is holding if they are holding anything,
and something out of their pocket if they are not -- the opponent plays second, by which time
the player's hand is always empty, and a levy that only ever burned a hand would have been a
blank every time they drew it.

## A round

`Discussion` -> `Offer` -> `Reaching` -> `PlayerTurn` -> `Resolving`, and then either the next
round or `Over`.

Both hands go into the box at the same moment and both sides are dealt; theirs is hidden. Then
it is **the player's turn**, and a turn is two things. First, the pockets: three plates along
the near edge of the table that are live while the turn is, and clicking one plays what is in
it -- as many as you like, one after another, each read out before the table comes back to
you. Second, the hand: **USE IT** plays the dealt item and destroys it, **KEEP IT** puts it in a
pocket, and **LEAVE IT** gives it back to the box. KEEP IT refuses two things and says which on
itself: full pockets, which you can do something about by playing one, and a blind deal, which
cannot be put away because putting it away unseen would be a way of never paying what the
rotgut charged.

Then the opponent takes theirs, second, into the unknown the player just made: they may take
one thing out of their own pockets, and then they use, pocket or give back what they were
dealt. They answer second because acting into the unknown is the shape of the round. What both
turns did is read one line at a time with CONTINUE, so a round that turns on a mirror is
legible instead of arriving as a new set of numbers.

The rules live in `Round/RoundEngine`, not in the screen. It takes a `SaveData` and a `Random`
and hands back what happened as lines, so the same code plays the table on screen and plays it
thousands of times in `Round/RoundSimulator`. The screen only decides what to draw and when.

## Saving

Three slots, one JSON file each, under
`%LOCALAPPDATA%\TheBlackBox\Saves\slot0.json`. Deliberately not next to the executable:
`bin/` is deleted by every clean build, and a save that a `dotnet clean` destroys is not a save.

| File | What it is |
| --- | --- |
| `SaveData` | The whole save: your name, chapter, opponent, round, lives, what each side is holding and carrying, what the opponent thinks of you, and a flat list of string flags for everything else that has to be remembered |
| `SaveSlot` | One slot as the form sees it -- empty, occupied or unreadable, plus the line printed on the plate |
| `SaveSystem` | Reads, writes and erases slots. The only thing in the project that touches a file |

Three decisions worth knowing about:

- **Writes are atomic.** A save goes to `slotN.json.tmp` and only then replaces the real file, so
  a crash mid-write leaves the old save intact rather than half of a new one. The displaced save
  is kept as `slotN.json.bak`.
- **Nothing throws.** A missing, locked or corrupt file comes back as a slot that says
  `UNREADABLE` and can be erased. A save system that throws turns a bad sector into a crash on
  the title screen.
- **Every file carries a `Version`.** The fields below it are a guess at a game that is not
  written yet and they will change; `SaveSystem.Upgrade` is where an old file gets brought
  forward when they do. It has been needed twice. Version 2 separated the item that has just
  been dealt from the ones kept, and reads version 1's `Hand` back out of `SaveData.Extra` --
  without it `System.Text.Json` would drop the field before the migration ever saw it. Version
  3 gave the bank a size: it is a pocket now, with `RoundRules.PocketSlots` slots, and a
  version 2 save that was carrying more keeps the oldest three.

The save form itself is `SaveSlotMenu`, drawn over the title screen rather than replacing it, so
the box is still watching while you pick a slot.

The lobby added two fields, `Look` (who you are and what you are wearing) and `LobbyTaken` (which
items have been picked up), both with defaults, so an older save just opens with the default look
and the floor full, and no version bump was needed. The volumes are not in a slot at all: they
are in `%LOCALAPPDATA%\TheBlackBox\settings.json`, since turning the music down should not depend
on which run is loaded.

## Where things are

- `BlackBoxGame.cs` sets up the window, the screen manager, the box and the sound, and puts the title up. `BlackBoxGame.Proof.cs` takes the proof shots.
- `StateManagement/` is the game state management tutorial: `GameScreen`, `ScreenManager`, `MenuScreen` and `MenuEntry`, `LoadingScreen`, and the input classes from the advanced input tutorial.
- `Screens/` is every screen but the table, one per file (the lobby and customization screens have their part of the proof schedule beside them).
- `Table/` is the table: `TableScreen` split by phase (the discussion, the hand, the turn, and its part of the proof schedule), and the plate, the heading, the ending veil and the sound cues it draws and plays with.
- `Lobby/` is the lobby's pieces: the map, the camera, the props, the people, the items on the floor, the dialogue box and what is said in it.
- `Characters/` is the walking sprite, the saved look and the palette swap.
- `Audio/` is the `AudioManager`.
- `Components/` is every sprite and screen element, each with `LoadContent`, `Update` and `Draw` the way the tutorials do it. `Components/Checks/` is the three skill checks.
- `Enums/` is every enum in the game, one file each. `Opponents/` is the roster, one type each.
- `Dialogue/`, `Items/`, `Round/` and `Save/` are the rules: talking, what the box deals, how a round plays, and the save file.
- `Collisions/` is the bounding shapes the checks test with.
- `Layers.cs` is the draw order, `Palette.cs` is the colours more than one screen uses, and `Text.cs` draws a line of text with its shadow for everything that writes on the wall.

## Assets

Every picture, song and sound in the game is original: drawn or synthesised by the three
`tools/` scripts and `tools/generate_audio.py`, or (the pictures in `tools/source/`) supplied by me.
The only outside work is the Spectral font and the MonoGame template files. See
[ASSETS.md](ASSETS.md) for every file.

## Author's Note:
I am extremely excited to be building this game this semester. I believe it will be a fun and interactive game. The inspiration behind this game is Buckshot Roulette and No I'm Not a Human. Please give me feed back on the current work as this may become a passion project. 

Note: There is lore that I am currently writing, but I am one who loves to have the players theorize :)
