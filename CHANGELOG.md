### v1.4.0
- Offset trainer: pressing Start early no longer cuts the rest of the countdown - the remaining beeps
  and clock flashes play out, so you still hear the beat you missed
- NPC Troubleshooter reworked: each NPC has its sprite and its report is drawn as arrows, the lines
  start on the box the run answered with, and the tracking keys (or arrows/WASD) build them
- Troubleshooter: Sweep replaced by Search - it reads all three presses and both fence guy parities
  at once, takes the frame you actually hit, and shows the five likeliest readings, greenest first
- Troubleshooter: the observable window (65/95/111) is now a button you cycle rather than something
  the search splits its results over - it starts on the window the run measured and searches that one
- Troubleshooter: an option row now ends in the three presses it reads (exit/oak/lab) rather than
  redrawing the movements you had just reported
- Troubleshooter: the section is captioned "NPC Troubleshooter" while it is up, every readout in it
  has been cut to a line, and the clipping is gone - the run picker no longer has its text cut off at
  75%, the report keys stay inside their column and the summary has room for both its lines
- Fence Guy Parity now modeled by default
- Added more tips (very helpful)
- Stat box can be streamed as a browser capture
- Hit chances now adjusted for context and OS delay

## v1.3.1
fix + - bindings on flow timer
75% on more monitor sizes
56.6555fps added for DS
beeps no longer cut off in offset training

### v1.3.0

# Features
- NPC Troubleshooter, can help figure out context window issues (very beta stage, need to human review and fix some code, manual UI)
- Added an even later window for getting to the pokeball
- Added audio cue for anchor 3 as a replacement (highly recommend changing the frames used on it, 0 is TAS and default is a really fast one I did)
- Changed window zoom, should??? default to 75% on 1080p monitors
- Settings window capped with scroll bar
- Added post run tips
- Lab Delay timings can be toggled on, higher control in Lab at the cost of some options not being shown without scrolling

# Bugfixes
- Arrow keys no longer scroll the found list by default
- Global hotkeys work while tabbed in
- Hit predictor now accounts for the context window/os delays
- Swapping lab frames will requeue your audio, glitchy but gives you a chance to hit the squirtle still
- Offset trainer: pressing Start early no longer cuts off the beep you pressed on - the beep in progress always finishes, only the beeps after it are cancelled
