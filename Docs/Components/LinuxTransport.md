# Linux transport

Covers `LibC`, `LinuxNative`, `LinuxMicroGateDevice`, `SynclinkParams`, `SynclinkConstants`, and `LinuxMicroGatePorts` (all internal, namespace `BlueHeighliner.MicroGate.Linux`). The SyncLink kernel driver exposes a tty device driven through `open`, `read`, `write`, `ioctl`, and `fcntl` from libc; there is no DLL and no link-layer engine.

## P/Invoke surface

`LibC` declares only the calls needed, with PascalCase managed names bound to the libc symbols through `EntryPoint`. `fcntl` has get and set overloads, and `ioctl` has three overloads (`ref int`, `ref SynclinkParams`, and a plain `nint`) matching how the driver interprets each request's argument. `LinuxNative` (behind `ILinuxNative`) turns those into semantic operations such as `EnableReceiver` and `SetParams`, choosing the request code and argument shape, and `LinuxMicroGateDevice` builds the frame transport on it: a write that is not fully accepted throws `IOException`, and each successful write is followed by `tcdrain` so the frame has left the device.

## Native struct layout

`SynclinkParams` mirrors `struct _MGSL_PARAMS` from `synclink.h` as laid out on 64-bit Linux, where `unsigned long` is 8 bytes (`nuint`) rather than the 4 bytes of the Windows `ULONG`. This is the one place the two platforms' native structs differ in shape and not just name.

## Request codes

`SynclinkConstants` holds the mode, encoding, CRC, and enable values (numerically identical to the Windows header's, since both SDKs share a driver lineage), the `N_HDLC` line discipline number, and the `fcntl` and file flag values. The `MGSL_IOC*` request codes are computed at class initialization by replicating the kernel's `_IO` and `_IOW` macros, with `Marshal.SizeOf<SynclinkParams>()` feeding the size field, so they cannot drift from the marshaled struct size. Values are `static readonly` rather than `const` so they are never baked into other assemblies.

## Port configuration

A port name that is not a rooted path is resolved under `/dev`, so `ttySLG0` and `/dev/ttySLG0` are equivalent.

Opening uses `O_NONBLOCK` only so the open does not wait on DCD; the flag is cleared afterwards so reads and writes block, matching the vendor's Linux sample. The sequence is: select the `N_HDLC` line discipline (`TIOCSETD`), apply `MGSL_IOCSPARAMS`, set the idle pattern from the options, enable receiver and transmitter, then clear `O_NONBLOCK`.

The parameters are: HDLC mode; the encoding, CRC, idle pattern, and hardware address filter from `MicroGateConnectionOptions` (the enum values are cast directly to the driver's numeric constants, and a null address filter becomes `0xFF`, disabled, because address filtering otherwise happens in `HdlcStateMachine`); and external clock through the TXC and RXC pins (`Flags = 0`, the cable supplies the clock). Frames failing the CRC are discarded by the driver and never reach `read`.

## Port enumeration

`LinuxMicroGatePorts` (whose device and sysfs folders are constructor parameters, defaulting to `/dev` and `/sys/class/tty`) lists `/dev/ttySLG*` (PCI and PCIe adapters, always MicroGate) and those `/dev/ttyUSB*` devices whose USB `idVendor` is `2618`, MicroGate's vendor ID. The vendor is found by resolving each tty's `/sys/class/tty/<name>/device` symlink and walking up ancestor directories until an `idVendor` file appears, stopping at `/` or `/sys`. Names are returned sorted ordinally.
