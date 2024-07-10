
# Notes / To Do list for FezMultiplayerMod

- [x] It'd be faster to send GUID as byte[], and parse it using `Guid(Int32, Int16, Int16, Byte, Byte, Byte, Byte, Byte, Byte, Byte, Byte)`

- [ ] Verify the player's remote IP is reachable before transmitting data; requires handshake. Just ping the remote endpoint or something. Note: if their IP endpoint changes, probably should refresh the handshake. Note: some clients may connect from multiple IP addresses at the same time; dunno how to handle that. Really we just don't want to be sending data to a local IP when it should be sent to a local IP on a different network. This should only really be a problem for serverless mode though.

- [ ] Add option for custom player skins; requires handshake.

- [ ] Add extensible modding support 

- [x] ~~Note: using strings may result in DoS attacks; someone could send a packet with really long strings. Consider sending the raw byte array instead, as then its length could be measured before it reads the data.~~ Maximum UDP datagram length is 65535 bytes, so it's not worth checking datagram size.

- [ ] Add more security measures, since rn it's possible to use an existing player's unique identifier, which would cause some problems. Could have the handshake when connecting check if the name is already taken, and use that in supplement to GUID

- [ ] add names (note: fonts do not support the same characters; consider sterilizing names to only chars in the range 0x20 to 0x7E, inclusive (subnote: all fonts except zuish also support 0x0A (Lf), 0x0D (Cr), 0xA0 (nbsp), 0xC7 (C with cedilla), 0xC9 (E with acute), 0xE7 (c with cedilla), and 0xE9 (e with acute), but newlines should not be possible to enter into the name field via the ini file, and nbsp should be drawn as normal spaces)) (note2: players can currently have the same name; to remedy this we must add more security measures)

- [x] display names above players heads

- [x] add IP verifying and banning functionality; these settings should be named "blocklist" and "allowlist", must not overwrite their values in the settings file, and the syntax will be like that which cPanel uses, but with entries separated by commas: https://docs.cpanel.net/cpanel/security/ip-blocker/

- [ ] add option for syncing inventory; requires handshake and unique identifiers for every collectible

- [ ] sync world state

- [ ] sync level state

- [ ] Note: TCP would work best for transmissions that require acknowledgement, but this
would likely require another port, and a way to send that port to other players. It might be best to do handshakes with UDP by resending the request of a response if it has not been acknowledged in a set amount of time, but there's probably not a system library that does this, so we might have to code it ourselves. Make something that has timers for each ack request, and creates an instance of a class that contains something like `volatile bool acked` when calling something like `udpAckWrapper.send(IPEndpoint endpoint, byte[] data, Action<byte[]> response)` (debating if this should be in MultiplayerServer or a new class that wraps the existing UdpClient; either way it would require defining a new PacketType and an associated Switch case in the ProcessDatagram method; we should also process the datagram using the existing BinaryReader defined in that method; the code would likely be much easier to read if it was in a separate wrapper class). Will need like a flag or enum something to differentiate between ack, seq, and fin types. Use like 10ms for retry timer.

- [ ] Theoretically the standalone FEZ multiplayer server executable should also work on macOS and Linux, and it might be possible to rig the mod so instead of running the server within the FEZ game itself, it runs the separate standalone executable, but then we'd have to figure out a way to get two applications potentially running different runtimes to communicate with each other in a way that does not impede the performance of either application.

- [x] MultiplayerServer's Dispose implementation needs work: https://learn.microsoft.com/en- us/dotnet/standard/garbage-collection/implementing-dispose#implement-the-dispose-pattern & https://learn.microsoft.com/en-us/dotnet/api/system.idisposable?view=net-7.0

- [x] Don't overwrite the settings file if it contains invalid endpoints

- [x] Fix sometimes `foreach (PlayerMetadata m in Players.Values)` can throw `KeyNotFoundException`, even though `Players` is a `ConcurrentDictionary<Guid, PlayerMetadata>`; Best fix would probably be slap the whole foreach loop inside of a try-catch and simply catch and ignore the KeyNotFoundException

- [ ] Do all the `//TODO` comments that are in the code (continuous task)


