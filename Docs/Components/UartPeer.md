# UART peer

Covers `UartPeer`, `IUartDeviceOpener` with its Linux and Windows implementations, and `WindowsUartDevice`. The peer reuses `IMicroGateDevice` from the HDLC peer, so reading, writing, receiver cancellation and disposal are the same operations; only configuration and the shape of reads differ.

## Lifecycle

Like the HDLC peer, the constructor opens nothing. `Start` validates the options, claims the peer with a `started` flag, opens the device through the opener for the running operating system on the thread pool, starts a receive loop and a delivery task, and moves `Idle` to `Open`. A failure after the claim disposes the peer. Received data goes through a channel from the receive loop to the delivery task so a slow `Receiver` cannot stall the device read; observers and the receiver run outside every lock on the same notification queue pattern as the HDLC peer, and an exception from either is reported on `Exceptions` rather than ending the loop.

The transmitter is enabled on the first `Send` under a send semaphore, so a peer that only listens never drives the line. `Send` splits data into writes of at most 4096 bytes, the driver's asynchronous transmit limit, and holds the semaphore for the whole call so concurrent sends are never interleaved.

Disposal disables the receiver to wake the blocked read, waits for the loop and delivery to finish (bounded by a timeout so a blocked `Receiver` cannot hang disposal), disables the transmitter if it was enabled, and disposes the device. Disposing from inside `Receiver` is safe because it does not wait on its own delivery task.

## Linux

The opener selects the standard `N_TTY` line discipline, not the HDLC one, so the device behaves as a tty and reads return whatever bytes have arrived. `ILinuxNative.ConfigureAsynchronous` puts the terminal in raw mode (no echo, line editing, signal characters or newline translation, so every byte value passes unchanged), sets the character size, stop bits, parity and the closest standard speed, and enables the receiver and ignores modem control lines. A rate with no standard speed falls back to 38400 on the terminal side. The opener then sets the driver parameters (mode asynchronous, exact data rate, data bits, stop bits, parity), which the vendor documents as overriding the terminal settings, so any positive rate is honored by the hardware.

Pseudo terminals force 8 data bits and no parity, so tests against one can check speed, stop bits and byte transparency but not the character size or parity; those are verified against the mocked native layer.

## Wire signals

`SignalWatcher` (shared with the HDLC peer) runs on a thread of its own that starts with the receive loop and blocks in `IMicroGateDevice.WaitSignals` instead of polling. The device waits for the driver's wire events, asking for the opposite state of each input wire (the inactive event for a wire that is active and the other way round); the driver treats such an event as satisfied while the wire is already in that state, so a change between the last reading and the wait is never missed. The watcher reports each change to the peer, which publishes it on `SignalsChanged` through the notification queue, and arms itself once every watched wire (DCD and DSR when `DetectDisconnect` is set, otherwise none) has been seen active; after that a watched wire going inactive calls the peer's loss handler once, which ends the receive loop by disabling the receiver, so the peer closes the same way as when the device fails. DTR and RTS are outputs the device drives, so they are read with the others but cause no wake-up. The watcher starts only when `EnableSignals` or `DetectDisconnect` is set, and without a changed callback when only the latter is, so a peer that wants neither never reads the wires. It stops by itself when the first reading returns null.

The Linux wait is a blocking `MGSL_IOCWAITEVENT` ioctl that has no cancel, so `CancelSignalWait` wakes it by sending the waiting thread a signal. The first use installs a no-op handler for `SIGURG` (ignored by default) with `siginterrupt`, so the signal makes the ioctl return `EINTR` instead of restarting it; the thread id is recorded under a lock while the thread waits, so it is never signalled after it has left. A signal that arrives just before the thread blocks would be lost, so disposal repeats the cancel every 20 ms until the thread has finished. Windows waits with an overlapped `MgslWaitEvent` on an event object and cancels with `MgslCancelWaitEvent`. A device whose driver rejects the event wait falls back to sleeping 100 ms between reads, still cancellable. Disposal stops the watcher before the device is closed, because reading a closed descriptor could hit an unrelated file that reused its number. Linux reads the wires with `TIOCMGET` and raises DTR and RTS with `TIOCMBIS`; Windows uses `MgslGetSerialSignals` and `MgslSetSerialSignals`, and its event path is untested on Windows hardware.

## Hardware behavior

Verified on two SyncLink USB bricks cabled through a null modem: 9600 8N1, 19200 and 115200 8N1, 9600 7E1, 5N2 and 8O2, and a nonstandard 12345 baud each carried 10000 random bytes both ways without loss, and loopback works. Mismatched baud rates deliver garbage. When a port is reopened at a higher rate than the remote end's previous session, the first few bytes sent immediately after the remote `Start` can be lost (up to about 20 at 115200); waiting about half a second after both ends have started removes it, so a sender should not transmit in the instant the remote end opens.

On the bricks through the null modem adapter, closing a port (a disposed peer, or a killed process) drops its DTR, which the remote brick sees as DCD and DSR going inactive; CTS stays active whatever the remote does, which is why it is not watched. An HDLC peer whose remote process was killed without sending a disconnect became `Disconnected` within the polling interval with the default detection, instead of waiting for retransmissions to give up. The outputs are already raised when a port opens.

## Windows

The opener sets the asynchronous mode and parameters through the driver, with the same busy-open retry as the HDLC opener. The Windows asynchronous read returns only after the requested number of bytes have arrived, so `WindowsUartDevice` always reads one byte at a time to deliver data as it comes. The Windows path has been tested against the mocked native layer only, not on Windows hardware.
