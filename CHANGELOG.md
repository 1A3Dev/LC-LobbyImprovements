### 1.1.0

- Added support for LobbyCompatibility v1.5.0+
- Made SetLobbyJoinable not be triggered on game start and disconnect

### 1.0.9

- Added support for LobbyCompatibility v1.4.0+

### 1.0.8

- Added a config option to disable the lobby list sorting by player count
- Changed the lobby name input character limit to match vanilla (40 chars)

### 1.0.7

- Fixed the LAN lobby list erroring if you try joining a lobby at the same time as refreshing the list
- Fixed the chat message when a player joins not showing the custom username on LAN
- Added basic support for IPv6 in LAN
  - The toggle for IPv6 is in the config and will prevent players from joining via your IPv4 address if enabled
  - When trying to join a lobby via an IPv6 address, it must be surrounded by square brackets e.g. `[2001:db8:3333:4444:5555:6666:7777:8888]` or with a port specified `[2001:db8:3333:4444:5555:6666:7777:8888]:7777`
- Made LobbyCompatibility show the lobby mod list on LAN

### 1.0.6

- Temporary fix for an index out of bounds error that occurs when playing with over 4 players (this is fixed properly in the next MoreCompany update).

### 1.0.5

- Fixed the lobby list scrollbar being longer than the results
- Lobby Hosting UI Changes
  - Added an option to specify a server password
    - If you have a password set then the lobby will only be joinable by people who have this mod
  - Added an option for online mode to "Validate Steam Sessions"
    - If you have this enabled then the lobby will only be joinable by people who have this mod
    - This validates [steam session tickets](https://partner.steamgames.com/doc/features/auth) to prevent people from spoofing their steam id when joining the lobby
  - Added an option for lan mode to "Validate Clients"
    - If you have this enabled then the lobby will only be joinable by people who have this mod
    - This is only currently used to enable bans on lan lobbies
  - Added a warning when trying to host a lobby where the name contains a word that is blacklisted in vanilla
  - Replaced the public, friends-only & invite-only buttons with a dropdown
- Added ability to set your player name on LAN

### 1.0.4

- Fixed an issue with lobby codes that caused the 4th player to be unable to join

### 1.0.3

- Lobby List Changes
  - Fixed LAN servers not disappearing once the round starts
  - Fixed LAN lobby name not updating when changed with a mod such as LobbyControl
- Pause Menu Changes
  - Replaced the invite friends button on LAN with the button to copy the lobby ip
  - Added lobby name and player count to the pause menu ([Screenshot](https://i.gyazo.com/c1d9be655f692be2a898b31c1e7e332a.png))
  - Ban confirmation prompt changes ([Screenshot](https://i.gyazo.com/9a51859c98bfa506d1dc94f5fa017217.png))
    - Added an option to the ban prompt to kick the player
    - Added an option to the ban prompt to specify a custom reason

### 1.0.2

- Updated README.md

### 1.0.1

- LAN Lobby List Fixes
  - Made lobbies only show if they are on the same game version
  - Added support for specifying a lobby tag
  - Fixed challenge moon lobbies not showing the "challenge moon" text on the lobby list
  - Replaced the lobby list distance dropdown with a dropdown for sorting the list
- Steam Lobby List Fixes
  - Fixed the button to copy a lobby id not showing

### 1.0.0

- Initial Release
