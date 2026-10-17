SC2 Mod Manager
===============

!! IMPORTANT !!
---------------
Windows will block the mod manager executable from running the first time because it is not
verified by Windows. To allow them:

  1. Double-click SC2ModManager.exe
  2. Click "More info"
  3. Click "Run Anyway"


NOTE: This step is no longer necessary, but if the updater is not working, you can try this step
!! IMPORTANT - Required Step for Auto-Updates !!
-------------------------------------------------
The Mod Manager is self-updating, but it cannot run the updater unless you tell
Windows it is safe first. If you skip this step, you will have to repeat it
every time an update is downloaded.

To fix this once and for all:

  1. Double-click SC2MMUpdater.exe
  2. Click "More info"
  3. Click "Run Anyway"
  4. Immediately close the window that appears

That's it - the Mod Manager will now be able to update itself automatically
in the future.


===============================================================================

What's New in v1.6.5
--------------------
New Features
  - Dark Mode theme (Big thanks to Alphahex)
  - Search boxes on the installed and download lists for maps and generic
    mods
  - Downloaded maps and generic mods are enabled right away. Turn off
    "Automatically Enable New Downloaded Maps/Mods" if you'd rather they
    start out disabled
  - Download lists can be filtered to Downloaded or Not Downloaded, and
    every list shows how many items are in it
  - Enable All and Disable All only affect what's currently showing, so you
    can search or filter first
  - "Show only .scd files" checkbox on the installed generic mods page
  - Replays now show match length and file size
  - Replays can be searched by player name, and filtered by player count,
    AI or no AI, and faction. Your sort and filters are remembered
  - Launch, Delete, and Show in Explorer buttons on replay cards and on the
    replay details page
  - Replays that couldn't be read are marked on their card
  - The update banner shows the new version and what's new in it, and the
    Mod Manager tells you when an update finished or failed
  - Applying a preset tells you about any files it couldn't put back
  - News shows a Retry button if it couldn't load
  - Advanced Settings page, with an option for modders to keep luo.scd and
    toc.win.bdf when uninstalling
  - Your replays folder was added to Common File Locations
  - Settings, Advanced Settings, and Home scroll when the window is small,
    and toolbars wrap instead of getting cut off
  - The sidebar highlights the page you're on
  - Enter and Escape work in dialogs, and Enter saves a preset name or game
    path
  - Empty lists say why they're empty, and the scanner shows when it's
    scanning
  - Every Browse button opens to the folder it's asking about

Improvements
  - The replay list loads much faster. Replay details are cached, read in
    parallel, and only the newest 100 are shown until you click Show More
  - Restoring original game files or a backup now builds the new gamedata
    next to the old one and only swaps it in once it's complete. Cancelling
    or a crash partway through no longer leaves your game half wiped. This
    needs about 4 GB of free space while it runs
  - Downloads are saved to a temporary file first and only replace the old
    file once they've finished, so a failed download can't leave you with a
    broken or missing mod
  - Auto-save remembers the last replay it saved, so a game that finished
    while the Mod Manager was closed still gets saved next time it opens

Bug Fixes
  - Fixed a crash when a saved path had a mix of forward and back slashes
  - Fixed a lot of hotkey bugs. Hotkeys always keep a key (Escape puts a key
    back instead of clearing it), only valid game keys are allowed, and the
    mod manager only ever changes hotkeys inside your luo.scd, never your
    other mod changes
  - Fixed the updater getting stuck on an old version for anyone with
    special characters in their install path (like an accent in their
    Windows username). The Mod Manager now finishes the stuck update itself
  - Fixed maps and generic mods with the same file name overwriting each
    other
  - Fixed the current version showing as restorable in Previous Versions
  - Fixed a bunch of smaller bugs, mostly around downloads, backups, and
    uninstalling


===============================================================================

What is SC2 Mod Manager?
------------------------
SC2 Mod Manager is a community-built tool for Supreme Commander 2 that makes
it easy to download, install, manage, and organize mods and custom maps without
having to manually move files around. It keeps your mods completely separate
from your base game files, so you can always restore your original gamedata
with a single click.


