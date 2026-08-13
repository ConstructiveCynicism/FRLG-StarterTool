

# FRLG Starter Tool

An improvement to the existing community resources for starter manip. This was made for my personal use and distributed for anyone else who finds it useful.

## New Features
- Fully global hotkeys
- Offset trainer
- Visual Cue (don't use on 60hz monitor)
- Squirtle frame prediction
- Clipboard stat export
- Dark mode
- Customizable display
- Window Pinning
- Context Manip

## Offset Trainer
The Offset Trainer can be used independently of the game, assuming that you start and stop the timer at the same time as the game inputs. I would recommend training the audio and visual offsets independently, to account for any audio delay and difference in audio cue reaction.

## Squirtle Frame Prediction
Because its not possible to know where in the frame the timer is started, the exact same input spacing can result in different frames. The predictor can tell you the odds of the input landing on each of the two possible frames. If the squirtle does not match either of the predicted frames, there were untracked additional frame advancements.

## Context Manip
Tracks possible RNG frames through anchor points, and compares them against observations from the runner to pinpoint exact RNG advancements.

## Attribution
Built on the work of three tools
• Gen3Predictor — MKDasher, modified by JP_Xinnam
• FlowTimer — Gunnermaniac (gunnermaniac.com/ft)
• Starter Program — stringflow
Copyright for any code borrowed is retained by their respective owners. 

Copyright for all other code is subject to the MIT License.
