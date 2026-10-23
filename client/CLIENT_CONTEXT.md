# GamePort Client — Development Context

## 1. Project Overview

GamePort Client is the Windows application installed on every gaming PC inside a GamePort gaming cafe.

It runs in kiosk mode and provides the customer-facing gaming environment.

The Client application is NOT the Cashier and NOT the Cloud application.

Its primary responsibilities are:

- Customer login
- Display gaming PC status
- Start assigned games
- Maintain active gaming session
- Enforce session time
- Restore session after crash/restart
- Communicate with Cashier over LAN
- Receive commands from Cashier
- Launch games
- Control Windows environment
- Kiosk mode
- Device health/heartbeat
- Show connection state
- Support administrator access
- Prepare for optimized LAN screen streaming

---

# 2. Read These Context Files First

Before making any architectural or implementation decision:

1. Read `../GAMEPORT_CONTEXT.md`
2. Read this `CLIENT_CONTEXT.md`

The global context is the source of truth for the complete GamePort architecture.

This file contains Client-specific rules.

Do not create an architecture that conflicts with the global context.

---

# 3. Technology Stack

Use:

- C#
- .NET 10 LTS
- WPF
- MVVM
- CommunityToolkit.Mvvm
- SignalR where appropriate
- REST only where appropriate
- Serilog
- FluentValidation where applicable
- xUnit

Windows-specific functionality may use appropriate Windows APIs.

Do not introduce unnecessary third-party dependencies.

---

# 4. Core Architecture

Recommended projects:

```text
client/
├── GamePort.Client.sln
├── CLIENT_CONTEXT.md
│
├── src/
│   ├── GamePort.Client/
│   ├── GamePort.Client.Domain/
│   ├── GamePort.Client.Application/
│   ├── GamePort.Client.Infrastructure/
│   └── GamePort.Client.Contracts/
│
└── tests/
    ├── GamePort.Client.UnitTests/
    ├── GamePort.Client.IntegrationTests/
    └── GamePort.Client.EndToEndTests/
```

Architecture:

```text
Presentation
      ↓
Application
      ↓
Domain
      ↑
Infrastructure
```

Domain must not depend on WPF, Windows APIs, PostgreSQL, SignalR, or HTTP.

---

# 5. Communication Architecture

The Client communicates with:

**Cashier over LAN**

The Client does NOT normally communicate directly with Cloud.

```text
Cloud
  ↑
Cashier
  ↑
 LAN
  ↓
Client
```

Cashier is the local operational authority.

The Client must never independently become the authority for:

- Customer wallet
- Pricing
- Reservations
- Session billing
- Payments
- Device ownership

---

# 6. Client Identity

A Client device has its own device identity.

Do NOT use customer credentials as device identity.

Device identity should contain appropriate information such as:

- DeviceId
- GameNetId
- Device name
- Device type
- Registration state
- Local configuration
- Software version
- Last heartbeat
- Connection state

Device registration begins from Cashier.

Cashier creates/registers the device.

The Client then connects using its assigned device identity/credentials.

---

# 7. Device States

The system must distinguish operational state from communication state.

Important states:

```text
Available
In Use
Reserved
Offline
Disconnected
```

Definitions:

### Available
Machine is powered on, healthy and can be reserved/used.

### In Use
An active gaming session exists.

### Reserved
Machine is assigned to a reservation.

### Offline
Machine is powered off or unavailable.

### Disconnected
Machine appears powered on but cannot communicate with Cashier.

Do NOT use "Offline" when the real condition is a LAN communication failure.

---

# 8. Heartbeat

Client periodically sends heartbeat information to Cashier.

Heartbeat may include:

- DeviceId
- Timestamp
- Current session
- Current game
- Windows status
- Client version
- Resource information where useful
- Connection state

Heartbeat must not unnecessarily consume resources.

---

# 9. Session Authority

Cashier is the session authority.

The Client receives session information from Cashier.

Client responsibilities:

- Display remaining time
- Enforce local session countdown
- Launch assigned game
- Detect session end
- Restore session
- Communicate state to Cashier

Client must not independently change the authoritative session price.

---

# 10. Session Price

Session pricing is frozen when the session starts.

Example:

```text
14:00 → 100,000 تومان/hour
14:30 → price changes to 150,000
```

The existing session remains based on the original session price.

Client should receive the effective session information from Cashier.

---

# 11. Session Recovery

The Client must recover from:

- Application crash
- Windows restart
- Temporary Client failure

Example:

