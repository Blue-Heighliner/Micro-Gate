# Windows transport

Covers `Mghdlc`, `WindowsNative`, `WindowsMicroGateDevice`, `MghdlcParams`, `MghdlcPort`, `MghdlcConstants`, and `WindowsMicroGatePorts` (all internal, namespace `BlueHeighliner.MicroGate.Windows`). Only `Mghdlc` and the members of `WindowsNative` touch the native library and are marked `[SupportedOSPlatform("windows")]`; everything else sees only `IWindowsNative` and runs on any operating system.

## P/Invoke surface

`Mghdlc` declares `LibraryImport` bindings for the base API of `mghdlc.dll` only: `MgslOpenByName`, `MgslClose`, `MgslSetParams`, `MgslSetIdleMode`, `MgslEnableTransmitter`, `MgslEnableReceiver`, `MgslCancelReceive`, `MgslCancelTransmit`, `MgslWrite`, `MgslRead`, and `MgslEnumeratePorts`. The link-layer `MgslDl*` functions are deliberately not declared, so both platforms share one protocol engine. Calls use the stdcall convention. `WindowsNative` (behind `IWindowsNative`) wraps them without pointers or `ref` parameters, and `EnumeratePorts` returns exactly the reported number of entries as a managed array.

## Native struct layout

`MghdlcParams` mirrors `MGSL_PARAMS` from `Mghdlc.h` field for field, for `MgslSetParams`. `MghdlcPort` mirrors `MGSL_PORT` as returned by `MgslEnumeratePorts`, including a fixed 25-byte ASCII device name buffer (the one place a genuine `const` is required, as the fixed buffer size) and `GetDeviceName` to decode the null-terminated name. `MghdlcConstants` carries the subset of the header's `#define` values in use, as `static readonly` values.

## Port configuration

The same settings as the Linux transport, expressed through `MghdlcParams`: HDLC mode, the device options' encoding, CRC, and hardware address filter (null becomes `0xFF`, disabled), and external clock. Then the idle mode from the options is set and the receiver and transmitter are enabled (the receiver starts disabled). There is no line discipline or blocking flag step; `MgslRead` blocks by itself and returns one frame per call. Every status code is checked, and a failure throws `IOException` naming the step. Disposal disables the receiver and also calls `MgslCancelReceive`, the call the driver documentation gives for cancelling a blocked read from another thread, since disabling the receiver alone is not documented to do so. `MgslCancelReceive` only cancels a read that is already blocked, so the device also sets a flag first and returns from any later read without calling the driver, closing the window where the receive loop starts a new read just after the cancellation. Disabling the transmitter likewise calls `MgslCancelTransmit`, the documented way to abort a blocked `MgslWrite`, which disposal uses when the disconnect write does not complete.

## Port enumeration

`WindowsMicroGatePorts` decodes each entry's device name and returns the names sorted ordinally; they are what `MgslOpenByName` accepts. `WindowsNative` follows the documented two-call pattern: `MgslEnumeratePorts` with no buffer returns the port count, a buffer of exactly that many entries is allocated, and the second call fills it. Both statuses are checked and a failure throws `IOException`, so a driver error is reported instead of being mistaken for an empty or garbage list.
