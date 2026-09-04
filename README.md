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

Press **Esc**, to exit. The eyes follow your mouse cursor; leave it
alone for a couple of seconds and the box goes back to looking around the room on its own. 

## What is on screen

| Sprite | Source | Motion |
| --- | --- | --- |
| `ash-drift.png` | `AshDriftSprite` | Two layers tiling and drifting at different speeds for parallax |
| `black-box.png` | `BlackBoxSprite` | Slow idle bob |
| `eye-sheet.png` | `EyeSprite` | 18 eyes, 5-frame blink driven by a state machine |
| `pupil.png` | `EyeSprite` | Tracks a shared gaze point; squashes as the lids close |
| `glow.png` | `EyeSprite` | Additive red light bleeding out of the opening |
| `mote.png` | `MoteSprite` | Ash spiralling into the mouth, accelerating as it is consumed |

Text is drawn with `spectral-title` (Spectral Light, 92pt) and `spectral-ui` (Spectral Medium,
26pt), both letterspaced. The `.spritefont` files name their TTF by relative path, so the
build always uses the font shipped in `Content/Spectral/` rather than whatever happens to be
installed on the machine.

The box has no lid, seam or latch. Its front face is not a surface, it is an opening, and the
eyes hang at different depths inside it - the deep ones small, dim and crowded toward the
centre, the near ones large and bright out by the mouth.

## Regenerating the art (Thank you Claude!)

Every PNG in `Content/` is drawn by a script rather than painted by hand:

```
python tools/generate_assets.py
```

It needs nothing but CPython - the PNG encoder is built into the script.

## Assets

See [ASSETS.md](ASSETS.md).

## Author's Note:
I am extremely excited to be building this game this semester. I believe it will be a fun and interactive game. The inspiration behind this game is Buckshot Roulette and No I'm Not a Human. Please give me feed back on the current work as this may become a passion project. 

Note: There is lore that I am currently writing, but I am one who loves to have the players theorize :)
