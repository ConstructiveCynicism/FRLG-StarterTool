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
- Adapter Manip
- Encounter Manip
- Generic Timers
- Torrent tracker
- Savestate Editor

## Offset Trainer
The Offset Trainer can be used independently of the game, assuming that you start and stop the timer at the same time as the game inputs. I would recommend training the audio and visual offsets independently, to account for any audio delay and difference in audio cue reaction.

## Other Games 
Basic flow timer features added with the increased timing performance and hit predictor.

## Squirtle Frame Prediction
Because its not possible to know where in the frame the timer is started, the exact same input spacing can result in different frames. The predictor can tell you the odds of the input landing on each of the two possible frames. If the squirtle does not match either of the predicted frames, there were untracked additional frame advancements.

## Context Manip
Tracks possible RNG frames through anchor points, and compares them against observations from the runner to pinpoint exact RNG advancements. This allows the inference of off screen NPC movements.

## Encounter Manip
Provides a list of possible input combinations and offsets to hit various initial RNG seeds

## Adapter Manip
Using context manip, helps track parity to allow runners to hit starter manip with the adapter in, doubling possible squirtles.

## Savestate Editor (WIP)
Allows the changing of savestate Squirtle stats for efficient practice.

## Torrent Tracker (WIP)
An easy visual way to see HP per level and torrent thresholds based off HP IV.

## Updates
The tool checks GitHub for a newer release a few seconds after it opens, and whenever you click `Help ▸ Check for updates…`. A newer release shows its changelog entry with `Update`, `Skip` and `Snooze`: Update downloads it, checks it against the SHA-256 GitHub recorded for it, swaps the running exe out and restarts; Skip keeps quiet about that version; Snooze (or closing the box) asks again next launch. An `Update vX.Y.Z` badge stays on the menu bar meanwhile and reopens the box. `Help ▸ About` names the exact build, and a CI test build also wears its `ci.<n>` run number on the menu bar.

## Verifying a download
Every release is built and attested by GitHub Actions; `SECURITY.md` has the `gh attestation verify` and checksum commands. What the tool stores and what it connects to is in `PRIVACY.md`, and how to build it and contribute is in `CONTRIBUTING.md`.

## Attribution
Built on the work of three tools
• Gen3Predictor — MKDasher, modified by JP_Xinnam
• FlowTimer — stringflow / Gunnermaniac (gunnermaniac.com/ft)
• Starter Program — stringflow
Copyright for any code borrowed is retained by their respective owners. 

Copyright for all other code is subject to the MIT License.

The released exe includes FFmpeg (ffmpeg.org) in a GPL-3.0 build, used to read OBS recordings, and
is therefore distributed under the GNU General Public License v3 - see `LICENSE.md` and
`THIRD-PARTY-NOTICES.md`.