```text
Session remaining:
42 minutes

Windows restarts

Client starts automatically

↓
Restore session

↓
42 minutes remaining
```

The Client should not silently start a new session.

---

# 12. Cashier Failure

If Cashier becomes temporarily unavailable:

Client should continue an active session using locally cached session information.

Target grace period:

**approximately 1 hour**

During this period:

- Session continues
- Remaining time continues
- Client shows disconnected state
- Client attempts reconnection

If Cashier returns:

```text
Reconnect
↓
Validate local session
↓
Synchronize state
↓
Resume normal operation
```

If Cashier does not return after the defined recovery window, the Client should follow the global GamePort recovery policy.

Do not invent a different policy.

---

# 13. Customer Login

Customer normally logs into the gaming Client using:

- National ID
- Password

The exact authentication mechanism belongs to the GamePort customer domain.

The Client should not store unnecessary customer credentials.

Authentication should be validated against Cashier/local GamePort authority.

---

# 14. QR Login

The mobile Customer App can later provide a QR-based login flow.

Expected conceptual flow:

```text
Customer App
    ↓
Generate temporary login token
    ↓
Customer scans/shows QR
    ↓
Client sends token to Cashier
    ↓
Cashier validates token
    ↓
Customer authenticated
```

Do not treat QR contents as trusted identity by themselves.

Tokens must be:

- Short-lived
- Single-use
- Non-guessable
- Validated by the appropriate authority

---

# 15. Game Launching

Cashier can command the Client to launch a game.

Example:

```text
Cashier
   ↓
Launch CS2
   ↓
Client
   ↓
Validate command
   ↓
Launch configured executable
```

Game configuration should contain appropriate:

- GameId
- Display name
- Executable
- Arguments where required
- Installation path
- Enabled state

Do not hard-code game paths in ViewModels.

---

# 16. Game Process Management

Client should be able to:

- Launch configured games
- Detect running game
- Detect game exit
- Prevent unauthorized applications where required
- Return to Client UI after game exits

Game process handling must be robust against:

- Game crash
- Client restart
- Windows restart
- Multiple processes
- Launcher processes

Do not assume the first launched process is always the final game process.

---

# 17. Kiosk Mode

The Client runs as a kiosk-style application.

Normal customer interaction should remain inside GamePort Client.

Potential restrictions include:

- Prevent access to Windows desktop
- Prevent unauthorized applications
- Hide unnecessary system UI
- Prevent accidental application termination

Administrator access must be available through a special flow.

---

# 18. Administrator Access

Admin access uses:

**Special hotkey + administrator authentication**

Example conceptual flow:

```text
Special hotkey
    ↓
Admin authentication
    ↓
Admin mode
```

Do not hard-code administrator credentials.

Admin mode should be auditable where relevant.

---

# 19. Windows Integration

Client may integrate with Windows for:

- Volume
- Mouse speed
- Display settings
- Brightness where supported
- Restart
- Shutdown
- Lock
- Application launch
- Process management
- Startup
- Kiosk behavior

Windows-specific functionality belongs in Infrastructure/services.

Do not put Win32 calls directly inside ViewModels.

---

# 20. Startup

Client should start automatically with Windows.

Startup flow:

```text
Windows starts
↓
GamePort Client starts
↓
Load configuration
↓
Initialize logging
↓
Load device identity
↓
Connect to Cashier
↓
Restore active session if necessary
↓
Enter normal UI
```

Startup must remain resilient.

---

# 21. Auto Recovery

Client should be designed to recover from:

- Network interruption
- Cashier restart
- Client crash
- Windows restart

Use retry/backoff strategies.

Do not create aggressive infinite reconnect loops.

---

# 22. Local Storage

Client may maintain a small local data store/cache for:

- Device identity
- Configuration
- Current session recovery information
- Cached connection information
- Application state
- Logs

Do not duplicate the full Cashier database on every Client.

Client local storage should remain intentionally small.

---

# 23. Security

Never trust commands simply because they arrive over LAN.

Client communication should use authenticated device identity.

Validate:

- Sender
- Command
- DeviceId
- Session context
- Permissions where appropriate
- Command freshness/idempotency

Sensitive operations should not be executable through arbitrary local network messages.

---

# 24. Commands From Cashier

Potential commands:

```text
LaunchGame
StopGame
LockClient
UnlockClient
RestartComputer
ShutdownComputer
ChangeVolume
ShowMessage
StartSession
EndSession
RestoreSession
```

Commands should have:

