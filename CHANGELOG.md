# Features

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
