# Connection lifecycle

Covers `MicroGateDeviceConnection`, `IMicroGateDevice` and its two implementations (`LinuxMicroGateDevice`, `WindowsMicroGateDevice`), the platform connectors (`LinuxMicroGateConnector`, `WindowsMicroGateConnector`), and `LimitedMemoryOwner` (all internal), plus how `MicroGateConnector` and `MicroGatePortSource` dispatch to them.

## Device abstraction

`IMicroGateDevice` is the raw, frame-oriented transport of an opened and configured device: a blocking `Read` of one frame, a `Write` of one frame, and `DisableReceiver` to cancel a blocked read; disposing closes the device. It is the only thing that differs between platforms. `MicroGateDeviceConnection` is the single `IMicroGateConnection` implementation for both, so the receive loop, sending, and disposal behave identically everywhere and can be exercised with a mock or an in-memory device.

Each platform device is a thin wrapper over a native interface (`ILinuxNative`, `IWindowsNative`) that hides pointers, `ref` parameters, and request codes behind semantic operations. The interface implementations (`LinuxNative`, `WindowsNative`) are the only code that calls into the P/Invoke declarations, so everything above them is testable without hardware or the native library.

## Opening a connection

The platform connector, not the connection, owns opening: it opens the device, applies the physical layer configuration from `MicroGateConnectionOptions`, wraps the descriptor or handle in a platform device, constructs a `MicroGateDeviceConnection` with a fresh `HdlcStateMachine` built from the same options, and calls `Establish`. Connections are therefore only ever handed out already established. Constructing the connection starts its receive loop immediately. If configuration throws, the device is disposed before the exception propagates.

`Establish` sends a SABM frame and awaits a `TaskCompletionSource` that the receive loop completes when the state machine first reports `Connected` (the peer's UA, or an inbound SABM when both peers connect at once). Any failure before that, including cancellation, disposes the partly built connection and rethrows, so a failed `Connect` never leaks a handle. When both peers initiate at once, one side's SABM can arrive after it has already become connected and momentarily return it to `Connecting`; both sides converge on `Connected` once the UAs cross.

## Receive loop

One `Task.Run` thread reads one raw frame at a time with a blocking call into a 65535-byte buffer (the maximum HDLC frame size) and feeds it to the state machine. Frames the parser rejects are dropped. For each result the loop, in order: writes any response frame back immediately; rents pooled memory, copies the payload into it, and raises `Received` for a delivered payload; and raises `Disconnected` exactly once when the state leaves `Connected` after having been connected. The loop ends when the blocking read returns zero or less, which disposal triggers by disabling the receiver, and raises `Disconnected` on the way out if it was still connected. A failed response write ends the loop with an exception that disposal swallows.

## Sending

`Send` builds an I-frame with `CreateInformation` and writes it through `WriteFrame`, which holds a lock so sends and the loop's automatic responses never interleave on the wire. A failed write throws `IOException`, and sending while not connected throws `InvalidOperationException`. The `IMemoryOwner<byte>` overload disposes its argument after the write, whether or not it succeeds.

## Disposal

`DisposeAsync` is idempotent. If still connected it makes a best-effort attempt to send DISC (swallowing `IOException`), disables the receiver to unblock the loop, awaits the loop, then disposes the device. Synchronous `Dispose` blocks on `DisposeAsync`.

## Pooled payloads

Delivered payloads are copied into memory rented from `MemoryPool<byte>.Shared`. The pool may return a larger buffer than requested, so `LimitedMemoryOwner` wraps the owner to expose only the first *N* valid bytes through `Memory` while forwarding `Dispose` to the real owner.

## Platform dispatch

`MicroGateConnector` (which defaults a null options argument to `new MicroGateConnectionOptions()`) and `MicroGatePortSource` pick the Windows or Linux implementation with `OperatingSystem.IsWindows()` and `IsLinux()` and throw `PlatformNotSupportedException` elsewhere. Their internal constructors take the platform implementations, which is how dispatch is tested.