- CommandId
- DeviceId
- Timestamp
- Type
- Payload
- CorrelationId where needed

Commands should be idempotent where practical.

---

# 25. UI

Client UI is customer-facing.

It should be:

- Extremely simple
- Fast
- Modern
- Gaming-focused but professional
- Persian RTL
- Touch/mouse friendly
- High contrast
- Minimal distractions

Do not use the same information density as Cashier.

Customer should immediately understand:

- Who is logged in
- Current session
- Remaining time
- Current game
- Available actions
- Connection state when relevant

---

# 26. Main Client States

Important UI states:

```text
Welcome / Login
Authenticated
Session Starting
Active Session
Game Running
Session Ending
Session Finished
Disconnected
Recovering
Admin Mode
```

Design these as explicit states.

---

# 27. Active Session Screen

During an active session, show:

- Remaining time
- Current game
- Session information
- Relevant controls

The remaining time is the most important piece of information.

Avoid clutter.

---

# 28. Disconnected UI

If Cashier cannot be reached:

Show something like:

```text
ارتباط با صندوق قطع شده است

سشن فعلی شما همچنان ادامه دارد.

در حال تلاش برای برقراری مجدد ارتباط...
```

Do not falsely say the PC is offline.

---

# 29. Session End

When time expires:

- Notify customer
- Prevent continued gaming
- Handle game process according to the global session policy
- Return to appropriate Client state

Do not silently create another session.

---

# 30. Screen Streaming

GamePort may later support optimized LAN live screen streaming.

Requirements:

- LAN-first
- Low latency
- Low bandwidth relative to quality
- Minimal gaming performance impact
- Support many clients
- Secure
- Must not interfere with active gameplay

Do not implement a naive screenshot-every-N-seconds solution.

When implementation begins, evaluate an appropriate real-time video/desktop streaming technology.

---

# 31. Performance

Client runs on gaming PCs.

Performance is critical.

Do not:

- Perform unnecessary polling
- Block UI thread
- Run expensive background tasks continuously
- Allocate large objects repeatedly
- Run heavy image processing unnecessarily
- Interfere with games

Resource usage should remain minimal during active gaming.

---

# 32. Logging

Use Serilog.

Log:

- Startup
- Shutdown
- Connection changes
- Session recovery
- Commands
- Game launch/exit
- Unexpected exceptions
- Windows integration failures

Never log:

- Passwords
- Authentication secrets
- Tokens
- Sensitive customer information unnecessarily

---

# 33. Testing

Important unit tests:

- Session countdown
- Session recovery
- Command validation
- Device state transitions
- Game state transitions
- Reconnection logic

Integration tests:

- Cashier communication
- SignalR
- Local persistence
- Device registration

End-to-end tests:

```text
Customer Login
→ Start Session
→ Launch Game
→ Cashier Disconnects
→ Session Continues
→ Cashier Returns
→ State Reconciles
```

---

# 34. Implementation Order

Build in this order:

### Phase 1
- Solution
- WPF shell
- DI
- Logging
- Configuration
- Device identity

### Phase 2
- Cashier connection
- Heartbeat
- Connection state

### Phase 3
- Customer login
- Session state

### Phase 4
- Session countdown
- Recovery

### Phase 5
- Game management
- Game launching
- Process tracking

### Phase 6
- Kiosk mode
- Windows integration
- Admin mode

### Phase 7
- Commands from Cashier

### Phase 8
- Hardening
- Security
- Testing
- Auto-start/update

### Phase 9
- Screen streaming research/implementation

---

# 35. Golden Rules

1. Cashier is the local operational authority.
2. Client communicates primarily with Cashier.
3. Client does not normally communicate directly with Cloud.
4. Client is not responsible for authoritative billing.
5. Session price comes from Cashier.
6. Existing session price never changes mid-session.
7. Active sessions must survive Client/Windows restart.
8. Temporary Cashier failure must not immediately terminate an active session.
9. "Disconnected" and "Offline" are different states.
10. Device identity is separate from customer identity.
11. Never trust arbitrary LAN commands.
12. Do not put Windows API calls in ViewModels.
13. Do not put business rules in code-behind.
14. Keep Client resource usage very low.
15. Do not duplicate Cashier's database.
16. Do not introduce unnecessary dependencies.
17. Prefer explicit Contracts.
18. Test recovery scenarios thoroughly.
19. Security-sensitive operations must be authenticated.
20. When uncertain, follow `GAMEPORT_CONTEXT.md`.