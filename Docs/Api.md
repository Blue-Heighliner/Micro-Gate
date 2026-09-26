# API

The public API is `IMicroGatePortSource` (discovery), `IMicroGatePeer` (one end of a link) with `IMicroGatePeerFactory` to create them, plus the `MicroGatePeerOptions` record that configures a peer. This document covers the *design and flow* of that surface (how the pieces fit together and the order things happen in) using public types only. It does not restate member-level detail already covered in the source itself.

## Shape

A caller constructs `MicroGatePortSource` and `MicroGatePeer` (both with no arguments), or resolves them through a container by their `IThing` to `Thing` naming. Because a peer is single use, code that needs a new link on demand, in particular a service built by a container, depends on `IMicroGatePeerFactory` and calls `Create` instead of holding a peer; the factory holds no state and is safe to register as a singleton. Constructing a peer touches no hardware and needs no port: the port name and options are supplied later, to `Start`, which is the first point the device is opened. Constructing and starting are separate so a caller can subscribe to everything the peer emits before any traffic or state change can occur; with a single call that returns an open connection, the first frames and the earliest state changes could be missed.

The concerns are deliberately separate types rather than one facade: discovery is stateless and needs no configuration, and the peer is a stateful, disposable resource with its own lifetime and event streams. Neither exposes HDLC framing or operating system details; a caller sees only port names and payload bytes.

Configuration is passed to `Start` as an optional `MicroGatePeerOptions`, not to the constructor, so a peer needs no configuration to be created, registered in a container, or observed, and the settings arrive together with the port they apply to. The record is flat. `Encoding`, `Crc`, `IdlePattern`, and `HardwareAddressFilter` configure the device's physical layer: line encoding, frame check sequence, the idle pattern sent between frames, and an optional hardware address filter (off by default). `Address`, `RetryInterval`, `RetransmitInterval`, and `DisablePollFinalBit` configure the HDLC layer: `Address` is the single address byte the station sends in every frame and requires in every frame it accepts, `RetryInterval` is how often the connection request is re-sent until answered (or `null` to never send one), `RetransmitInterval` is how long sent data may go unacknowledged before it is sent again (or `null` to resend only when the remote peer rejects), `MaxRetransmissions` is how many such resends without any acknowledgement are tolerated before the remote peer is considered gone (or `null` for no limit), and `DisablePollFinalBit` forces the poll/final bit to 0 on every frame in both directions, for peers that do not tolerate it. Every setting has a default (NRZ, CRC-16-CCITT, flag idle, no hardware filter, address `0xFF`, requests and retransmissions every second, 10 retransmissions, poll/final enabled), so omitting the options, or setting only one property, is always valid.

## Flow

1. `IMicroGatePortSource.GetPorts` returns the names of the MicroGate ports on the machine.
2. The caller creates an idle peer with `new MicroGatePeer()`.
3. The caller subscribes to `Received` and `StateChanged` on the peer.
4. The caller starts the peer, exactly once, with `Start(portName, options)`. It opens and configures the device for HDLC, then completes once the link is up. Unless `RetryInterval` is `null`, it sends a set asynchronous balanced mode (SABM) request immediately and again every `RetryInterval` until the remote peer answers. Independently of that, a request arriving from the remote peer is accepted and acknowledged, and also brings the link up. Invalid options (an interval that is zero, negative, or longer than `int.MaxValue` milliseconds, or a `MaxRetransmissions` of zero or less) throw `ArgumentOutOfRangeException` and an unsupported operating system throws `PlatformNotSupportedException`, both leaving the peer `Idle`. Otherwise `Start` throws `IOException` if the device cannot be opened or closes before the link is established, `ObjectDisposedException` if the peer is disposed before the link is up, and `OperationCanceledException` if canceled; those failures dispose the peer.
5. `Send` transmits one information frame per call. Inbound information frames are published on `Received` once each, in order and without gaps.
6. Disposing the peer (`Dispose` or `DisposeAsync`) sends a disconnect frame if still connected, stops receiving, and releases the device. A remote disconnect or a device failure ends the peer the same way, without disposal.

