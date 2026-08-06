# AK Card Scanner
> This application allows you to send barcode data to the "Arcade I/O HTTP Server" of the [**Play! - PlayStation2 emulator**](<https://github.com/jpd002/Play->).<br>
> ![image](https://github.com/Gama-Tech/AK-Card-Scanner-Releases/assets/80988054/a9e996ad-2afa-4bca-9efc-7b9c47cb9af3)


## Disclaimer:
> This application contains no copyrighted material!<br>
> It simply contains lists of 8-character strings of text which are freely available online.<br>
> This application simply sends strings of text to a port on a local server, which can be accomplished using many other applications such as PostMan or even via command-prompt using Curl.


## Installation:
> 1. Download the latest version of AK Card Scanner from the [**releases page**](<https://github.com/Gama-Tech/AK-Card-Scanner/releases/>).
> 
> 2. Run the installer.
> 
> 3. Double-click `AKCardScanner.exe` to start the application.


## Enabling the Arcade I/O HTTP Server in Play!:
> 1. In the emulator go to Options -> Settings -> General
> 
> 2. Check the "Enable Arcade I/O HTTP Server" box, then restart the emulator.<br>
> <sub>**NOTE:** *If you change the Arcade I/O HTTP Server's port, you'll need to set the same port in the Card Scanner application.*</sub>
> 
> 3. Choose "Allow Access" if you're presented with a popup from Windows Defender Firewall.<br>



## Scanning a card (Windows) 
> **Pre-loaded Cards<br>**
> Select an Animal, Strong & Miracle card from the dropdown boxes and click `Scan Card` when the game prompts you to.<br>
> You can click `Scan All` to send all 3 selected cards at once.<br>
> The application will display a progress bar as it scans each card in sequence.<br><br>
> 
> **Custom Cards/Codes**<br>
> You can manually enter a code in the `Custom Barcode` text box and click `Scan Card`.<br><br>
>
> **Adding Cards**<br>
> The Pre-loaded Cards lists are stored in `.XML` format in the  `AKCardScanner\CardData` folder.<br>You can add your own cards here for them to appear in the application.<br>
> <sub>**NOTE:** *If adding cards that are missing from the Pre-loaded Cards lists, please submit a pull request so they can be included in the next update!*</sub>
<br><br>
> 
> **USB Barcode Scanners<br>**
> If you have a USB barcode scanner you can use this to scan cards you physically own.<br>
> Simply click inside the `Custom Barcode` text box then scan your card.<br>
> Your barcode scanner should enter the code for you and send it automatically.<br>
> <sub>**NOTE:** *The above method will only work for USB barcode scanners that emulate keyboard input.*</sub>


## Other Platforms:
> If you're using a non-Windows platform I highly suggest trying [**Card Collections**](<https://github.com/Bebeq/Card-Collections>) by [**Bebeq**](<https://github.com/Bebeq>).<br>
> Card Collections allows you to display, store, and send barcode data via a cross-platform web server application.<br>
> You can even scan barcodes directly using a mobile device.


## Contributing:
> If you would like to contribute to this project then feel free to create a fork!<br>
> Pull requests are welcomed under the condition that they contain no copyrighted material.
