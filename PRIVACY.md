FRLG Starter Tool is a timer and RNG predictor that runs on your own PC. It does not collect nor share any kind of personal information whatsoever.

The tool makes two kinds of network request, neither of which carries anything about you beyond your IP address: it asks GitHub for the latest release when it opens (and when you click `Help ▸ Check for updates…`), and, unless `Atomic clock sync` is switched off in Settings, it reads the time from a public NTP server to measure your clock's drift. The optional OBS stat feed is a local web server that serves the stat boxes and nothing else; it is off by default and listens on your own machine only unless you allow the network.

The tool saves its settings and a plain-text log of each timed attempt under `%AppData%\frlg-startertool`. These are only ever saved on your local device, and can only reach a developer if you send them over yourself.
