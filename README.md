# AK Card Scanner for macOS

A macOS build of [**AK Card Scanner**](https://github.com/Gama-Tech/AK-Card-Scanner) by Gama-Tech. It sends barcode data to the "Arcade I/O HTTP Server" of the [**Play! PlayStation 2 emulator**](https://github.com/jpd002/Play-).

The upstream app is a Windows Forms program and runs only on Windows. This fork adds a native macOS version in [`macOS/`](macOS/). It rebuilds the window in [Avalonia](https://avaloniaui.net/) and keeps the same card lists, layout and behaviour. It runs natively on Apple Silicon, and on Intel Macs if built for `osx-x64`.

The original Windows project remains at the repository root, unchanged apart from excluding `macOS/` from its build. That keeps upstream changes easy to merge.


## Disclaimer
This application contains no copyrighted material. It contains lists of 8-character strings that are freely available online, and sends them to a port on a local server. The same can be done with Postman or `curl`.


## Building the Mac app
Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`brew install --cask dotnet-sdk`).

```bash
cd macOS
./build-app.sh            # Apple Silicon (osx-arm64)
./build-app.sh osx-x64    # Intel Macs
```

The script writes `macOS/dist/AK Card Scanner.app`, self-contained so no .NET runtime is needed to run it. On a Mac it is ad-hoc signed automatically. Drag it into `/Applications`.

If you build on another OS, or copy the app onto a Mac from elsewhere, sign it on the Mac before first launch. Apple Silicon will not run unsigned code:

```bash
xattr -cr "AK Card Scanner.app" && codesign --force --deep -s - "AK Card Scanner.app"
```

The app is not notarised. If Gatekeeper still blocks it, right-click the app, choose **Open**, then confirm.


## Enabling the Arcade I/O HTTP Server in Play!
1. In the emulator go to **Settings → General**.
2. Tick **Enable Arcade I/O HTTP Server**, then restart the emulator.

The default port is `9876`. If you change it in Play!, set the same port in the Card Scanner. Use **Test Port** to check the connection.


## Scanning a card
**Pre-loaded cards.** Select an Animal, Strong and Miracle card from the dropdowns and click **Scan Card** when the game prompts you. **Scan All** sends all three in sequence: Animal at once, Strong after 4 seconds (the game's animal animation blocks scans until then), and Miracle after 5 seconds.

**Custom cards and codes.** Type a code into the **Custom Barcode** box and press Return or click **Scan Card**.

**USB barcode scanners.** Click into the **Custom Barcode** box and scan. Scanners that emulate a keyboard type the code and press Return, which sends it. Only keyboard-emulating scanners work this way.


## Adding cards
The pre-loaded lists are the XML files in [`CardData/`](CardData/), shared by the Windows and Mac builds.

On the Mac, don't edit the copies inside the app bundle, as that breaks its signature. Instead copy the three files (`AnimalCards.xml`, `StrongCards.xml`, `MiracleCards.xml`) to:

```
~/Library/Application Support/AKCardScanner/CardData/
```

and edit them there. The app uses that folder in preference to its bundled lists when it exists. If you add cards missing from the lists, please submit them upstream so everyone gets them.


## Differences from the Windows version
- **No auto-updater.** Upstream's updater downloads and runs a Windows installer. On the Mac, pull this fork and rebuild.
- **Settings.** The server port is saved to `~/Library/Application Support/AKCardScanner/settings.json` instead of the Windows user settings store.
- **Card list override.** The Application Support `CardData` folder, described above.


## Windows
For Windows, use the upstream project and its [releases](https://github.com/Gama-Tech/AK-Card-Scanner/releases/). The Windows source here builds the same as upstream (`dotnet build AKCardScanner.csproj` on Windows).


## Other platforms
[**Card Collections**](https://github.com/Bebeq/Card-Collections) by Bebeq is a cross-platform web server application that displays, stores and sends barcode data, including scanning from a mobile device.


## AK Barcode Preservation Project
The codes come from the community-run AK barcode preservation project: [**Barcode List**](https://docs.google.com/spreadsheets/d/1CbBiaGXh8Kiq20VubqkGdwByh6mY6jTwFGv_XPnkyVM). To submit missing cards, join the [**Unofficial Community Discord Server**](https://discord.gg/kaiser-community-1006111395424768000).


## Licence
GPL-3.0, as upstream. See [`LICENSE`](LICENSE).
