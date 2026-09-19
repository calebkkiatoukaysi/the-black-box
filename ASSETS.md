# Assets

Every asset the game ships, and where it came from. Nothing here was downloaded: the art is
drawn by `tools/generate_assets.py` at build time, the two pictures it reads instead of drawing
are mine, and the only outside work is the font and the MonoGame template files.

## Sprites

Content/black-box.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/eye-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/pupil.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/glow.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/mote.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/button.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/panel.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/ash-drift.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/room.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/hand-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/token-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/item-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/sight-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/ember-sheet.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/steady-bar.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/hearts.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

Content/box-lid.png - created by Caleb Kiatoukaysi for The Black Box, drawn procedurally by tools/generate_assets.py, released under public domain

## The two opponents

These are the only sprites the generator does not draw from nothing. Both start from a picture
I made, and the generator keys, recolours and samples it into a sheet.

Content/opponent-second-sheet.png - built by tools/generate_assets.py from tools/source/opponent-second-portrait.png: Serenity's picture, the chapter-one opponent, keyed off the wall behind her, cleaned up, paled and sampled to art pixels, five columns of the one still; the picture was supplied by Caleb Kiatoukaysi for The Black Box

tools/source/opponent-second-portrait.png - a picture of Serenity, the chapter-one opponent, sitting at the table, supplied by Caleb Kiatoukaysi for The Black Box; one of the two pictures the generator reads rather than draws

Content/opponent-sheet.png - built by tools/generate_assets.py from tools/source/opponent-portrait.png: the portrait off the chapter-two opponent's character sheet, keyed and sampled to art pixels, with each of the five poses made as pixel edits on it; the character sheet was supplied by Caleb Kiatoukaysi for The Black Box

tools/source/opponent-portrait.png - the portrait from the chapter-two opponent's character sheet, supplied by Caleb Kiatoukaysi for The Black Box; the other picture the generator reads rather than draws

## Text

Content/Spectral/Spectral-Light.ttf - Spectral font created by Production Type, released on Google Fonts (https://fonts.google.com/specimen/Spectral) under the SIL Open Font License

Content/Spectral/Spectral-Medium.ttf - Spectral font created by Production Type, released on Google Fonts (https://fonts.google.com/specimen/Spectral) under the SIL Open Font License

Content/Spectral/OFL.txt - the SIL Open Font License text and copyright notice for Spectral, redistributed alongside the font as that licence requires

Content/spectral-title.spritefont - written by Caleb Kiatoukaysi for The Black Box, from the MonoGame SpriteFont template; names Spectral Light at 92pt by relative path, so the build uses the font shipped in Content/Spectral/ rather than whatever is installed

Content/spectral-ui.spritefont - written by Caleb Kiatoukaysi for The Black Box, from the MonoGame SpriteFont template; Spectral Medium at 26pt, letterspaced

Content/spectral-detail.spritefont - written by Caleb Kiatoukaysi for The Black Box, from the MonoGame SpriteFont template; Spectral Light at 17pt, letterspaced

Content/spectral-small.spritefont - written by Caleb Kiatoukaysi for The Black Box, from the MonoGame SpriteFont template; Spectral Medium at 14pt, for an item name on a pocket plate

## From the MonoGame template

MonoGame.Framework.DesktopGL and MonoGame.Content.Builder.Task - the framework the game is built on and the content pipeline that builds Content/, created by the MonoGame Team (https://www.monogame.net/) and distributed under the Microsoft Public License (https://licenses.nuget.org/MS-PL)

Icon.ico - MonoGame project icon, unmodified from the MonoGame DesktopGL project template, created by the MonoGame Team (https://www.monogame.net/) and distributed under the Microsoft Public License (https://licenses.nuget.org/MS-PL)

Icon.bmp - MonoGame project icon, unmodified from the MonoGame DesktopGL project template, created by the MonoGame Team (https://www.monogame.net/) and distributed under the Microsoft Public License (https://licenses.nuget.org/MS-PL)

app.manifest - the Windows application manifest, unmodified from the MonoGame DesktopGL project template, created by the MonoGame Team (https://www.monogame.net/) and distributed under the Microsoft Public License (https://licenses.nuget.org/MS-PL)
