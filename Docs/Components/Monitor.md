# Monitor lifecycle

Covers `MicroGateMonitor`, `MicroGateFrame`, `MicroGateFrameKind`, the device openers (`IMicroGateMonitorDeviceOpener`, `LinuxMicroGateMonitorDeviceOpener`, `WindowsMicroGateMonitorDeviceOpener`; all internal), and `MicroGateMonitorFactory`. `IMicroGateDevice`, `ILinuxNative`, and `IWindowsNative` are shared with the peer and covered in [Peer.md](Peer.md); this file only covers what differs.

## A simpler sibling of the peer

`MicroGateMonitor` reuses the peer's shape (state under a lock, a notification queue drained one item at a time so observers never run inside a lock or concurrently with each other, a `notifyingThreadId` check so disposal from inside a callback does not deadlock, the same `Isolate` wrapper around each `Subject<T>` so a throwing observer is unsubscribed without affecting the others) but strips out everything that exists to speak the protocol: no `HdlcStateMachine`, no `Establish`, no send window, no retransmission, no write lock (there is only one writer method in the whole type, and it does not exist), and no `Machine` accessor. `ReceiveLoop` reads a frame, decodes it with `ParseFrame`, and publishes it; there is no per-frame lock, since nothing else ever touches the device concurrently with the loop.

## Starting

`Start(portName, options)` selects the opener for the running operating system (`PlatformNotSupportedException` otherwise), claims the monitor by moving `Idle` to `Monitoring` under the state lock (a second start throws `InvalidOperationException`), opens the device through the opener on the thread pool, and starts the receive loop. Unlike the peer, there is nothing to wait for after that: a monitor does not establish anything, so `Start` returns as soon as the device is open and the loop is running, not once a remote peer has answered. Any failure while starting, including cancellation and disposal racing the open, disposes the monitor and rethrows, mirroring the peer's `Start` exactly (down to `ObjectDisposedException` when disposal wins the race).

## Parsing

`ParseFrame` copies the loop's buffer once into a fresh array (`Raw`), then calls `HdlcFrame.Parse` on it and maps the result to a public `MicroGateFrame`, translating `HdlcFrameKind` to the public `MicroGateFrameKind` one for one. `SendSequence` is populated only for `Information`, and `ReceiveSequence` only for `Information` and the three supervisory kinds, matching which fields the wire format actually carries for each kind. A `HdlcFrameException` (too short, or a control byte matching no known kind) is caught here rather than allowed to end the loop like the peer would treat it: the frame is reported as `Malformed`, with `Raw` and the exception's message, so a garbled frame is visible instead of silently dropped, and the loop continues to the next read either way.

## Never writing

Nothing in `MicroGateMonitor` calls `IMicroGateDevice.Write`; the type has no method that could reach it. The stronger guarantee is one layer down, in the openers: `LinuxMicroGateMonitorDeviceOpener` and `WindowsMicroGateMonitorDeviceOpener` mirror `LinuxMicroGateDeviceOpener`/`WindowsMicroGateDeviceOpener`'s configuration exactly, minus the two calls that touch the transmitter (`EnableTransmitter`, and the idle pattern set by `SetTransmitIdle`/`SetIdleMode`), so the transmitter is left in the disabled state the vendor drivers document it starting in. `DisposeAsync` also calls `DisableTransmitter` before closing the device. Since the transmitter was never enabled, this call has nothing to do in practice; it is kept anyway so the guarantee does not depend on the opener and disposal staying in agreement, only on disposal always running this line.

## Disposal

`DisposeAsync` is idempotent and safe before starting. It disables the receiver (ending the loop) and the transmitter, then closes the device either inline or, if called from inside an observer callback, in the background once the loop and any pending work have stopped, exactly as the peer does for the same reason (waiting for the callback's own thread from inside the callback would deadlock). There is no DISC to send first, since a monitor was never party to a connection to disconnect from.

## Platform dispatch

`MicroGateMonitor` (defaulting a null options argument to `new MicroGateMonitorOptions()` and rejecting a non-positive `ClockSpeed` with `ArgumentOutOfRangeException`) picks the Windows or Linux opener the same way `MicroGatePeer` picks its opener, through an internal constructor taking both, which is how the dispatch is tested without depending on the running operating system.
