# API

The public API is three interfaces, `IMicroGatePortSource`, `IMicroGateConnector`, and `IMicroGateConnection`, plus the `MicroGateConnectionOptions` record that configures a connection, covering both the device and the HDLC layer. This document covers the *design and flow* of that surface (how the pieces fit together and the order things happen in) using public types only. It does not restate member-level detail already covered in the source itself.

## Shape

A caller constructs `MicroGatePortSource` and `MicroGateConnector` (both with no arguments), or resolves them through a container by their `IThing` to `Thing` naming. Configuration is passed per `Connect` call, as an optional `MicroGateConnectionOptions`, rather than at construction: one connector can then open connections to different devices with different settings, and it stays a plain singleton with nothing to configure.

The three concerns are deliberately separate types rather than one facade: discovery (`IMicroGatePortSource`) is stateless and needs no configuration, opening a link (`IMicroGateConnector`) turns options into a running link, and an open link (`IMicroGateConnection`) is a stateful, disposable resource with its own lifetime and events. None of them expose HDLC framing or operating system details; a caller sees only port names and payload bytes.

`MicroGateConnectionOptions` is a single flat record. `Encoding`, `Crc`, `IdlePattern`, and `HardwareAddressFilter` configure the device's physical layer: line encoding, frame check sequence, the idle pattern sent between frames, and an optional hardware address filter (off by default). `Address` and `DisablePollFinalBit` configure the HDLC layer: `Address` is the single address byte the station sends in every frame and requires in every frame it accepts, and `DisablePollFinalBit` forces the poll/final bit to 0 on every frame in both directions, for peers that do not tolerate it. Every setting has a default (NRZ, CRC-16-CCITT, flag idle, no hardware filter, address `0xFF`, poll/final enabled), so omitting the options, or setting only one property, is always valid.

## Flow

1. `IMicroGatePortSource.GetPorts` returns the names of the MicroGate ports on the machine.
2. `IMicroGateConnector.Connect(portName, options)` opens the device, configures it for HDLC using the device settings in `options`, sends a set asynchronous balanced mode (SABM) frame, and, using the HDLC settings in `options`, completes only once the peer has acknowledged it (or sent its own SABM). It throws `IOException` if the device cannot be opened or the connection cannot be established, and `PlatformNotSupportedException` outside Windows and Linux.
3. The caller subscribes to `Received` and `Disconnected` on the returned `IMicroGateConnection`.
4. `Send` transmits one information frame per call. Inbound information frames raise `Received` once each, in order.
5. Disposing the connection (`Dispose` or `DisposeAsync`) sends a disconnect frame if still connected, stops receiving, and releases the device. A peer disconnect or a device failure raises `Disconnected` instead, exactly once.

## Payload ownership

`Received` delivers an `IMemoryOwner<byte>` drawn from a shared pool. The subscriber owns it and must dispose it when done, or the pooled buffer is never returned. `Send(IMemoryOwner<byte>)` is the mirror image: it takes ownership of the argument and disposes it once the frame has been written, while `Send(ReadOnlyMemory<byte>)` leaves ownership with the caller.

## Threading

`Received` and `Disconnected` are raised on a background thread owned by the connection, not a caller thread, so UI callers must marshal to their own thread. `Send` may be called from any thread; writes are serialized so frames never interleave.
