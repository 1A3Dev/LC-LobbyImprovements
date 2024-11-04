# LobbyImprovements

[![Latest Version](https://img.shields.io/thunderstore/v/Dev1A3/LobbyImprovements?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Dev1A3/LobbyImprovements)
[![Total Downloads](https://img.shields.io/thunderstore/dt/Dev1A3/LobbyImprovements?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/lethal-company/p/Dev1A3/LobbyImprovements)
[![Discord](https://img.shields.io/discord/646323142737788928?style=for-the-badge&logo=discord&logoColor=white&label=Discord)](https://discord.gg/CKqVFPRtKp)
[![Ko-fi](https://img.shields.io/badge/Donate-F16061.svg?style=for-the-badge&logo=ko-fi&logoColor=white&label=Ko-fi)](https://ko-fi.com/K3K8SOM8U)

> If you are using MoreCompany alongside this mod please ensure you are using MoreCompany v1.11.0 or newer!

### Information

This mod adds various features relating to lobbies:

- LAN Improvements
  - Added ability to change the port in-case the default (7777) isn't available
  - Added a lobby list ([Screenshot](https://i.gyazo.com/a84d8057da4d3b48856a66b073df7c97.png))
    - You can directly connect to an IP via the tag input above the lobby list (the port is optional and can be specified after a colon for example: 127.0.0.1:7777)
    - This uses UDP to discover other lobbies on your local network
    - Support for challenge moon and specifying a lobby tag
    - This is compatible with late joining mods such as LobbyControl
  - Added ability to set your player name
    - Only people who have this mod will be able to see the usernames
  - Added an option to "Validate Clients"
    - This is currently only used to ban players
  - Added basic support for IPv6 in LAN
    - The toggle for IPv6 is in the config and will prevent players from joining via your IPv4 address if enabled
    - When trying to join a lobby via an IPv6 address, it must be surrounded by square brackets e.g. `[2001:db8:3333:4444:5555:6666:7777:8888]` or with a port specified `[2001:db8:3333:4444:5555:6666:7777:8888]:7777`
- Lobby Codes/IDs
  - You can copy your current lobby's id via the option on the pause menu
  - You can copy a lobby id from the lobby list using the "Copy ID" button
  - You can join a lobby via the id by putting it into the tag input above the lobby list
- Lobby Hosting Changes
  - Added a warning when trying to host a lobby where the name contains a word that is blacklisted in vanilla
  - Added the ability to restrict joining a lobby with a password
  - Made lobbies be automatically sorted by the player count
- Lobby Name Filter Customisation
  - You can modify the blocked terms
- Steam Improvements
  - Added option to set the lobby as invite only
  - "Recently Played With" Integration ([Screenshot](https://i.gyazo.com/02fc2fce3599a737a54376f2fa22f49d.png))
    - This enables the steam "recently played with" integration which allows you to view the list of people you have recently played with. You can access the list by opening steam then clicking View > Players. ([Screenshot](https://i.imgur.com/Mzdrgjt.png))
  - Added an option to "Validate Steam Sessions"
    - This validates [steam session tickets](https://partner.steamgames.com/doc/features/auth) to prevent people from spoofing their steam id when joining the lobby
- Lobby name and player count in the pause menu ([Screenshot](https://i.gyazo.com/c1d9be655f692be2a898b31c1e7e332a.png))
- Ban Confirmation Prompt Changes ([Screenshot](https://i.gyazo.com/9a51859c98bfa506d1dc94f5fa017217.png))
  - Added an option to the ban prompt to kick the player
  - Added an option to the ban prompt to specify a custom reason

### Vanilla Compatibility

Using some features will make the lobby will only be joinable by people who have this mod:

- Password Protection
- Steam: Validate Steam Sessions
- LAN: Validate Clients

### Support

You can get support in any the following places:

- The [thread](https://discord.com/channels/1168655651455639582/1282200504318820374) in the [LC Modding Discord Server](https://discord.gg/lcmod)
- [GitHub Issues](https://github.com/1A3Dev/LC-LobbyImprovements/issues)
- [My Discord Server](https://discord.gg/CKqVFPRtKp)

### Compatibility

- Supported Game Versions:
  - v64+
- Works Well With:
  - [LethalFixes](https://thunderstore.io/c/lethal-company/p/Dev1A3/LethalFixes/)
  - [MoreCompany](https://thunderstore.io/c/lethal-company/p/notnotnotswipez/MoreCompany/)
  - [LethalRichPresence](https://thunderstore.io/c/lethal-company/p/mrov/LethalRichPresence/)
- Not Compatible With:
  - N/A
