# AK Card Scanner for macOS

A macOS build of [**AK Card Scanner**](https://github.com/Gama-Tech/AK-Card-Scanner) by Gama-Tech. It sends barcode data to the "Arcade I/O HTTP Server" of the [**Play! PlayStation 2 emulator**](https://github.com/jpd002/Play-).

The upstream app is a Windows Forms program and runs only on Windows. This fork adds a native macOS version in [`macOS/`](macOS/), built with [Avalonia](https://avaloniaui.net/), with a card-fan interface styled after Animal Kaiser+. It runs natively on Apple Silicon, and on Intel Macs if built for `osx-x64`.

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


## Using the app
The Mac version picks cards from a fan rather than from lists.

- **Choose a pile:** Animal, Strong or Miracle, from the tabs or with the `1`, `2` and `3` keys.
- **Fan through it:** `←` and `→`, the arrow buttons, or click a card at the side. The fan wraps round at the ends.
- **Select a card:** `Space`, `Return`, a click on the centre card, or **Select this card**. Selecting from the same pile again swaps the card; selecting the same card again deselects it.
- **Your hand:** the three slots at the bottom show what is picked. Pick one, two or all three, and clear a slot with its ×.
- **Scan cards:** sends the picked cards to Play!, Animal first. It waits 4 seconds after the Animal card (the game's animation blocks scans until then) and 1 second between later cards.
- **Game version:** the dropdown limits the piles to cards released up to that version. Only cards whose English version is known appear when a version is chosen; *All versions* shows everything.
- **Search:** filters the current pile by name or card number.
- **Enter a barcode:** the barcode button in the top bar sends a code typed by hand, or from a USB barcode scanner that types like a keyboard.
- **Play! connection:** the *Play! · port* button sets the port and tests the connection. The dot is green when Play! answered, red when it did not.

The window keeps its shape and scales when resized.

## Card data
Cards come from `macOS/CardData/cards.json`, one entry per barcode with the card's number, name, rarity, stats and English version where known. It was merged from the original app's lists and the community barcode spreadsheet at [zybuilds/animal-kaiser-playable](https://github.com/zybuilds/animal-kaiser-playable).

To use your own list without rebuilding, put a `cards.json` in the same format at `~/Library/Application Support/AKCardScanner/cards.json`.

### Card art
The app draws card frames from the card data, and ships no card images. To show real card art, put images named by card number (for example `A007E.jpg` or `S124E.png`) in:

```
~/Library/Application Support/AKCardScanner/CardArt/
```

## Differences from the Windows version
- **No auto-updater.** Upstream's updater downloads and runs a Windows installer. On the Mac, pull this fork and rebuild.
- **Settings.** The server port and game version are saved to `~/Library/Application Support/AKCardScanner/settings.json` instead of the Windows user settings store.
- **Card picker.** A fan of cards with stats replaces the dropdown lists, as described above.
- **Card list.** `cards.json` replaces the XML lists, and can be overridden from Application Support.


## Windows
For Windows, use the upstream project and its [releases](https://github.com/Gama-Tech/AK-Card-Scanner/releases/). The Windows source here builds the same as upstream (`dotnet build AKCardScanner.csproj` on Windows).


## Other platforms
[**Card Collections**](https://github.com/Bebeq/Card-Collections) by Bebeq is a cross-platform web server application that displays, stores and sends barcode data, including scanning from a mobile device.


## AK Barcode Preservation Project
The codes come from the community-run AK barcode preservation project: [**Barcode List**](https://docs.google.com/spreadsheets/d/1CbBiaGXh8Kiq20VubqkGdwByh6mY6jTwFGv_XPnkyVM). To submit missing cards, join the [**Unofficial Community Discord Server**](https://discord.gg/kaiser-community-1006111395424768000).


## Licence
GPL-3.0, as upstream. See [`LICENSE`](LICENSE). The bundled Hind Siliguri font is under the SIL Open Font License ([`macOS/Assets/Fonts/OFL.txt`](macOS/Assets/Fonts/OFL.txt)).
