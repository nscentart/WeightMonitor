# Changelog

## 1.3 (17/11/2026)
A flyout for the virtual memory, a cleaner log and new pictures. Everything else works as in 1.2.
- **Virtual memory flyout:** a new flyout for the committed memory, opened from the VM part of the bar, from the VM row of the overview, from the Committed row of the RAM flyout and from the low committed memory notification. It shows how much is committed, the limit and how much is left before it (red under 10 GB), how much sits in RAM and how much in the paging file, the paging file size, the peak since startup, a graph over the last second, minute and hour, the five programs that commit the most with a Close button, and a line of advice.
- **Overview:** the virtual memory has its own row, with the percentage and the space left before the limit.
- **Texts:** "Committed" in the RAM flyout is now "Engagée" in French and "Comprometida" in Spanish.
- **Log:** every line of `data\WeightMonitor.log` is now written in English whatever the program language; the texts shown to the user keep their translations.
- **Pictures:** the pictures in `docs` are regenerated and a new one shows the virtual memory flyout.

## 1.2 (17/11/2026)
Code base rewritten in English and cleaned up, a fix for the bar and a new memory reading.
- **Bar:** the bar never shows the red X again. If a drawing fails, that part shows "n/d" for a moment and the program tries again at the next refresh.
- **Committed memory:** the bar, the tooltip, the overview and the RAM flyout show the committed memory. When less than 10 GB is left before the limit, it turns red and a notification warns you.
- **Code:** folders, files, classes, methods, option names, log lines and test descriptions are in English, and the source holds no comments.
- **Layout:** `src` (with the tests in `src/Tests`), `locales`, `assets`, `scripts`, `tests` and `docs`; everything the build produces goes to `bin`.
- **Files:** the program now writes `data\settings.ini` and `data\license.txt`. The files written by 1.0 and 1.1 (`dati\impostazioni.ini`, `dati\licenza.txt`, the old log and the old temporary folders) are read once when the new ones are missing and are never changed or deleted by the program while it runs.
- **Options:** the command-line options are in English; the old names `--disinstalla`, `--apri`, `--stile` and `--installa` (with `--utente` and `--lingua`) still work.
- **Tests:** a new migration suite checks the reading of the 1.0 and 1.1 files against sample files in `tests/legacy-data`.
- **Releases:** 1.0 and 1.1 were withdrawn; 1.2 replaces them.

## 1.1 (03/11/2026)
Small fixes to the window that announces a new version.
- **Update notice:** the sentence under the title is no longer cut off in any language or at any screen zoom.
- **Fingerprint:** the window shows the SHA-256 fingerprint of the new exe on a fixed line above the notes, ready to select and copy and to compare with the file you download. If GitHub and the notes do not agree, or no fingerprint is given, the window says so. The program still downloads and compares nothing by itself.
- **Notes:** shown as plain text, without tags such as details and summary.
- **Page:** the notice only opens the official version page.

## 1.0 (03/11/2026)
First public version.
- **Flyouts:** details for CPU, GPU, RAM, disks and network. With a flyout open, a click on another part opens that one at once. The Network flyout has a fixed height and the program list has a scroll bar on the right.
- **Network:** who is using the network, program by program, with a Close button and a confirmation.
- **Notices:** a notification when the PC is struggling, and a sentence at the top of the overview saying what is going on.
- **Appearance:** five colors (Blue, Green, Lemon, Pink, Purple), each dark and light, plus Automatic. The logo takes the chosen color.
- **Languages:** Italian, English, French, Spanish and German, to choose at installation.
- **Menu and Settings:** a short menu and a Settings window. Right-click works on the whole bar, which sits at the same level as the Windows taskbar.
- **Updates:** the program no longer updates itself. It checks on GitHub, tells you and opens the new version's page.
- **PawnIO:** before installing it, a window explains what it is for and the choice is yours.
- **Security:** checked addresses, a cleaned error report that only you can send, unknown options rejected.
- **License:** the words "Personal license" or "Commercial license" in the overview and in Settings, with a window for pasting the license. It is only a label: every feature stays the same and no network is needed.
