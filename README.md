# FezMultiplayerMod

Game modification for FEZ adding online multiplayer 

[![GitHub releases](https://img.shields.io/github/downloads/FEZModding/FezMultiplayerMod/total.svg?style=flat)](https://github.com/FEZModding/FezMultiplayerMod/releases)
[![Version](https://img.shields.io/github/v/release/FEZModding/FezMultiplayerMod.svg?style=flat)](https://github.com/FEZModding/FezMultiplayerMod/releases/latest)

<!--img src="thumbnail.png" width="50%" alt="Fez Multiplayer Mod in action" title="FezMultiplayerMod in action" /-->

## Overview 

This repository focuses on creating a game modification and multiplayer server for FEZ, enabling exciting online gameplay.

## Features

- Allows players to see each other in the game world
- Customizable player names

For a full list of current features, see [changelog.txt](/changelog.txt)

___Future Feature Considerations:___

- Text Chat
- Co-op Gameplay
- Competitive Modes
- Spectator Mode

## Installation

1. Install [HAT](https://github.com/Krzyhau/HAT) via the instructions there
2. Download `FezMultiplayerMod.zip` from https://github.com/FEZModding/FezMultiplayerMod/releases/latest and put it in the "Mods" directory.
3. Run `MONOMODDED_FEZ.exe` and enjoy!

After running the mod for the first time, a `FezMultiplayerMod.ini` configuration file will appear.
In the generated `FezMultiplayerMod.ini` configuration file, you will find settings to configure your multiplayer experience.
To connect to a different server, change the `MainEndpoint` setting to match the desired server's IP address and port.

To set up a multiplayer server yourself, see "[Setting up a Multiplayer Server](#setting-up-a-multiplayer-server)" below.

## Setting up a Multiplayer Server

<!-- Note: taken from https://terraria.fandom.com/wiki/Guide:Setting_up_a_Terraria_server#Preparing_your_Network -->

Before you begin setting up the server, consider these network changes that may be necessary if your server is in your home network.

- Assign the computer running the server a static IP address. It is unlikely, but if you don't do this then your router may reassign the computer's IP address while you are using the server which will interfere with your connection to it. To learn how to do this with your router, refer to your router's manual or search "how to set up static IP on <make and model of your router>".
- If anyone is connecting to the server from outside your local area network (aka "over the internet"), you will need to forward the port for the server. Additionally, make sure you have assigned the server computer a static IP on your router. See below on how to forward ports.

### Opening a port accessible through your public IP:

- To find your external IP, a simple website can display your public IP address without any unnecessary details, such as [whatsmyip.com](https://whatsmyip.com/) or [ipify](https://api.ipify.org/?format=raw) (has a lot more features at [ipify.org/](https://www.ipify.org/)).
- You will have to port forward (port 7777 by default, note that this is the same port as Terraria, Ark: Survival Evolved, Mordhau, Just Cause 2: Multiplayer mod, SCP: Secret Laboratory, and San Andreas Multiplayer) for FezMultiplayerMod. [(port forward guide)](https://www.pcworld.com/article/478406/how-to-forward-ports-on-your-router.html).

To start a server, download `FezMultiplayerServer.zip` from https://github.com/FEZModding/FezMultiplayerMod/releases/latest and run the contained `FezMultiplayerServer.exe`
After running FezMultiplayerServer.exe, a `FezMultiplayerServer.ini` configuration file will appear.
In the generated `FezMultiplayerServer.ini` configuration file, you will find options to change how the multiplayer server works.

## Building

1. Clone repository.
2. Copy all dependencies listed in `libs` directory and paste them into said directory.
3. Build it. idk. it should work.

## Contributing

Look for comments starting with the text `TODO` (whole word, case sensitive) (e.g., `//TODO add effects that mess with the controls`)

