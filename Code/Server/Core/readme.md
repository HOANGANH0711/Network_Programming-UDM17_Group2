# Server Core Module - Member 1 Implementation

## Overview
Server Core module implements the foundational TCP server infrastructure for the Caro Online game. This includes connection management, client handling, and event logging.

## Component Breakdown

### 1. ServerManager.cs
**Purpose:** Manages the entire TCP server and client connections

**Key Features:**
- Starts TCP server on configurable port (default: 5000)
- Accepts incoming client connections (1-n clients)
- Maintains list of active clients
- Provides broadcast and point-to-point messaging
- Event system for connection/disconnection tracking
- Graceful shutdown capability

**Key Methods:**
```csharp
StartAsync()           // Start server and begin accepting connections
StopAsync()            // Stop server and close all connections
GetConnectedClients()  // Get list of all connected clients
GetClientCount()       // Get count of active clients
BroadcastAsync()       // Send message to all clients
SendToClientAsync()    // Send message to specific client
```

**Events:**
- `ClientConnected`: Raised when a new client connects
- `ClientDisconnected`: Raised when a client disconnects

### 2. ClientHandler.cs
**Purpose:** Manages individual client connections

**Key Features:**
- Handles send/receive of messages per client
- Maintains connection state
- Async message processing
- Graceful disconnection handling

**Key Methods:**
```csharp
HandleAsync()         // Handle client communication
SendMessageAsync()    // Send message to client
DisconnectAsync()     // Disconnect the client
```

**Events:**
- `MessageReceived`: Raised when message is received from client
- `Disconnected`: Raised when client disconnects

## Program.cs - Server Entry Point
- Initializes ServerManager
- Subscribes to connection events
- Implements console UI for monitoring
- Handles graceful shutdown (Ctrl+C)
- Real-time connection monitoring

## Testing

### TestClient.cs (Located in Utils)
Provides two testing modes:

**Single Client Test:**
- Connect one test client to server
- Send test message
- Verify connection/disconnection

**Multiple Clients Test:**
- Connect N test clients simultaneously
- Verify 1-n client connections
- Test concurrent message handling

### Running Tests

1. **Start Server:**
```bash
cd Code/Server
dotnet run
```

2. **Run Test Client (in another terminal):**
```bash
cd Code/Server
dotnet run --project Utils/TestClient.csproj
```

Then select test mode:
- Press 's' for single client test
- Press 'm' for multiple clients test (enter number 2-10)
- Press 'q' to quit

## Logging System

### Color-coded Console Output:
- 🟢 **Green**: Information messages (connections, server status)
- 🔴 **Red**: Error messages
- 🟡 **Yellow**: Warning messages
- 🔵 **Blue**: Monitor/Status updates
- 🔷 **Cyan**: Connection events
- 🟣 **Magenta**: Disconnection events

### Log Format:
```
[YYYY-MM-DD HH:mm:ss] [LEVEL] Message
```

## Architecture Diagram

```
ServerManager (Main TCP Listener)
├── Accepts TcpClient connections
├── Creates ClientHandler for each client
└── Maintains collection of active ClientHandlers
    ├── ClientHandler 1
    │   ├── TcpClient
    │   ├── NetworkStream
    │   └── Message Handling
    ├── ClientHandler 2
    │   ├── TcpClient
    │   ├── NetworkStream
    │   └── Message Handling
    └── ClientHandler N
        ├── TcpClient
        ├── NetworkStream
        └── Message Handling
```

## Thread Safety
- Uses lock mechanism for thread-safe client collection access
- Async/await pattern for non-blocking I/O
- CancellationTokenSource for graceful shutdown

## Configuration
- **Default Port:** 5000
- **Buffer Size:** 4096 bytes
- **Monitor Interval:** 10 seconds

## Deliverables Checklist ✓

- [x] Server runs successfully
- [x] Client can connect/disconnect
- [x] Connection logging implemented
- [x] Support for 1-n client connections
- [x] Async message handling
- [x] Event system for connection tracking
- [x] Graceful shutdown mechanism
- [x] Test client for verification
- [x] Console UI with monitoring

## Future Enhancements
- Message packet serialization/deserialization
- Heartbeat/ping-pong mechanism
- Connection timeout handling
- Bandwidth throttling
- Client authentication
- Persistent logging to file
