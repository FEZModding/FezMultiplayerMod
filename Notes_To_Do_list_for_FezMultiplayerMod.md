
# Notes / To Do list for FezMultiplayerMod

- [x] It'd be faster to send GUID as byte[], and parse it using `Guid(Int32, Int16, Int16, Byte, Byte, Byte, Byte, Byte, Byte, Byte, Byte)`

- [ ] Add option for custom player skins/appearance

- [ ] Add extensible modding support 

- [ ] Add a way to detect player connection speed (both read and write speeds)

- [x] Note: using strings may result in DoS attacks; someone could send a packet with really long strings. Consider sending the raw byte array instead, as then its length could be measured before it reads the data.

- [ ] Add more security measures, since rn it's possible to use an existing player's name, which could cause confusion. Could have the handshake when connecting check if the name is already taken.

- [ ] Add more data integrity checks, since rn if a bit gets corrupted it could disconnect the client.

- [x] add names (note: fonts do not support the same characters; consider sterilizing names to only chars in the range 0x20 to 0x7E, inclusive (subnote: all fonts except zuish also support 0x0A (Lf), 0x0D (Cr), 0xA0 (nbsp), 0xC7 (C with cedilla), 0xC9 (E with acute), 0xE7 (c with cedilla), and 0xE9 (e with acute), but newlines should not be possible to enter into the name field via the ini file, and nbsp should be drawn as normal spaces)) (note2: players can currently have the same name; to remedy this we must add more security measures)

- [ ] add universal font for names? see also: support for custom colors in player names

- [x] display names above players heads

- [x] add IP verifying and banning functionality; these settings should be named "blocklist" and "allowlist", must not overwrite their values in the settings file, and the syntax will be like that which cPanel uses, but with entries separated by commas: https://docs.cpanel.net/cpanel/security/ip-blocker/

- [ ] add option to sync world save data & level states. this would also sync inventory 

- [x] Convert UDP netcode to TCP. TCP would work best for transmissions that require acknowledgement

- [ ] Theoretically the standalone FEZ multiplayer mod should also work on macOS and Linux. If it doesn't, it might be possible to rig the mod so instead of running the multiplayer client netcode directly within the FEZ game itself, it runs in a separate standalone executable, but then we'd have to figure out a way to get two applications potentially running different runtimes to communicate with each other in a way that does not impede the performance of either application.

- [x] Fix sometimes `foreach (PlayerMetadata m in Players.Values)` can throw `KeyNotFoundException`, even though `Players` is a `ConcurrentDictionary<Guid, PlayerMetadata>`; Best fix would probably be slap the whole foreach loop inside of a try-catch and simply catch and ignore the KeyNotFoundException

- [x] Add support for custom colors in player names; requires tokenizing the names. could also use the tokenized text to print characters that aren't supported by the current font, but are supported by other fonts.

- [ ] Do all the `//TODO` comments that are in the code (continuous task)


