### v2.4.1
- anchor lockout
- better fence guy rating
- parity update when swapping squirtles

### v2.4.0
- Experimental Adapter Manip (off by default)
- Experimental Audio shift (off by default)

### v2.3.1
- Encounter Manip accuracy changes: 1/14 complete failure chance fixed, 1/2 as likely for duplicate fence guys

### v2.3.0
- Added generic Flowtimer based timers, requested for Gen 3
- Added video capture support for active OBS recordings (use .mkv files and default file naming)

### v2.2.0
- More encounter manip seeds (not close to done, only partial for Fire Red, no new ones for Leaf Green)
- Updated input UI for seeds, and tile prediction
- PC Potion audio cue, configured in the top left of the Constraints tab
- Encounter Manip Visual Cue Helper

### v2.1.4
- Backspace added as global hotkey
- Removed guessed loop tables, added confirmed single loop tables
- Changed Audio API, lower latency, offsets likely changed
- Fixed a bug that often resulted in negative delay for encounter manip, likely needs a small recalibration

### v2.1.3
- Fixed DS FPS
- Routes can now have their own Offset and Delay, these **OVERRIDE** the settings, not add

### v2.1.0
- Encounter Manip no longer targets the edge of the window
- Fixed a bug with negative delays
- Encounter Manip hit prediction corrected
- Constraints/Routes can be imported/exported

### v2.0.0
- Encounter Manip + Planner
- ROM Patching for Encounter Manip testing
- Settings now organized by header
- Multiple constraint filters and colors for easier starter selection
- Atomic + System clock syncing for higher accuracy
- Controller support
- Unlimited keybinds + combo support
- Better support for unusual offsets

### v1.4.0
- window scaling hotfix (untested)
- fixed more hotkey overlap bugs
- Fence Guy Parity now modeled by default
- Added more tips (very helpful)
- Stat box can be streamed as a browser capture
- Hit chances now adjusted for context and OS delay
- savestate editor (WIP, not ready for use)
- num row global entry bugfix

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
