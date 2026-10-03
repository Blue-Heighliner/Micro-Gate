# UART peer

Covers `UartPeer`, `IUartDeviceOpener` with its Linux and Windows implementations, and `WindowsUartDevice`. The peer reuses `IMicroGateDevice` from the HDLC peer, so reading, writing, receiver cancellation and disposal are the same operations; only configuration and the shape of reads differ.

## Lifecycle

Like the HDLC peer, the constructor opens nothing. `Start` validates the options, claims the peer with a `started` flag, opens the device through the opener for the running operating system on the thread pool, starts a receive loop and a delivery task, and moves `Idle` to `Open`. A failure after the claim disposes the peer. Received data goes through a channel from the receive loop to the delivery task so a slow `Receiver` cannot stall the device read; observers and the receiver run outside every lock on the same notification queue pattern as the HDLC peer, and an exception from either is reported on `Exceptions` rather than ending the loop.

The transmitter is enabled on the first `Send` under a send semaphore, so a peer that only listens never drives the line. `Send` splits data into writes of at most 4096 bytes, the driver's asynchronous transmit limit, and holds the semaphore for the whole call so concurrent sends are never interleaved.

Disposal disables the receiver to wake the blocked read, waits for the loop and delivery to finish (bounded by a timeout so a blocked `Receiver` cannot hang disposal), disables the transmitter if it was enabled, and disposes the device. Disposing from inside `Receiver` is safe because it does not wait on its own delivery task.

## Linux

The opener selects the standard `N_TTY` line discipline, not the HDLC one, so the device behaves as a tty and reads return whatever bytes have arrived. `ILinuxNative.ConfigureAsynchronous` puts the terminal in raw mode (no echo, line editing, signal characters or newline translation, so every byte value passes unchanged), sets the character size, stop bits, parity and the closest standard speed, and enables the receiver and ignores modem control lines. A rate with no standard speed falls back to 38400 on the terminal side. The opener then sets the driver parameters (mode asynchronous, exact data rate, data bits, stop bits, parity), which the vendor documents as overriding the terminal settings, so any positive rate is honored by the hardware.

Pseudo terminals force 8 data bits and no parity, so tests against one can check speed, stop bits and byte transparency but not the character size or parity; those are verified against the mocked native layer.

## Hardware behavior

Verified on two SyncLink USB bricks cabled through a null modem: 9600 8N1, 19200 and 115200 8N1, 9600 7E1, 5N2 and 8O2, and a nonstandard 12345 baud each carried 10000 random bytes both ways without loss, and loopback works. Mismatched baud rates deliver garbage. When a port is reopened at a higher rate than the remote end's previous session, the first few bytes sent immediately after the remote `Start` can be lost (up to about 20 at 115200); waiting about half a second after both ends have started removes it, so a sender should not transmit in the instant the remote end opens.

## Windows

The opener sets the asynchronous mode and parameters through the driver, with the same busy-open retry as the HDLC opener. The Windows asynchronous read returns only after the requested number of bytes have arrived, so `WindowsUartDevice` always reads one byte at a time to deliver data as it comes. The Windows path has been tested against the mocked native layer only, not on Windows hardware.
