# Architecture

This document explains the high-level design decisions behind the library's implementation: *why* it's built the way it is, not the class-by-class mechanics of *how*.

## One protocol engine over raw framing, on both platforms

MicroGate ships two structurally different SDKs. On Windows, `mghdlc.dll` exposes a base API (bit-level HDLC framing) and a separate link-layer API (`MgslDl*`) with its own asynchronous balanced mode (ABM) engine. On Linux, the SyncLink kernel driver exposes only a tty device with the base API's equivalent; there is no link-layer engine at all.

Using the Windows link layer there and something else on Linux would mean two independent, potentially inconsistent implementations of the same protocol. The library instead never touches `MgslDl*`: both platforms use only the base API (flag detection, bit stuffing, CRC) and share one userspace ABM implementation, `HdlcStateMachine`, so protocol behavior cannot drift between them. The accepted trade-off is that features of the Windows link layer (automatic retries, timers) are not available; the library implements only what a directly cabled link needs.

## The operating system branch sits as low as possible

`MicroGateConnector` and `MicroGatePortSource` are the only types that branch on the operating system, and only to choose which platform transport to call. Everything above them (the public interfaces) knows nothing about HDLC or the OS; everything below the choice (frame building, ABM state, poll/final handling) is shared. Branching higher up would tempt each platform to grow its own protocol logic, which is the divergence the shared engine exists to prevent.

The platform transports sit behind internal interfaces injected through an internal constructor, so the dispatch is unit-testable with mocks while the public constructor stays free of internal types.

## No container dependency in the library

`MicroGateConnector` and `MicroGatePortSource` expose a single public parameterless constructor each, with configuration supplied per `Connect` call, and every implementation is a plain class behind an `IThing` interface, so any container resolves them by naming convention with no explicit registration or configuration object. The library itself references no container and no other package; only the sample application wires one up.

## A native interface at the bottom of each transport

Each platform's P/Invoke surface is wrapped by a small interface (`ILinuxNative`, `IWindowsNative`) of semantic operations, and everything above it (devices, connectors, port enumeration) depends only on that interface. The connection itself depends on an even smaller `IMicroGateDevice`, shared by both platforms. The alternative, calling the static P/Invoke methods directly, would make every path from opening a device to receiving a frame untestable without hardware and, for Windows, without Windows. The accepted cost is one thin, untested-by-mocks adapter per platform, covered instead by integration tests against a real tty on Linux.

## One dedicated reader per connection

Each connection owns one background task that blocks on the device read for the connection's lifetime. That loop is the only place inbound frames are parsed and answered, so protocol responses (acknowledgements, rejects) are produced in exactly one place, in receive order. Blocking reads were chosen over polling or overlapped I/O because both native APIs offer a blocking read that a receiver disable reliably interrupts, the same technique the vendor SDK samples use to cancel a pending read. The cost is one thread per open connection.

## No retransmission window

`ReceiveReady`, `ReceiveNotReady`, and `Reject` frames received from the peer are observed for connection state only; there is no send window or retransmission buffer. On a directly cabled link the driver's CRC check already discards corrupted frames, so a resend buffer would add complexity for a failure mode the link rarely has. Extending the library to lossy or shared-media links would need one.

## Pooled payload delivery

Received payloads are handed to subscribers as pooled `IMemoryOwner<byte>` values rather than fresh arrays, so high-rate small-frame traffic does not allocate per frame. The price is an ownership contract: the subscriber must dispose every payload.
