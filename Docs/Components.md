# Components

See [Architecture.md](Architecture.md) for how these pieces fit together, and [Protocols.md](Protocols.md) for the wire format they implement.

## Public API (`Core/src/*.cs`)

| Type | File | Role |
| --- | --- | --- |
| `IMicroGateConnection` | `MicroGateConnection.cs` | An open ABM connection. `IsConnected`; `Disconnected` and `Received` events (the latter delivers `IMemoryOwner<byte>` — the subscriber owns and must dispose it, to support pooled memory); `Send(ReadOnlyMemory<byte>)` and `Send(IMemoryOwner<byte>)` overloads; both `IDisposable` and `IAsyncDisposable`. |
| `IMicroGateConnector` / `MicroGateConnector` | `MicroGateConnector.cs` | `Connect(portName, cancellationToken)` opens a connection. `MicroGateConnector`'s constructor takes the `HdlcStationOptions` applied to every connection it opens, and its `Connect` dispatches to `WindowsMicroGateConnection.Connect` or `LinuxMicroGateConnection.Connect` based on `OperatingSystem.IsWindows()`/`IsLinux()`. |
| `IMicroGatePortSource` / `MicroGatePortSource` | `MicroGatePortSource.cs` | `GetPorts(cancellationToken)` lists available device names. Dispatches to `WindowsMicroGatePorts`/`LinuxMicroGatePorts` the same way. |
| `LimitedMemoryOwner` | `LimitedMemoryOwner.cs` | Wraps an `IMemoryOwner<byte>` rented from `MemoryPool<byte>.Shared` so `Memory` only exposes the first *N* valid bytes instead of the pool's (larger) rented buffer, while still forwarding `Dispose()` to the real owner. Used by both connection implementations when raising `Received`. |

## HDLC engine (`Core/src/Hdlc/`, namespace `BlueHeighliner.MicroGate.Hdlc`)

Platform-agnostic and has no P/Invoke or I/O of its own — it only turns bytes into `HdlcFrame`s and back, and tracks ABM connection state. Internal (implementation detail), except `HdlcStationOptions` which is public because it's a constructor parameter of the public `MicroGateConnector`.