## Forming the link

HDLC asynchronous balanced mode has no client and server: both stations are equals and either may send the request, so the API has one `Start` rather than separate connect and listen calls. Whether a side transmits requests is a setting, not a role. The usual choice is the default, where both sides send and whichever request lands first wins, so neither has to be started first. A `null` `RetryInterval` makes a side purely passive, for a remote that always initiates. Requests are repeated because the protocol implementation has no timers of its own and a request sent while the remote peer is not yet reading is otherwise lost. A side that is passive depends on the remote peer sending a request after this side is reading.

## Reliable delivery

While the peer is connected, data sent with `Send` arrives at the remote peer in order and without gaps. Each frame is numbered and kept until acknowledged, and is sent again when the remote peer rejects it or when `RetransmitInterval` passes without an acknowledgement. If `MaxRetransmissions` such timer driven resends pass with nothing acknowledged, the remote peer is considered gone and the peer becomes `Disconnected` (streams complete, waiting and later sends fail); without the limit a dead remote peer would leave the peer `Connected` indefinitely. At most 7 frames may be unacknowledged at once; a `Send` beyond that waits until an acknowledgement arrives, and fails with `IOException` if the peer disconnects while it waits or with `OperationCanceledException` if its token is canceled. A `Send` that completes means the frame was handed to the device, not that the remote peer has it. A `Send` whose write to the device fails throws `IOException` and takes the frame back, so the caller may repeat it without the data being delivered twice. A payload may be at most `MaxPayloadSize` bytes (4090): the drivers discard received frames over 4096 bytes, and a larger `Send` throws `ArgumentOutOfRangeException` rather than being resent forever.

A repeated connection request while connected (for instance a retry that crossed the answer) resets the link on both sides: sequence numbers restart at zero and any unacknowledged frames are renumbered and sent again, so nothing is lost, but the remote peer may then receive a frame it already had. Delivery is therefore exactly once and in order except across such a reset, where it is at least once.

Delivery to `Received` is hot: data that arrives while nothing is subscribed is acknowledged and discarded, so subscribe before `Start`.

## Lifecycle

A peer is single use and only moves forward: `Idle`, then `Connecting`, then `Connected`, then `Disconnected`. `Disconnected` is terminal and is reached by a remote disconnect, loss of the device, a failed or canceled start, or disposal; a peer that has left `Idle` cannot be started again, and calling `Start` on it throws `InvalidOperationException`. A caller that wants to reconnect creates a new peer, which is why a container should register the peer as transient. Disposing a peer is still required after a remote disconnect to release the device.

## Observables

`Received` and `StateChanged` are standard `IObservable<T>` streams backed by System.Reactive subjects, so they compose with the rest of Rx. Both complete when the peer becomes `Disconnected`, and subscribing to a peer that is already `Disconnected` completes immediately. `StateChanged` emits transitions from the moment of subscription; the current state is available from `State`.

Received data, state changes, and completion are delivered one at a time and in order across both streams, never concurrently and never while the peer holds a lock, so an observer may read `State` or call the peer from another thread. They are delivered synchronously, normally on the peer's receive thread, so an observer should hand work off rather than block, since receiving waits for it. Disposing the peer from inside a callback is allowed and does not wait: it sends the disconnect and the device is closed in the background once the peer's own threads have stopped. An observer that throws is unsubscribed, and does not affect other observers or the peer.

## Payload data

Every observer of `Received` is handed the same `ReadOnlyMemory<byte>` for a frame. It is backed by an array allocated for that frame and never reused, so an observer may keep it, hand it to another thread, or pass it through Rx operators that buffer or observe on another scheduler; it is shared, so it must not be modified. `Send(ReadOnlyMemory<byte>)` leaves ownership of its argument with the caller, while `Send(IMemoryOwner<byte>)` takes ownership and disposes it once the frame has been sent or the send has failed.

## Threading

`Send` may be called from any thread; writes are serialized so frames never interleave with each other or with the peer's automatic protocol responses.
