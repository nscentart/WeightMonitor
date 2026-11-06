# Changelog

## 1.2 (06/11/2026)
Code base rewritten in English and cleaned up. The program does the same as 1.1 and looks the same.
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
