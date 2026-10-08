# How it works

## What it does

A kerbal's head is drawn with one picture (one for the men, one for the women) that has the skin, the
hair and the inside of the mouth all on it. For each skin colour in use the mod makes a copy of that
picture in which only the skin is changed: every point that is the kerbals' own yellow-green is given
the new colour, as light or as dark as it was, so the shading round the eyes and under the hair stays;
hair, lips, teeth, tongue, eyes and suits are left alone. The copy goes to that kerbal's head and
nowhere else.

Everything that shows the kerbal shows the new skin: the view from inside the cockpit, the portraits at
the bottom right, and the kerbal outside the ship.

## What it costs

Nothing while nothing changes. A new colour is a million points of arithmetic, done on another thread
(the game does not wait for it; the kerbal changes a moment later), and a picture of 5 MB on the
graphics card for as long as a kerbal wears it. While a slider is dragged a copy is made for about every
tenth colour passed through; the ones nobody is wearing are thrown away after four seconds.
