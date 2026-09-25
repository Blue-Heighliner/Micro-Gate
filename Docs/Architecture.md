# Architecture

This document explains the high-level design decisions behind the library's implementation: *why* it's built the way it is, not the class-by-class mechanics of *how*.

## One protocol engine over raw framing, on both platforms

MicroGate ships two structurally different SDKs. On Windows, `mghdlc.dll` exposes a base API (bit-level HDLC framing) and a separate link-layer API (`MgslDl*`) with its own asynchronous balanced mode (ABM) engine. On Linux, the SyncLink kernel driver exposes only a tty device with the base API's equivalent; there is no link-layer engine at all.

Using the Windows link layer there and something else on Linux would mean two independent, potentially inconsistent implementations of the same protocol. The library instead never touches `MgslDl*`: both platforms use only the base API (flag detection, bit stuffing, CRC) and share one userspace ABM implementation, `HdlcStateMachine`, so protocol behavior cannot drift between them. The accepted trade-off is that features of the Windows link layer (automatic retries, timers) are not available; the library implements only what a directly cabled link needs.

## The operating system branch sits as low as possible

`MicroGatePeer` and `MicroGatePortSource` are the only types that branch on the operating system, and only to choose which platform transport to call. Everything above them (the public interfaces) knows nothing about HDLC or the OS; everything below the choice (frame building, ABM state, poll/final handling) is shared. Branching higher up would tempt each platform to grow its own protocol logic, which is the divergence the shared engine exists to prevent.

The platform transports sit behind internal interfaces injected through an internal constructor, so the dispatch is unit-testable with mocks while the public constructor stays free of internal types.

## Construct a peer first, then start it with the port and options

Constructing a peer does nothing and takes no arguments; the port name and options are supplied to `Start`. The alternative, a connector or factory whose `Connect` or `Create` returns a ready connection, forces callers to subscribe to events only after the link exists, so the earliest frames and state changes can be lost, and a factory adds a type whose only job is to carry a port name and options to a constructor. Passing them to `Start` keeps the peer trivially constructible, registrable in a container without configuration, and observable before anything happens, and it keeps failure handling in one place: validating options, opening the device, requesting the link, and waiting for the remote peer all happen inside `Start`, which disposes the peer on failure once it has begun. The accepted cost is a peer that can be in a not-yet-usable state, made explicit by `MicroGatePeerState`, and one that is single use so a lifecycle never has to be rewound, which means a container must hand out a new peer per connection.

## One symmetric start, with retried requests

ABM has no client and server, so the peer has a single `Start` instead of connect and listen calls: it both sends connection requests and accepts them, and a `null` retry interval turns the sending off for a purely passive side. The alternative, separate `Connect` and `Listen` methods, encodes a role the protocol does not have and, worse, leaves a timing hazard: a request sent, or a device opened, at the wrong moment is lost, because the implementation has no protocol timers. Re-sending the request on an interval until answered removes the need to start one side first, at the cost of extra requests on the wire, harmless because a peer that is already connected simply acknowledges again. A retry is skipped once the link is up, so a request from the remote peer that lands between retries is never followed by a stray one from this side.

## Observables instead of events

Received data and state changes are exposed as `IObservable<T>` rather than .NET events. Observables complete, which gives a definite end to both streams when the peer disconnects (an event cannot say "no more"), compose with Rx operators, and let each subscription be disposed independently. They are implemented with System.Reactive `Subject<T>`, the standard multicast primitive, rather than a hand-written one; that makes System.Reactive the library's only dependency, which is acceptable because it is the reference implementation of the interface callers are likely to use anyway. Subjects deliver one item to every observer, so received data is a `ReadOnlyMemory<byte>` over a fresh array, shared and safe to keep, instead of a per-observer pooled owner; the price is one allocation per received frame (at most 4 KB) in exchange for a contract that survives Rx operators that buffer or switch threads, which an owner disposed after `OnNext` could not.

Each stream is exposed through a thin wrapper that unsubscribes an observer that throws and swallows the exception, because a subject would otherwise propagate it into the receive loop and skip the observers after it. Observers are called synchronously on the peer's receive thread, outside any peer lock, so a slow observer delays receiving but cannot deadlock the peer; disposing the peer from inside a callback is supported by deferring the device close until the receive thread unwinds.

## No container dependency in the library

`MicroGatePeer` and `MicroGatePortSource` expose a single public parameterless constructor each, with configuration supplied to `Start`, and every implementation is a plain class behind an `IThing` interface, so any container resolves them by naming convention with no explicit registration or configuration object. The library references no container and only the System.Reactive package; only the sample application wires a container up. Because a peer is single use, container-built code creates peers through `IMicroGatePeerFactory` rather than injecting `IMicroGatePeer`, which would either share one peer or make the container track every disposable peer it hands out.

## A native interface at the bottom of each transport

Each platform's P/Invoke surface is wrapped by a small interface (`ILinuxNative`, `IWindowsNative`) of semantic operations, and everything above it (devices, device openers, port enumeration) depends only on that interface. The peer itself depends on an even smaller `IMicroGateDevice`, shared by both platforms. The alternative, calling the static P/Invoke methods directly, would make every path from opening a device to receiving a frame untestable without hardware and, for Windows, without Windows. The accepted cost is one thin, untested-by-mocks adapter per platform, covered instead by integration tests against a real tty on Linux.

## One dedicated reader per connection

Each connection owns one background task that blocks on the device read for the connection's lifetime. That loop is the only place inbound frames are parsed and answered, so protocol responses (acknowledgements, rejects) are produced in exactly one place, in receive order. Blocking reads were chosen over polling or overlapped I/O because both native APIs offer a blocking read that a receiver disable reliably interrupts, the same technique the vendor SDK samples use to cancel a pending read. The cost is one thread per open connection.

## Reliable delivery with go-back-N

Information frames are numbered modulo 8 and each is kept until the remote peer acknowledges it, with at most 7 outstanding. If the remote peer sees a gap it rejects from the missing frame and the sender resends everything from there; if nothing follows a lost frame, or the rejection is itself lost, a timer resends whatever is still unacknowledged. The alternative, trusting the driver's CRC check to make the link reliable, is wrong: the CRC only discards a corrupted frame, so a single lost frame otherwise stalls that direction permanently while both sides still report connected. Selective repeat would resend less but needs a reordering buffer on the receiver and a larger sequence space; go-back-N keeps the receiver stateless apart from one counter, which suits a point-to-point serial link where loss is rare. Receivers reject a gap only once until it is filled, so a burst of later frames does not trigger a burst of resends. Sends beyond the window wait for an acknowledgement, which also gives callers backpressure, a limit on timer driven resends turns a remote peer that has gone silent into a disconnect instead of a peer that looks connected forever, and a payload limit (the drivers discard received frames over 4096 bytes) is enforced up front, since an oversized frame would otherwise be resent forever without ever being acknowledged.
