
# Notes / To Do list for FezMultiplayerMod

- [ ] Add option for custom player skins/appearance

- [ ] Add extensible modding support 

- [ ] Add a way to detect player connection speed (both read and write speeds)

- [x] Note: using strings may result in DoS attacks; use WriteStringAsByteArrayWithLength and ReadStringAsByteArrayWithLength instead.

- [ ] Add more security measures, since rn it's possible to use an existing player's name, which could cause confusion. Could have the handshake when connecting check if the name is already taken.

- [ ] Add more data integrity checks, since rn if a bit gets corrupted it could disconnect the client.

- [ ] add ability to modify the "blocklist" and "allowlist" directly via the server's interface while the server is running

- [ ] add a "kick" option to the server's interface, to forcibly disconnect players

- [ ] add option to sync world save data & level states. this would also sync inventory 

- [ ] Theoretically the standalone FEZ multiplayer mod should also work on macOS and Linux. If it doesn't, it might be possible to rig the mod so instead of running the multiplayer client netcode directly within the FEZ game itself, it runs in a separate standalone executable, but then we'd have to figure out a way to get two applications potentially running different runtimes to communicate with each other in a way that does not impede the performance of either application.

- [ ] Do all the `//TODO` comments that are in the code (continuous task)