| Type | File | Role |
| --- | --- | --- |
| `HdlcFrameKind` | `HdlcFrameKind.cs` | The 9 frame kinds this implementation understands: `Information`, the supervisory frames (`ReceiveReady`, `ReceiveNotReady`, `Reject`), and the unnumbered frames used for ABM connection management (`SetAsynchronousBalancedMode`, `Disconnect`, `UnnumberedAcknowledge`, `DisconnectedMode`, `FrameReject`). |
| `HdlcConnectionState` | `HdlcConnectionState.cs` | `Disconnected` / `Connecting` / `Connected` / `Disconnecting`. |
| `HdlcStationOptions` | `HdlcStationOptions.cs` | `Address` (the HDLC address byte this station sends and expects) and `DisablePollFinalBit`. |
| `HdlcFrame` | `HdlcFrame.cs` | A parsed address+control+information field. `Parse(ReadOnlySpan<byte>)` decodes raw bytes; `ToArray()` encodes back to raw bytes. All control-byte bit-twiddling lives here — see [Protocols.md](Protocols.md#control-field-encoding). |
| `HdlcFrameException` | `HdlcFrameException.cs` | Thrown by `HdlcFrame.Parse` for frames too short to contain an address/control field, or whose control byte doesn't decode to a recognized kind. |
| `HdlcReceiveResult` | `HdlcReceiveResult.cs` | What `HdlcStateMachine.Receive` produces: the resulting `State`, an optional delivered `Payload`, and an optional `Response` (raw bytes the caller must transmit back, e.g. a `UA` or `RR`). |
| `IHdlcStateMachine` / `HdlcStateMachine` | `HdlcStateMachine.cs` | The ABM engine. `CreateConnect()`/`CreateDisconnect()`/`CreateInformation(payload)` produce outbound frame bytes and advance local state; `Receive(data)` parses an inbound frame, advances state, and returns a `HdlcReceiveResult`. See [Protocols.md](Protocols.md#connection-state-machine) for the state transitions it implements. |

## Windows transport (`Core/src/Windows/`, namespace `BlueHeighliner.MicroGate.Windows`)

All types here are `[SupportedOSPlatform("windows")]` and `internal`.

| Type | File | Role |
| --- | --- | --- |
| `Mghdlc` | `Mghdlc.cs` | `LibraryImport` P/Invoke declarations for `mghdlc.dll`'s **base API only** (`MgslOpenByName`, `MgslClose`, `MgslSetParams`, `MgslSetIdleMode`, `MgslEnableTransmitter`, `MgslEnableReceiver`, `MgslWrite`, `MgslRead`, `MgslEnumeratePorts`). The link-layer (`MgslDl*`) functions are deliberately not declared here — see [Architecture.md](Architecture.md#why-the-os-branch-happens-where-it-does). |
| `MghdlcParams` | `MghdlcParams.cs` | Mirrors the native `MGSL_PARAMS` struct (`Mghdlc.h`) field-for-field, for `MgslSetParams`. |
| `MghdlcPort` | `MghdlcPort.cs` | Mirrors the native `MGSL_PORT` struct returned by `MgslEnumeratePorts`, including the fixed 25-byte ASCII device name buffer and a `GetDeviceName()` helper to decode it. |
| `MghdlcConstants` | `MghdlcConstants.cs` | The subset of `Mghdlc.h`'s `#define` constants actually used (mode/encoding/CRC/enable values, `MgslOpenByName`'s success code). |
| `WindowsMicroGateConnection` | `WindowsMicroGateConnection.cs` | `IMicroGateConnection` implementation. See [Connection lifecycle](#connection-lifecycle) below — identical structure to `LinuxMicroGateConnection`, differing only in which native calls move bytes. |
| `WindowsMicroGatePorts` | `WindowsMicroGatePorts.cs` | `GetPorts()` calls `MgslEnumeratePorts` into a `MghdlcPort[]` buffer and decodes each entry's device name. |

## Linux transport (`Core/src/Linux/`, namespace `BlueHeighliner.MicroGate.Linux`)

All types here are `internal`.

| Type | File | Role |
| --- | --- | --- |
| `LibC` | `LibC.cs` | `LibraryImport` P/Invoke declarations for the libc calls needed to drive a SyncLink tty device directly: `open`, `close`, `read`, `write`, `fcntl` (two overloads, get/set flags), `ioctl` (three overloads — `ref int`, `ref SynclinkParams`, and a plain `nint` — matching how the driver interprets each request's argument), and `tcdrain`. |
| `SynclinkParams` | `SynclinkParams.cs` | Mirrors the native `struct _MGSL_PARAMS` from `synclink.h` **as laid out on 64-bit Linux**, where `unsigned long` is 8 bytes (`nuint`), not 4 like Windows' `ULONG` — this is the one place the two platforms' native structs genuinely differ in shape, not just in name. |
| `SynclinkConstants` | `SynclinkConstants.cs` | Mode/encoding/CRC/enable constants (numerically identical to the Windows header's, since both SDKs share the same driver lineage), plus the `N_HDLC` line discipline number, `fcntl`/file-flag constants, and the `MGSL_IOC*` ioctl request codes — computed at class-init time by replicating the Linux `_IOW`/`_IO` macros (`Marshal.SizeOf<SynclinkParams>()` feeds the size field), rather than hardcoded, so they can't drift from the actual marshaled struct size. |
| `LinuxMicroGateConnection` | `LinuxMicroGateConnection.cs` | `IMicroGateConnection` implementation. See [Connection lifecycle](#connection-lifecycle) below. |
| `LinuxMicroGatePorts` | `LinuxMicroGatePorts.cs` | `GetPorts()` lists `/dev/ttySLG*` (PCI/PCIe adapters — always MicroGate) and `/dev/ttyUSB*` filtered to those whose USB `idVendor` sysfs attribute is `2618` (MicroGate's vendor ID), found by resolving each tty's `/sys/class/tty/<name>/device` symlink and walking up ancestor directories until an `idVendor` file is found. |

## Connection lifecycle

`LinuxMicroGateConnection` and `WindowsMicroGateConnection` are structured identically (member-for-member); only the native calls inside `ConfigurePort`, `ReceiveLoop`, and `WriteFrame` differ.

1. **`Connect(portName, options, cancellationToken)`** (static factory, not a public constructor — connections are only ever handed out already-open): opens the device, configures it for HDLC mode (`ConfigurePort`), constructs an `HdlcStateMachine`, starts the background `ReceiveLoop` task, then sends a `SABM` frame (`stateMachine.CreateConnect()`) and awaits a `TaskCompletionSource` that `ReceiveLoop` completes once the state machine reports `Connected` (i.e. once the peer's `UA` — or an inbound `SABM`, in the two-peers-connect-simultaneously case — has been processed). Any failure before that disposes the partially-constructed connection and rethrows.
2. **`ReceiveLoop`** (runs for the connection's lifetime on a dedicated `Task.Run` thread): blocking-reads one raw frame at a time, feeds it to `stateMachine.Receive`, and reacts to the `HdlcReceiveResult`: writes any `Response` frame back immediately (auto-`UA`/`RR`/`REJ`), rents pooled memory and raises `Received` for any delivered `Payload`, and raises `Disconnected` exactly once when state drops out of `Connected`. The loop itself exits when the blocking read returns ≤ 0 bytes, which is triggered on `Dispose`/`DisposeAsync` by disabling the receiver (`MGSL_IOCRXENABLE`/`MgslEnableReceiver` with a disable flag) — the same technique the vendor SDK's own sample code uses to cancel a blocked read.
3. **`Send`** overloads build an `I`-frame via `stateMachine.CreateInformation` and write it through the shared, lock-guarded `WriteFrame`; the `IMemoryOwner<byte>` overload disposes its argument once the write completes.
4. **`Dispose`/`DisposeAsync`**: best-effort sends a `DISC` frame if still connected, disables the receiver to unblock `ReceiveLoop`, awaits it, then closes the device handle/file descriptor.

## Sample app (`Sample/src/`)

| Type | File | Role |
| --- | --- | --- |
| `Program` | `Program.cs` | Composition root: builds the `ServiceCollection`, calls `AddConventionServices` against `Core`'s assembly, registers `HdlcStationOptions` and `MainWindow`, builds the `ServiceProvider` into `App.Services`, then starts Avalonia. |
| `ConventionServiceCollectionExtensions` | `ConventionServiceCollectionExtensions.cs` | The `IThing` → `Thing` naming-convention auto-registration described in [Architecture.md](Architecture.md#dependency-injection). |
| `App` | `App.axaml`, `App.axaml.cs` | Avalonia application entry point; resolves `MainWindow` from `Services` instead of `new`-ing it. |
| `MainWindow` | `MainWindow.axaml`, `MainWindow.axaml.cs` | The demo UI: refresh/select a port, connect/disconnect, send a text message, and view a timestamped log of connects, disconnects, sends, and received messages. Takes `IMicroGatePortSource`/`IMicroGateConnector` as constructor dependencies. |

## Tests (`Tests/src/`)

`HdlcFrameTests.cs` and `HdlcStateMachineTests.cs` cover the HDLC engine — the only layer testable without real MicroGate hardware. `Core.csproj` grants `Tests` access to `Core`'s `internal` types via `InternalsVisibleTo`. Notably, `[Theory]`/`[InlineData]` isn't used for `HdlcFrameKind`-parameterized cases, because `HdlcFrameKind` is `internal` and xUnit requires test classes/methods to be `public` — a `public` method can't expose an `internal` type as a parameter (`CS0051`), so those cases are separate `[Fact]` methods delegating to a private helper instead.
