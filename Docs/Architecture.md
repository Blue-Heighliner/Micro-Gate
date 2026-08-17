# Architecture

## Solution layout

| Project | Assembly | Purpose |
| --- | --- | --- |
| [`Core`](../Core) | `BlueHeighliner.MicroGate.Core` | The public API: connection/connector/port-source interfaces, the platform-agnostic HDLC engine, and the Windows/Linux native transports that implement the interfaces. Published as a NuGet package. |
| [`Sample`](../Sample) | `BlueHeighliner.MicroGate.Sample` | An Avalonia desktop app demonstrating `Core`: enumerate ports, connect, send/receive data. |
| [`Tests`](../Tests) | `BlueHeighliner.MicroGate.Tests` | xUnit tests for `Core`, focused on the HDLC engine (the only part testable without real hardware). |

All three share the root namespace `BlueHeighliner.MicroGate`; sub-namespaces (`.Hdlc`, `.Windows`, `.Linux`) group implementation detail. See [Components.md](Components.md) for what lives where, and [Protocols.md](Protocols.md) for the HDLC/ABM wire protocol itself.

## Layering

```
IMicroGateConnector / IMicroGatePortSource / IMicroGateConnection   (public API, BlueHeighliner.MicroGate)
                              |
              MicroGateConnector / MicroGatePortSource               (OS dispatch via OperatingSystem.IsWindows()/IsLinux())
                    /                                    \
  WindowsMicroGateConnection/-Ports              LinuxMicroGateConnection/-Ports
  (BlueHeighliner.MicroGate.Windows)               (BlueHeighliner.MicroGate.Linux)
                    \                                    /
                       HdlcStateMachine / HdlcFrame                  (BlueHeighliner.MicroGate.Hdlc — shared, platform-agnostic)
                    /                                    \
         mghdlc.dll base API (P/Invoke)              libc + SyncLink driver ioctls (P/Invoke)
```

The public interfaces (`Core/src/MicroGateConnection.cs`, `MicroGateConnector.cs`, `MicroGatePortSource.cs`) know nothing about HDLC or the operating system. `MicroGateConnector`/`MicroGatePortSource` are the only types that branch on OS, and only to pick which platform-specific implementation to construct. Everything below that point — how a frame's address/control bytes are built, how the asynchronous balanced mode (ABM) connection state machine behaves, how the poll/final bit is handled — lives once in `HdlcStateMachine` and is shared by both platforms, so their protocol behavior can't drift apart. Only the raw byte transport (how a frame's bytes physically reach the device) differs per platform, per [Protocols.md](Protocols.md#base-api-vs-link-layer).

## Why the OS branch happens where it does

MicroGate ships two structurally different SDKs:

- **Windows**: a single DLL, `mghdlc.dll`, exposing a synchronous base API (`MgslOpen`/`MgslRead`/`MgslWrite`/`MgslSetParams`/...) plus a separate link-layer API (`MgslDl*`) that implements its own ABM engine.
- **Linux**: no DLL at all — the SyncLink kernel driver exposes a standard tty device node, configured and driven through `ioctl(2)`/`read(2)`/`write(2)` from libc. There is no link-layer engine on Linux.

Since only Windows has a built-in ABM engine, using it there and something else on Linux would mean two independent, potentially inconsistent implementations of the same protocol. `Core` avoids that by never using `mghdlc.dll`'s `MgslDl*` functions — it only calls the *base* API (bit-level HDLC framing: flag detection, bit stuffing, CRC) on both platforms, and layers its own `HdlcStateMachine` on top of that raw framing on both platforms identically. This is why the OS branch is pushed as low as possible (into `MicroGateConnector`/`MicroGatePortSource`, choosing a transport) rather than high up (which would tempt each platform into its own protocol logic).

## Dependency injection

`Core` has no dependency on any DI container — it is a plain library. `Sample` is where a container is wired up (per [`AGENTS.md`](../AGENTS.md)'s DI convention), in `Sample/src/Program.cs`:

- `ConventionServiceCollectionExtensions.AddConventionServices` scans an assembly (here, `Core`) and registers every public `IThing` against a public, non-abstract class `Thing` in the same namespace, if one exists and implements it — so `IMicroGateConnector` resolves to `MicroGateConnector` and `IMicroGatePortSource` resolves to `MicroGatePortSource` with no explicit registration.
- `HdlcStationOptions` (the station address and the poll/final-bit-disable flag) is registered as a singleton instance, since `MicroGateConnector` takes it as a constructor dependency.
- `MainWindow` is registered explicitly (it isn't behind an interface, so the naming convention doesn't apply) and resolved by `App.axaml.cs` in place of `new MainWindow()`.

## Concurrency model

Each `IMicroGateConnection` implementation (`LinuxMicroGateConnection`, `WindowsMicroGateConnection`) owns one dedicated background task running a blocking read loop for the lifetime of the connection. That loop is the only place frames are received, parsed, and — via `HdlcStateMachine` — turned into either a delivered payload (`Received` event) or an automatic protocol response (e.g. `UA` for an inbound `SABM`, `RR`/`REJ` for an inbound `I`-frame), which the loop writes back itself. `Send` calls, and the loop's own automatic responses, both funnel through a single `WriteFrame` method guarded by a `Lock`, so frame writes never interleave. See [Components.md](Components.md#connection-lifecycle) for the exact sequencing.