Features
--------
Custom Maps
  Browse and download community-made maps directly from within the app, or
  manage maps you already have installed. Enable or disable maps individually
  without deleting them, and view details including the map image, player
  count, size, team style, tags, and description. Search by file name or map
  name, and filter and sort the list by name, date, player count, size,
  gameplay style, and more. The download list can also be filtered to show
  only maps you have or haven't downloaded yet. Newly downloaded maps are
  enabled right away unless you turn off "Automatically Enable New
  Downloaded Maps".

Generic Gamedata Mods
  Install and manage gameplay mods that modify core gamedata files. View
  mod details including a description and image. Enable or disable mods
  individually, search by file name or mod name, and sort the list by name
  or date. Like maps, newly downloaded mods are enabled right away unless you
  turn off "Automatically Enable New Downloaded Mods". "Show only .scd files"
  hides any other file types you imported, since the game only loads .scd
  files.

  Enable All and Disable All (for both maps and generic mods) only affect
  what's currently showing, so you can search or filter first and then
  enable just those.

Hotkey Editor (Maksing's Hotkey Mods)
  Download, install, and customize Maksing's hotkey mods directly from the
  app. The normal hotkey mod (luo.scd) adds and customizes unit ability
  hotkeys, and the build mode mod (BuildmodeHotkeys.scd) lets you rebind
  build mode keys for every faction. Edit any binding in a simple grid with
  duplicate detection, or switch to Advanced mode to edit the raw lua files
  directly. If you already installed or edited the hotkey mod manually, the
  mod manager picks up your existing files and edits those instead of
  overwriting them - only the hotkey entries you change are touched.

  NOTE: Installing the normal hotkey mod edits game files that are required
  to run the game (lua.scd is backed up and replaced). Uninstalling the
  hotkey mod from the Hotkeys screen restores your original files, and
  uninstalling the mod manager does the same.

Replays
  Browse your replays folder with details parsed from each replay,
  including the map, players, match length, and file size. The list loads
  fast even with thousands of replays - it shows the newest 100 first and
  you can click Show More for the rest. Search by replay name, map, or
  player name, and sort and filter by player count, AI or no AI, faction,
  and more. Your sort and filters are remembered for next time. Rename,
  delete, launch, or show a replay in Explorer right from the list or from
  its details page. Replays that couldn't be read are marked so you know
  why their details are missing. Turn on Auto-save replays to automatically
  copy each finished game's replay into your replays folder so it isn't
  overwritten by the next game.

Mod Presets
  Save your current mod setup as a named preset and restore it at any time.
  Compare two presets side by side to see exactly what files differ between
  them. On first install, an "Original Game Files" preset is automatically
  created, and if unrecognized files were already in your gamedata folder,
  an "Initial Gamedata State" preset is created as well. If a preset has
  files the mod manager can't put back anymore (for example a mod that was
  deleted), it tells you which ones. Presets never touch the hotkey mod
  files - those are managed only from the Hotkeys screen.

Backups
  Restore your game files back to their original unmodified state at any
  time by downloading the original gamedata directly from GitHub. Before
  restoring, you can choose to save a full backup of your current gamedata
  folder first. It appears on the Backups screen as "Backup Before
  Restoring Original Game Files" with the date it was made, and restoring
  it puts your gamedata back exactly how it was. Only one full backup
  exists at a time.

  Restores are built next to your gamedata folder first and only swapped in
  once they're complete, so cancelling or a crash partway through never
  leaves your game half wiped. This needs about 4 GB of free space while it
  runs.

Gamedata Scanner
  Automatically scans your gamedata folder for files that are not part of
  the original game. The scanner runs on startup and will alert you on the
  home screen if anything unexpected is found. Unrecognized files are split
  into two categories:

    - Found in Downloadable List: files that match a known mod available
      for download. You can import them with full metadata or delete them
      and re-download cleanly later.

    - Unrecognized Files: files that do not match any known mod. You can
      import them as Generic Gamedata Mods to track and manage them, or
      delete them.

Manual Import
  Drag and drop .scd or .zip files directly into the Mod Manager to install
  them. Any file type is accepted, though a warning will be shown for
  non-standard files.

News Feed
  Stay up to date with community news, tournament announcements, and Mod
  Manager updates from within the app. News items are individually
  collapsible so you can keep the home screen tidy. If the news can't load
  (no internet, GitHub down), there's a Retry button.

