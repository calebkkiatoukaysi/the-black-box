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

**START GAME** opens the save form. A slot that has never been named asks what you are
called before it lets you sit down; a slot that has been picks up where it was -- including
mid-decision, if you left with something in your hand. **EXIT** and **Esc** close the game.
Esc means "back" rather than "quit" once you are past the title screen -- it closes the form,
and it saves and leaves a run. The eyes follow your mouse cursor; leave it alone for a couple
of seconds and the box goes back to looking around the room on its own.

Two switches exist for working on the game, and neither is the game:

```
dotnet run -- --simulate 5000     # play the table headless and print who wins
dotnet run -- --proof shots       # render the table to shots/*.png and quit
```

The first is how the numbers in `RoundRules` were chosen (see [A round](#a-round)). The second
is how the layout is checked after any change to the sheet, the plate or the pockets, and how
the pictures for a release get taken. It opens a scratch run that never touches a save slot.

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
| `panel.png` | `SaveSlotMenu`, `NameEntry` | Nine-sliced slab the forms are built on |
| `room.png` | `TableScreen` | The room, drawn at twice the resolution of the rest of the table so it reads as a photograph: poured concrete under three sizes of noise, damp streaks and bloom, a rusting steel door, pipes, a vent, a camera, tally marks, and the one lamp with its haze. `ROOM_HORIZON` is where the opponent is cut off |
| `opponent-second-sheet.png` | `OpponentSprite` | The default opponent, chapter one: her picture cleaned up, paled and sampled to art pixels at 4x, five columns of the one still |
| `opponent-sheet.png` | `OpponentSprite` | The opponent from the character sheet, chapter two: 5 poses across, one row. Static |
| `hand-sheet.png` | `HandSprite`, `CatchHandSprite` | 5 frames of your own arm, elbow to fingertips, curled through offered; the open frame is also the hand that catches the payout |
| `token-sheet.png` | `TokenSprite` | 8 frames of the steel tag the box pays with, spinning; slides down the table under its own physics |
| `hearts.png` | `HeartsSprite` | A heart, full and hollow, for both sides' lives on the heading |
| `item-sheet.png` | `ItemSprite` | A picture of every item, 18 of them in `ItemId` order; on the pocket plates and beside the dealt item's line |
| `box-lid.png` | `BoxLidSprite` | The box's jaws in the close-up: the plate over its mouth that draws back into the rim |
| `sight-sheet.png` | `AimCheck`, `ReadCheck` | 4 frames of the sight's ticks turning; wanders over her on a tremor, or steps along the tags |
| `ember-sheet.png` | `SteadyCheck` | 4 frames of the ember breathing; blown along the groove and pushed back |
| `steady-bar.png` | `SteadyCheck` | The groove and the band of light the ember has to be held in |
| `button.png` again | `PocketStrip` | Three pocket plates a side, live on your turn |

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
python tools/generate_assets.py
```

It needs nothing but CPython - the PNG encoder is built into the script, and so is the decoder
for the two pictures it reads rather than draws (the opponents' portraits, see below). Every
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

### The opponent

The first opponent is big. At 6x a frame of `opponent-sheet.png` runs from the table lip to
the top of the wall, the way a visitor fills a doorway in the games this one is built on. The
first sheet was a head and a collar drawn at 5x, and at that size the box could have swallowed
it. The fix was not a bigger number but a bigger person, drawn to fill the frame.

She is a reference to Nikki from *Obsession* (2025) -- specifically to what that film does with
a lovely face, which is hold it perfectly still and smile with it a little too wide. She is the
first of the two sprites in the game that the generator does not draw. She is cut from her character sheet:
the portrait in its corner, keyed off the sheet's background and box-sampled down to art
pixels (four image pixels to one, a little finer than the sheet's own uneven grid), is the
`Even` pose, and every other cell is that same picture with a few pixels moved. The source is
`tools/source/opponent-portrait.png`, the portrait alone at the sheet's own resolution; the
rest of the sheet stays out of the build. I drew her twice before this -- a lit height map,
then a painted head to the sheet's palette -- and both were a likeness of her, which is not
the same thing as her. The sheet is the design, so the sheet is the sprite.

The sheet gives the face and the hair: long, straight, parted down the middle, hanging in
front of the shoulders, and not much volume in it. She is meant to resemble Nikki, not to be
her, and to be pretty, which is the sheet's job -- the less done to it the better it does it.
Five things it does not have make her the reference, and all five are recolourings or small
edits of what is there rather than paint over it. The hair is black (`opp_edit_hair_black`):
every pixel of it keeps its brightness and loses its colour, so every streak the sheet painted
into it is still there, and the hairline blends into the forehead the way it did. The skin is
pale (`opp_edit_skin`): everything warm enough to be skin, down to the shadow under the jaw, is
pulled most of the way toward one pale cool tone, so what was darker stays darker, only
closer, and the blush is left in fainter so she reads as kept rather than ill. The eyes are
East Asian, as far as eight pixels of eye can be (`opp_edit_eye_shape`): the crease above the
lid is painted over with the lid's own skin so it is one smooth lid from brow to lash, and the
outer corner of the lash line lifts a pixel -- the opening is left the size the sheet drew
it, because bringing the lid down a row as well made her look asleep. The oatmeal knit is
recoloured to a dark top, every rib and fold of it kept at the new colour (`opp_edit_dress`).
And the near ear is tucked out of the hair with a hoop in it (`opp_edit_ear`), painted in the
skin of the cheek beside it.
while and taken off again: it gave her more hair than face. The chain on the sheet is already
hers. The hooded smoker she replaced
had the hair over his face and nothing much under it; the point of her is that there is a
person there to lose.

They sit left of centre on purpose. The box owns the middle of the table, and a figure drawn
straight behind it is a figure the box covers from the collar down. Off to one side, the box
covers their far shoulder and nothing else: the face, the chain and the near shoulder are
always in the open. The words moved to make room -- see [The plate](#the-plate).

**Nothing on the opponent moves, ever.** Every cell of `opponent-sheet.png` is one finished
picture and the game switches between them; there is no blink, no loop, no timer anywhere in
`OpponentSprite`. A jaw flapping through a line it has no audio for reads as a puppet, and a
blink on a loop reads as a screensaver. What the player is meant to notice is that the face is
not the one that was there a moment ago.

One axis picks the picture now. **Pose** is the column: the three `Warmth` bands while there
is talking to do, plus `Reaching` and `Hurt`. None of them is square to the player.
`Even` has the head over toward the box a little and down a touch, the way someone sits when
they have been sitting a while, and it is the talking face too: nothing changes when a line
lands. There was a talking column once, the head lifted and leant in, and it jumped on every
line like a tic, so the idle is the talking. `Hostile` is the one to look at: it is not a scowl. The head goes over to one
side, the eyes open a pixel wider than they should, and the grin runs past the corners of the
mouth with a row of teeth in it, held. `Open` is the same face smiling properly, and the sheet
is about how little separates the two. The sheet had three rows once, for the wounds she took,
and they are gone: what has been taken off her is on the **hearts** on the heading (`HeartsSprite`,
both sides, spent ones drawn hollow), not on her face. She is meant to stay pretty at this
table whatever the count is, and a split lip is not that.

The poses are pixel edits in `opp_edit_eyes` and `opp_edit_mouth`, and they are small on
purpose: the eyes are seven pixels wide and the mouth is ten, and at that size one pixel is a
whole expression. A smile lifts the outer three columns of the mouth by a row; shut eyes
are the skin from the cheek painted over the opening with a lash line laid across. Anything
bigger than that and it stops being her face.

Tilting the head bends the picture rather than turning it (`opp_bend`). Every row above the
chin turns by the full angle about the base of the neck, the rows down through the neck turn
less and less, and the shoulders do not move -- so the hair hanging in front of them stays
joined to the hair on her head, and the shoulders stay on the table. It is sampled backwards,
one source pixel per frame pixel, so nothing is blended and there are no holes.

Two numbers hold the composition together: `OpponentFoot` in `TableScreen`, and
`ROOM_HORIZON` in `tools/generate_assets.py`, which is the table line and the bottom of them.
The top of the frame has to clear the run's bookkeeping along the top edge, so the sheet's
height (`OPP_OH`) is cut to exactly that distance at that scale: 100 rows at 6x, where the
drawn one was 150 at 4x. Change one and recut the other.

### The second opponent

`Opponents` is the roster, and she is first on it, so she is the one across the table by default and the one an old save finds. The character-sheet opponent above sits down in chapter two. The roster is: one `Opponent` record per person --
an id for the save, a sheet for the sprite with its frame size and scale, where the face is
for the aim check, and a script for the talking -- in chapter order, and `SaveData.Advance`
empties the seat so that the next time the run is entered the roster fills it. A player who is
consumed sits back down with the same person; a player who gets up meets the next one. Every
sheet has the same five columns in the same order, so `OpponentSprite` cuts whichever one `Who`
names at whatever size `Who` says. She is Serenity, and she has her own script; the
character-sheet opponent talks through the stand-in script until she has one.

She comes off a picture, `tools/source/opponent-second-portrait.png`, of her at the table with
the wall behind her and the plate and the box in front. The wall is keyed by temperature rather
than by colour, because it is a lit wall and not a flat grey: it is cool everywhere and she is
warm to the last strand, so the wall is whatever a flood from the border reaches that is cool,
with the leaks into the darkest hair closed up and only the largest piece of what is left kept,
which drops the stain on the wall above her shoulder. Where the plate and the box cover her arms,
each column is filled from the last rows she is visible for. Then four things are done to her
before she is sampled down: the scar and the dirt are cleaned off her face (a dark speck inside
the face becomes the skin around it, with the eyes, nose and mouth left alone), the skin is
pulled toward a pale grey, the eye sockets are put in shadow from the brow down, and she is
sampled at four picture pixels to the art pixel and drawn at 4x. I tried her at the picture's
own pixels first and it was too much detail for the table. Every column of her sheet is that
one still. A mouth edit looked pasted on and a head bend jumped, so nothing on her moves when
a line lands, the same as the other one.

### The arm

Your own arm is `hand-sheet.png`, and it is the thing nearest the camera. It comes in from the
bottom-right corner on a diagonal and the wrist turns, so what you see is the back of your
own hand with the fingers going out to the left, stacked one above the next, relaxed and a
little curled, the thumb riding along the top edge: the way your right hand looks when you
reach into something in front of you. It is modelled on a photograph of exactly that, and
drawn from nothing -- the skeleton in the generator is the pose, and every pixel is lit off
it. It slides out along its own length: `HandRest` and `HandMouth` in `TableScreen` are both
on the line the arm is drawn along, so the elbow end never leaves the edge of the screen and
the arm is always coming out of somebody. The sprite
is placed by the tip of the middle finger, because that is the only part of it whose
position matters -- the game says where the fingers land and the arm follows.

It is a whole forearm with the cuff of a sleeve at the near end, drawn big in its own pixels
rather than blown up further than the rest of the table, and at 6x in the close-up it fills the
corner the way an arm fills the bottom of your own eye. The first sheet was a hand on its own, forty pixels
wide and drawn straight up the middle, and it floated: a hand with nothing behind it reads as
a glove. The five frames go from half-curled to open and fanned, and one number drives both the
pose and the position, so the fingers finish opening at the moment the hand finishes arriving.

It is meant to look like a photograph of a hand shrunk to pixels, the way she does, so it is
not drawn: it is modelled and lit. The arm is a height field in the generator -- the forearm
and the back of the hand flattened tubes, each finger three rounded segments with a nail set
into the last, the thumb two, the knuckles bumps that rise as the hand closes, the tendons low
ridges up the back of the hand, the joints creases across it -- and every pixel is coloured by
the angle that surface makes with the lamp, with the skin going red where the light comes
through the edge of a finger and dark where two fingers meet. It is rendered at three times
the frame size and averaged down, which is what gives it the soft pixels of a picture rather
than the hard ones of a drawing. The second sheet was this arm drawn off a distance field, and
it read as a glove too: fingers are not tubes. The fingertip the sprite is anchored by is
computed from the skeleton when the sheet is built, and the build stops if it drifts from
`HAND_FINGERTIP`.

### The room

One lamp is still all the light there is, and everything in the room is lit by it and only
it, so what is in the far corner is a shape in the dark and what is under the lamp has a
highlight down it. It is drawn at twice the resolution of the rest of the table, on purpose:
the room is the one thing on screen that is not a sprite, and a photograph of a wall has more
in it than a sprite's pixel can hold. The wall is poured concrete, three sizes of value noise
on top of each other -- the mottling of the pour, the grain of the aggregate, the dust --
with damp running down it in streaks from the top and from under every pipe joint, pale bloom
where the water dried, seams between the panels with their edges chipped, and cracks that
branch. The lamp has a haze under it where the dust in the air catches the light. The table
has a grain, the sheen of the lamp lying on it past the lip, the scuffs of everyone who has
reached across it, and the rings of things set down wet and left.

The room is dressed where she is not: the left wall has a steel door, riveted, rust coming up
from the bottom of it and out from every rivet, with a small barred window and the cold light
of a corridor behind it -- the only light in the room that is not the lamp's, and the only way
out. Pipes run along the top of the wall either side of the flex, sweating rust at their
rings, with a valve where the near one turns down and the stain each joint has been dripping
onto the wall for years; a cable sags from the flex across to the right. The wall is painted
two tones with the line at shoulder height, the way rooms like this are painted so the lower
half can be scrubbed, and the paint has worn through in places. Under the plate on the right
there is a louvred vent, a camera in the corner with its one red light -- the box is not the
only thing watching -- and tally marks scratched into the paint in fives by whoever sat here
before. Nobody knows what they were counting. Rounds, probably.

### The end of a run

Two endings and one screen. When somebody is out of lives the round closes on the log as it
always did, and then a veil comes down over the table and the verdict is set large across it:
**YOU ADVANCE** if the player is still standing, **YOU HAVE BEEN CONSUMED BY THE BOX** if they
are not, with one line under it and the one plate, LEAVE THE TABLE. Leaving is what turns the
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
would across a real table, which is why `DemoDiscussion.Seconds` went up by about what a played
discussion now spends on speech -- the player is left with the deliberation time they had
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
| `DemoDiscussion` | A stand-in opponent, meant to be thrown away |

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

### The trial run

This is the first table, and it leans the player's way on purpose: the point of it is to teach
how a round works, and a player who is shot dead while they are still learning what a pocket
is has been taught nothing. Every dial is in `Round/RoundRules`:

- **Pockets.** Three a side, the same for both. The player may play any number of them in a
  turn; the opponent plays at most one, and only looks in their pockets half the time.
- **Favour.** Half the time the box deals the player a blank or a pact, it takes it back and
  deals again. Once, so a bad hand is still possible. The opponent gets no favour.
- **Restraint.** The opponent's willingness to fire a weapon is what their temper says it is,
  times 0.7.

`dotnet run -- --simulate 4000` plays the table with a plain, unclever player -- fires a
revolver when it has one, patches itself when it is hurt, keeps a guard for when it is
frightened and throws blanks away -- at each temper the opponent can be in:

```
TEMPER     WON     LOST   UNFINISHED   ROUNDS
HOSTILE    67 %    32 %        1 %     11.9
EVEN       70 %    29 %        1 %     12.1
OPEN       72 %    27 %        1 %     12.5
```

Seven in ten, a dozen rounds, and talking worth five points. Change a number in `RoundRules`
or a weight in `ItemCatalog` and run that before trusting it.

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

## Where things are

- `BlackBoxGame.cs` is the title screen, the forms and the sprite batches. `BlackBoxGame.Proof.cs` takes the proof shots.
- `Table/` is the run: `TableScreen` split by phase (the discussion, the hand, the turn, and its part of the proof schedule), and the plate, the heading and the ending veil it draws with.
- `Components/` is every sprite and screen element, each with `LoadContent`, `Update` and `Draw` the way the tutorials do it. `Components/Checks/` is the three skill checks.
- `Enums/` is every enum in the game, one file each. `Opponents/` is the roster, one type each.
- `Dialogue/`, `Items/`, `Round/` and `Save/` are the rules: talking, what the box deals, how a round plays, and the save file.
- `Collisions/` is the bounding shapes the checks test with.
- `Layers.cs` is the draw order, `Palette.cs` is the colours more than one screen uses, and `Text.cs` draws a line of text with its shadow for everything that writes on the wall.

## Assets

See [ASSETS.md](ASSETS.md).

## Author's Note:
I am extremely excited to be building this game this semester. I believe it will be a fun and interactive game. The inspiration behind this game is Buckshot Roulette and No I'm Not a Human. Please give me feed back on the current work as this may become a passion project. 

Note: There is lore that I am currently writing, but I am one who loves to have the players theorize :)
