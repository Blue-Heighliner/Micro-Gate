# Connection lifecycle

Covers `LinuxMicroGateConnection`, `WindowsMicroGateConnection`, their connectors (`LinuxMicroGateConnector`, `WindowsMicroGateConnector`), and `LimitedMemoryOwner` (all internal), plus how `MicroGateConnector` and `MicroGatePortSource` dispatch to them. The two connection classes implement `IMicroGateConnection` with identical structure; only the native calls that move bytes differ.

## Opening a connection

The platform connector, not the connection, owns opening: it opens the device, applies the physical layer configuration from `MicroGateConnectionOptions`, constructs the connection with a fresh `HdlcStateMachine` built from the same options, and calls `Establish`. Connections are therefore only ever handed out already established. Constructing the connection starts its receive loop immediately.

`Establish` sends a SABM frame and awaits a `TaskCompletionSource` that the receive loop completes when the state machine first reports `Connected` (the peer's UA, or an inbound SABM when both peers connect at once). Any failure before that disposes the partly built connection and rethrows, so a failed `Connect` never leaks a handle.

## Receive loop

One `Task.Run` thread reads one raw frame at a time with a blocking call into a 65535-byte buffer (the maximum HDLC frame size) and feeds it to the state machine. Frames the parser rejects are dropped. For each result the loop, in order: writes any response frame back immediately; rents pooled memory, copies the payload into it, and raises `Received` for a delivered payload; and raises `Disconnected` exactly once when the state leaves `Connected` after having been connected. The loop ends when the blocking read returns zero or less, which disposal triggers by disabling the receiver, and raises `Disconnected` on the way out if it was still connected.

## Sending

`Send` builds an I-frame with `CreateInformation` and writes it through `WriteFrame`, which holds a lock so sends and the loop's automatic responses never interleave on the wire. On Linux the write is followed by `tcdrain` so a frame has left the device before the lock is released. A short write throws `IOException`. The `IMemoryOwner<byte>` overload disposes its argument after the write, whether or not it succeeds.

## Disposal

`DisposeAsync` is idempotent. If still connected it makes a best-effort attempt to send DISC (swallowing `IOException`), disables the receiver to unblock the loop, awaits the loop, then closes the descriptor or handle. Synchronous `Dispose` blocks on `DisposeAsync`.

## Pooled payloads

Delivered payloads are copied into memory rented from `MemoryPool<byte>.Shared`. The pool may return a larger buffer than requested, so `LimitedMemoryOwner` wraps the owner to expose only the first *N* valid bytes through `Memory` while forwarding `Dispose` to the real owner.

## Platform dispatch

`MicroGateConnector` (which defaults a null options argument to `new MicroGateConnectionOptions()`) and `MicroGatePortSource` pick the Windows or Linux implementation with `OperatingSystem.IsWindows()` and `IsLinux()` and throw `PlatformNotSupportedException` elsewhere. The Windows classes additionally guard on `IsWindows()` themselves so they can be constructed on any OS without tripping platform analyzers.
