# Windows transport

Covers `Mghdlc`, `WindowsNative`, `WindowsMicroGateDevice`, `MghdlcParams`, `MghdlcPort`, `MghdlcConstants`, and `WindowsMicroGatePorts` (all internal, namespace `BlueHeighliner.MicroGate.Windows`). Only `Mghdlc` and the members of `WindowsNative` touch the native library and are marked `[SupportedOSPlatform("windows")]`; everything else sees only `IWindowsNative` and runs on any operating system.

## P/Invoke surface

`Mghdlc` declares `LibraryImport` bindings for the base API of `mghdlc.dll` only: `MgslOpenByName`, `MgslClose`, `MgslSetParams`, `MgslSetIdleMode`, `MgslEnableTransmitter`, `MgslEnableReceiver`, `MgslWrite`, `MgslRead`, and `MgslEnumeratePorts`. The link-layer `MgslDl*` functions are deliberately not declared, so both platforms share one protocol engine. Calls use the stdcall convention. `WindowsNative` (behind `IWindowsNative`) wraps them without pointers or `ref` parameters, and `EnumeratePorts` returns exactly the reported number of entries as a managed array.

## Native struct layout

`MghdlcParams` mirrors `MGSL_PARAMS` from `Mghdlc.h` field for field, for `MgslSetParams`. `MghdlcPort` mirrors `MGSL_PORT` as returned by `MgslEnumeratePorts`, including a fixed 25-byte ASCII device name buffer (the one place a genuine `const` is required, as the fixed buffer size) and `GetDeviceName` to decode the null-terminated name. `MghdlcConstants` carries the subset of the header's `#define` values in use, as `static readonly` values.

## Port configuration

The same settings as the Linux transport, expressed through `MghdlcParams`: HDLC mode, the device options' encoding, CRC, and hardware address filter (null becomes `0xFF`, disabled), and external clock. Then the idle mode from the options is set and the receiver and transmitter are enabled. There is no line discipline or blocking flag step; `MgslRead` blocks by itself.

## Port enumeration

`WindowsMicroGatePorts` calls `MgslEnumeratePorts` into a `MghdlcPort` buffer sized for the header's maximum of 200 ports, decodes each entry's device name, and returns the names sorted ordinally. The names are what `MgslOpenByName` accepts.