Faction Themes
  Choose from five color themes that apply instantly across the entire
  application:
    - Jaywix Standard
    - UEF
    - Cybran Nation
    - Aeon Illuminate
    - Dark Mode

  Your theme choice is saved and restored automatically on next launch.

Auto-Updater
  The Mod Manager checks for new versions on startup and can download and
  install updates automatically. The update banner shows the new version
  and what's new in it. After updating, the Mod Manager tells you which
  version you're now on, or tells you if the update failed. See the
  IMPORTANT note at the top of this file for the one-time setup step
  required. If an update ever gives you trouble, you can roll back to any
  previous release (1.6.0 or newer) from Settings -> Advanced Settings ->
  See Previous Versions.

File Locations
  Settings -> Open File Locations shows where the mod manager keeps its
  files, your game folder, and your replays folder, with buttons to open
  each one.


How to Use
----------
Downloading Mods
  Go to Manage Mods -> Download and choose a mod type. Check the boxes next
  to the mods you want and click Download. Downloaded mods are enabled right
  away unless "Automatically Enable" is turned off at the top of the page, in
  which case they go to your disabled list.

Enabling and Disabling Mods
  Go to Manage Mods -> Installed and choose a mod type. Check the boxes
  next to the mods you want to enable or disable, then click the
  corresponding button. When you are done, click Save to apply the changes
  to your game. Enabled mods are copied into your gamedata folder. Disabled
  mods are stored safely by the mod manager and do not affect your game.

Editing Hotkeys
  Go to Hotkeys and pick a tab. If the mod isn't installed yet, click
  Download & Install (you'll see a confirmation explaining what gets
  changed). Click any Key Binding cell, type your new key combination (use
  modifiers with a dash, e.g. Ctrl-B), then click Save & Apply to Game.
  Press Escape while editing a key to put it back to what it was. Restore
  Originals brings back the hotkeys from when the mod was first installed,
  and Uninstall removes the mod and restores your original game files.

Watching Replays
  Go to Replays and the mod manager will list everything in your replays
  folder. Click Launch on any replay to watch it, or open Details to see
  more about it. Close the game when you're done watching before playing
  multiplayer.

Using Presets
  Presets let you save and restore your current mod setup instantly. Go to
  Manage Mods -> Backups -> Presets, enter a name, and click Save (or press
  Enter). To restore a setup, select a preset and click Apply.

Restoring Original Game Files
  If something goes wrong with your game files, go to Manage Mods ->
  Backups -> Restore Original Game Files. This will download the original
  unmodified gamedata from GitHub and replace your current gamedata folder.
  Any maps, mods, and hotkey changes currently in gamedata will be removed,
  but your installed mods are stored separately by the mod manager and will
  not be deleted. Before it runs you can choose to create a full backup of
  your current gamedata first, which you can restore later from the same
  Backups screen.

Scanning for Unrecognized Files
  If you added mod files directly to your gamedata folder without using the
  mod manager, the scanner will detect them automatically on startup and
  show a banner on the home screen. You can also run the scan manually from
  Manage Mods -> Installed -> Scan Gamedata for Unrecognized Mods.

Uninstalling
  Go to Settings -> Uninstall. Any installed hotkey mods are uninstalled
  first so your original game files are restored, and you will be asked
  whether you want to keep your installed maps and mods in the game folder.
  Everything else the mod manager created is removed.

  For modders: Settings -> Advanced Settings has an option to keep luo.scd
  and toc.win.bdf in the game when uninstalling, if you maintain your own.


Installation
------------
  1. Download the latest SC2ModManager zip from the Releases page
  2. Extract both files (SC2ModManager.exe and SC2MMUpdater.exe) to any
     temporary folder
  3. Follow the IMPORTANT steps at the top of this file before running
  4. Run SC2ModManager.exe - the setup wizard will guide you through
     choosing an install location, selecting your faction theme, locating
     your Supreme Commander 2 installation, and completing the install


Requirements
------------
  - Windows 10 or later (64-bit)
  - Supreme Commander 2 installed via Steam

No additional software or .NET runtime installation is required. Everything
is bundled inside the executable.


===============================================================================

Credits
-------
Developed by OSS || Jaywix
Maksing's Hotkey Mods created by Maksing
Dark Mode theme by alphahex99
