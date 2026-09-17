# GamePort — Cashier Context

## 1. Purpose

Cashier is the local operational authority of a GamePort GameNet.

Cashier is NOT just a desktop UI.

Cashier acts as:

- Local server
- Local database authority
- Session authority
- Device manager
- Reservation manager
- Wallet/payment operational layer
- Local sync engine
- LAN communication hub
- Audit/logging authority

Cashier must continue operating when the GameNet has no Internet connection.

Internet connectivity is required only for synchronization with Cloud and cloud-dependent operations.

---

# 2. Relationship With Master Context

The root `GAMEPORT_CONTEXT.md` is the source of truth for the overall GamePort architecture.

This file contains only Cashier-specific decisions and implementation rules.

When a decision conflicts with this file and `GAMEPORT_CONTEXT.md`, the more specific Cashier rule in this file takes precedence unless it violates a system-wide rule.

OpenCode must read:

1. `../GAMEPORT_CONTEXT.md`
2. `CASHIER_CONTEXT.md`

before making architectural changes.

---

# 3. Technology Stack

Cashier is a Windows desktop application.

### Core

- C#
- .NET 10 LTS
- WPF
- MVVM
- CommunityToolkit.Mvvm

### Database

- PostgreSQL
- Entity Framework Core
- Npgsql

### Communication

- SignalR
- REST/HTTP where appropriate
- LAN-first communication with Client and Cafe
- HTTPS communication with Cloud when Internet is available

### Validation

- FluentValidation

### Logging

- Serilog

### Testing

- xUnit
- Integration tests
- Domain/application tests

---

# 4. Cashier Architecture

Cashier follows a layered architecture.

Recommended structure:

```text
cashier/
├── CASHIER_CONTEXT.md
├── src/
│   ├── GamePort.Cashier/
│   ├── GamePort.Cashier.Domain/
│   ├── GamePort.Cashier.Application/
│   ├── GamePort.Cashier.Infrastructure/
│   └── GamePort.Cashier.Contracts/
│
└── tests/
    ├── GamePort.Cashier.UnitTests/
    ├── GamePort.Cashier.IntegrationTests/
    └── GamePort.Cashier.EndToEndTests/
```

Responsibilities:

### Presentation

WPF UI only.

The UI must not contain business logic.

### Application

Contains use cases and orchestration.

Examples:

- StartSession
- EndSession
- ExtendSession
- TransferSession
- CreateReservation
- CancelReservation
- RechargeWallet
- ProcessPayment
- RegisterCustomer
- RegisterDevice
- LaunchGame
- SyncData

### Domain

Contains business rules and domain entities.

The domain must not depend on:

- WPF
- PostgreSQL
- SignalR
- HTTP
- Cloud
- UI

### Infrastructure

Contains implementations for:

- PostgreSQL
- EF Core
- SignalR
- LAN communication
- Cloud API
- Sync
- Logging
- Windows integration
- Printing
- Hardware/system integrations

### Contracts

Contains shared DTOs and communication contracts.

---

# 5. Cashier Is the Local Authority

Cashier is the operational source of truth inside a GameNet.

Client and Cafe must not independently decide critical business state.

Examples:

- Active session
- Session start/end
- Session price
- Reservation state
- Wallet transaction
- Payment state
- Device operational state

These are controlled by Cashier.

Client primarily executes commands and reports its state.

Cafe handles cafe operations but Cashier remains the local operational hub where cross-system coordination is required.

---

# 6. Local Database

Cashier must have its own local PostgreSQL database.

The local database contains all information required for normal GameNet operation.

Examples:

- Customers
- Customer credentials/identifiers required locally
- Devices
- Games
- Sessions
- Reservations
- Wallet
- Wallet transactions
- Payments
- Employees
- Roles/permissions
- Audit logs
- Cafe-related operational data when required
- Sync metadata
- Outbox events
- Inbox/idempotency records
- Configuration

The system must NOT require Cloud access for normal local GameNet operation.

---

# 7. Offline-First Behavior

Cashier must work normally when Internet is unavailable.

When Internet is down:

- Customers can be handled locally.
- Sessions can start/end/extend.
- Reservations already synchronized to Cashier remain usable.
- Cashier can create reservations manually.
- Cashier can cancel reservations.
- Wallet operations can continue according to local business rules.
- Payments can be recorded.
- Devices remain controllable over LAN.
- Client communication continues.
- Cafe communication continues.

Cloud synchronization pauses but local operations continue.

When Internet returns:

```text
Local DB
   ↓
Sync Engine
   ↓
Cloud
```

and relevant Cloud changes are synchronized back:

```text
Cloud
   ↓
Sync Engine
   ↓
Local DB
```

---

# 8. Synchronization

Synchronization must be:

- Reliable
- Resumable
- Idempotent
- Auditable
- Crash-safe

Use:

- Outbox
- Inbox
- Cursor/checkpoint
- Idempotency keys

Example:

```text
100 local events

67 successfully synchronized

Internet disconnects

Next synchronization starts from event 68
```

Do not resend already-confirmed operations unnecessarily.

Every synchronization operation should have a deterministic identity.

The same event must not create duplicate:

- Payments
- Wallet transactions
- Sessions
- Reservations
- Orders
- Audit records

---

# 9. Device Management

Current device types:

- Gaming PC
- PlayStation

Do not introduce additional device types unless explicitly requested.

Cashier manages device registration.

A device is first registered from Cashier.

Registration includes required information such as:

- Device ID
- Name
- Type
- IP address
- MAC address if required
- Hardware information if required
- GamePort Client identity
- Configuration
- Status
- Created/updated timestamps

The Client then connects to Cashier using its registered identity.

---

# 10. Device State Model

Do not confuse machine power state with network connection state.

Operational states include:

### Available

The machine is powered on, healthy, connected, and can be reserved/used.

### InUse

The machine currently has an active session.

### Offline

The machine itself is unavailable/powered off.

### Disconnected

The machine appears to be powered on but cannot communicate with Cashier.

Connection state and operational state should be represented separately internally.

---

# 11. Client Communication

Cashier communicates with Client over LAN.

Communication must support:

- Heartbeat
- Device status
- Session status
- Start session
- End session
- Extend session
- Lock/unlock
- Launch game
- Stop/close game when permitted
- Restart
- Shutdown
- Volume control
- Mouse speed
- Display-related commands when supported
- Background change
- Current running game
- Configuration
- Screen streaming control
- Recovery/session restoration

Commands originate from Cashier.

Client executes commands and reports results/state.

Client does not need to ask Cashier for confirmation for every command.

Sensitive actions may require Cashier operator confirmation before the command is sent.

---

# 12. Real-Time Communication

Cashier must maintain real-time communication with Clients.

Use SignalR where appropriate.

Cashier should maintain connection information for each Client.

The system should detect:

- Connected
- Disconnected
- Reconnected
- Heartbeat timeout
- Invalid device identity
- Authentication failure

A temporary network disconnect must not automatically mean that the physical machine is offline.

---

# 13. Cashier Server

Cashier acts as a local server.

The local server is responsible for:

- Client communication
- Cafe communication
- Local API
- SignalR hub
- Session coordination
- Device coordination
- Local authentication/authorization
- Sync engine
- Health monitoring

The WPF UI and local server responsibilities should remain separated.

The application should not depend on the UI remaining open for the local GameNet services to function.

---

# 14. Session Management

Cashier is the session authority.

A session contains at minimum:

- Session ID
- Customer ID
- Device ID
- Device type
- Start time
- Planned end time
- Actual end time
- Price snapshot
- Pricing information
- Status
- Creation source
- Operator information when applicable
- Audit information

Possible states:

```text
Reserved
Active
Paused
Completed
Cancelled
Expired
Interrupted
```

Do not mutate historical financial information unnecessarily.

---

# 15. Session Price Snapshot

The session price is frozen when the session starts.

Example:

```text
14:00
Price = 100,000

14:30
Price changes to 150,000

Existing session continues at 100,000.
```

The new price only applies to new sessions.

Never recalculate an active session using a newly changed pricing rule unless the business rules explicitly require it.

---

# 16. Reservation Rules

Reservations can originate from:

- Customer App
- Cashier

Cashier must be able to create and cancel reservations manually.

Reservations synchronized from Cloud remain usable while offline.

Arrival time behavior:

Example:

```text
Reservation:
18:00 → 20:00

Customer arrives:
17:50

Usable session:
17:50 → 19:50
```

If customer arrives late:

```text
Reservation:
18:00 → 20:00

Arrival:
18:20

Usable time:
18:20 → 20:00
```

Unused reserved time is returned to the customer's Wallet according to the financial rules.

All reservation changes must remain auditable.

---

# 17. Customer Management

Cashier must support:

- Register customer
- Login/authentication
- Search customer
- View customer
- View active sessions
- View reservations
- View wallet balance
- Recharge wallet
- View transaction history
- Manage customer operational status

Customer data must be handled according to the minimum necessary access principle.

---

# 18. Wallet

Wallet financial history is immutable.

Do not delete or rewrite historical wallet transactions.

Use transactions such as:

```text
Wallet
└── WalletTransactions
    ├── +500,000  Recharge
    ├── -120,000  Session
    ├── -80,000   Cafe
    └── +80,000   Refund
```

A correction must create a new transaction.

Never change the original transaction to hide the correction.

Every transaction must contain enough information to determine:

- Amount
- Type
- Source
- Related entity
- Actor
- Timestamp
- Unique transaction ID

---

# 19. Payments

Payments are also immutable historical records.

Supported payment concepts include:

- Cash
- Card
- Wallet

A refund/reversal is a new financial event.

Never delete an old payment to represent a refund.

---

# 20. Audit Log

All important operational and financial actions must be auditable.

Audit records should contain:

- Actor
- Actor type
- Action
- Entity
- Entity ID
- Timestamp
- Source
- Before state when appropriate
- After state when appropriate
- Correlation ID
- Device/session information when relevant

Examples:

```text
Employee changed customer wallet
Employee started session
Employee extended session
Employee cancelled reservation
Employee transferred session
Employee changed pricing
Employee registered device
Employee issued refund
```

Financial history must never be silently altered.

---

# 21. Game Management

Cashier manages available games.

Game information may include:

- Game ID
- Name
- Version
- Executable/path information
- Icon
- Active/inactive state
- Supported device type
- Launch configuration

Cashier can select a device and command its Client to launch a game.

The Client is responsible for actually launching the game.

---

# 22. Game Launch

Flow:

```text
Cashier
   ↓
Select Device
   ↓
Select Game
   ↓
Validate Session/Permissions
   ↓
Send Command
   ↓
Client
   ↓
Launch Game
   ↓
Client reports result/status
   ↓
Cashier updates state
```

Cashier must not attempt to launch Windows processes directly on another PC.

---

# 23. Session Transfer

Cashier must support moving a customer/session between supported devices.

Example:

```text
PC #12
   ↓
Transfer
   ↓
PC #20
```

Transfer must:

1. Validate source session.
2. Validate destination device.
3. Preserve financial/session history.
4. Update device assignment.
5. Notify both Clients.
6. Record audit information.

Never create an unrelated second financial session simply because the customer moved devices.

---

# 24. Cashier ↔ Cafe

Cashier and Cafe are separate Windows applications.

They communicate over the local network.

Possible communication includes:

- Customer identification
- Wallet-related operations
- Order information
- Payment status
- Customer/session context
- Device/customer information where required

Cafe remains responsible for cafe-specific operations such as:

- Products
- Inventory
- Orders
- Receipt printing
- Stock receiving

Do not duplicate cafe business logic inside Cashier.

---

# 25. Startup and Recovery

Cashier is normally the first GamePort system started in a GameNet.

Startup must:

1. Initialize local services.
2. Validate local database connectivity.
3. Run required migrations safely.
4. Load configuration.
5. Start local server.
6. Start SignalR/LAN services.
7. Start device monitoring.
8. Start sync engine.
9. Recover interrupted sessions.
10. Resume pending synchronization.
11. Validate local operational state.
12. Start WPF UI.

A crash must not corrupt financial or session data.

Use transactions where required.

---

# 26. Cashier Crash Recovery

If Cashier crashes:

- Local DB must preserve state.
- Clients with active sessions may continue temporarily according to GamePort's global recovery rules.
- When Cashier starts again, it must recover active sessions.
- Clients must reconnect automatically.
- Remaining session time must be reconstructed from persisted state.
- Duplicate session creation must be prevented.

Cashier must not assume that an active session ended simply because the application crashed.

---

# 27. Client Crash Recovery

Cashier should persist enough session information for Client recovery.

If a Client crashes:

```text
Client restarts
   ↓
Reconnects to Cashier
   ↓
Authenticates device
   ↓
Cashier returns active session
   ↓
Client restores session
```

The session should continue based on persisted start/end information.

---

# 28. Security

Cashier is a trusted local system but must not assume that every LAN device is trusted.

Use:

- Device identity
- Authentication
- Authorization
- Secure local communication where appropriate
- Role-based permissions
- Signed/validated commands where appropriate
- Audit logging
- Secret storage
- No plaintext passwords in source code
- No hardcoded production credentials

Customer credentials and employee credentials must be handled separately.

Device identity must be different from customer identity.

---

# 29. Permissions

Cashier should support role-based authorization.

Permissions should be granular enough for operations such as:

- View customers
- Edit customers
- Recharge wallet
- Refund
- Start session
- End session
- Extend session
- Transfer session
- Manage devices
- Manage games
- Manage pricing
- Manage employees
- View financial reports
- Configure GameNet
- Shutdown/restart devices

Do not implement permissions as scattered boolean checks throughout UI code.

Use centralized authorization policies.

---

# 30. UI Architecture

WPF follows MVVM.

Recommended:

```text
View
 ↓
ViewModel
 ↓
Application Use Case
 ↓
Domain
 ↓
Infrastructure
```

Views must not directly access:

- DbContext
- PostgreSQL
- HTTP clients
- SignalR hubs
- File system business logic

ViewModels should remain thin.

Business logic belongs in Application/Domain.

---

# 31. Main Cashier Screens

Initial navigation should include:

### Dashboard

Shows:

- GameNet overview
- Active sessions
- Device states
- Available devices
- Disconnected devices
- Today's revenue summary
- Alerts
- Recent activity

### Sessions

- Active sessions
- Session details
- Start
- End
- Extend
- Transfer
- Device assignment

### Devices

- PCs
- PlayStations
- Device details
- Status
- Connection state
- Remote commands

### Reservations

- Calendar/list
- Create
- Cancel
- Search
- Reservation details

### Customers

- Search
- Customer details
- Wallet
- Sessions
- Reservations
- History

### Wallet / Payments

- Recharge
- Payment history
- Refund/reversal
- Transactions

### Games

- Game list
- Add/edit
- Device support
- Launch configuration

### Pricing

- Pricing rules
- Device type pricing
- Time-based pricing if implemented
- Active/inactive rules

### Reports

- Revenue
- Sessions
- Device usage
- Payments
- Wallet
- Reservations

### Employees

- Employees
- Roles
- Permissions
- Attendance where applicable

### Settings

- GameNet configuration
- Network
- Devices
- Cloud synchronization
- Backup/recovery
- Application settings

---

# 32. Dashboard Rules

Dashboard should prioritize operational information.

The operator should immediately understand:

- How many devices are available
- How many are in use
- Which devices are disconnected
- Which sessions are ending soon
- Upcoming reservations
- Today's operational/financial summary
- Important errors

Do not overload the dashboard with rarely used information.

---

# 33. UI/UX Rules

UI must be:

- Modern
- Premium
- Fast
- Practical
- Responsive
- RTL-first
- Persian-first
- Dark mode
- Light mode

Google Stitch is used for design exploration and UI concepts.

Implementation must preserve the approved visual system.

Avoid unnecessary animations.

Animations must never interfere with:

- Cashier operation
- Responsiveness
- Data visibility
- Keyboard workflows

---

# 34. RTL

The first language is Persian.

The application must be designed RTL-first.

Do not implement RTL by manually reversing every component.

Use WPF's proper FlowDirection and layout mechanisms.

Architecture must not prevent future support for:

- English
- Arabic

All user-facing strings should be localizable.

Do not hardcode UI strings throughout ViewModels and services.

---

# 35. Error Handling

Errors must be categorized.

Examples:

- Validation error
- Business rule violation
- Network error
- Device communication error
- Database error
- Sync error
- Authentication error
- Permission error
- Unexpected system error

Never show raw exceptions to operators.

Operator-facing messages should be understandable.

Technical details must be logged.

---

# 36. Logging

Use structured logging with Serilog.

Logs should contain useful context:

- Timestamp
- Level
- Message
- Exception
- Correlation ID
- Session ID where relevant
- Device ID where relevant
- Employee ID where relevant

Do not log:

- Passwords
- OTPs
- Sensitive authentication secrets
- Card security information

---

# 37. Database Rules

Use EF Core migrations.

Use transactions for operations involving multiple related records.

Examples:

Starting a session may require updating:

- Session
- Device
- Reservation
- Wallet/payment state
- Audit log

These changes must be handled atomically when business rules require it.

Database constraints should enforce important invariants where possible.

Do not rely only on UI validation.

---

# 38. Concurrency

Cashier may receive simultaneous operations.

Examples:

- Operator starts a session.
- Customer reservation exists.
- Another operation targets the same device.

The system must protect against:

- Double booking
- Double session start
- Double wallet charge
- Duplicate payment
- Duplicate synchronization
- Race conditions

Use appropriate:

- Database transactions
- Unique constraints
- Concurrency tokens
- Idempotency keys
- Domain/application validation

---

# 39. Background Services

Cashier may use background workers for:

- Client heartbeat monitoring
- Device state monitoring
- Cloud synchronization
- Outbox processing
- Inbox processing
- Session expiration checks
- Reservation processing
- Health checks
- Cleanup of temporary data
- Automatic recovery tasks

Background services must not directly manipulate WPF UI.

Use appropriate application events/message dispatching for UI updates.

---

# 40. Sync Status

Cashier UI should expose synchronization health.

Operators should be able to understand:

- Online/offline
- Last successful sync
- Pending sync operations
- Failed sync operations
- Sync errors

Sync failure must not prevent normal local operations unless the specific operation fundamentally requires Cloud.

---

# 41. Backups

Local operational data is critical.

Cashier should support a reliable local backup strategy.

Backups must not interfere with active operations.

Backup/restore procedures must be testable.

Do not assume Cloud synchronization alone is a backup.

---

# 42. Auto Update

Cashier will eventually support automatic application updates.

Update mechanism must:

- Be versioned
- Validate downloaded packages
- Avoid corrupting the installation
- Support rollback where practical
- Avoid losing local DB data
- Not silently break compatibility with Clients

Database migrations must be backward/forward compatibility conscious.

---

# 43. Testing

Important business logic must be testable without WPF.

Unit tests should cover:

- Session rules
- Pricing snapshot
- Reservation rules
- Wallet transactions
- Payment rules
- Transfer
- Device state
- Permissions
- Idempotency
- Sync logic

Integration tests should cover:

- PostgreSQL
- EF Core
- Local server
- SignalR
- Sync

End-to-end tests should cover important operator workflows.

---

# 44. Coding Rules

OpenCode must follow these rules:

1. Do not put business logic in WPF Views.
2. Do not put business logic in code-behind unless strictly UI-specific.
3. Do not access DbContext directly from ViewModels.
4. Do not access PostgreSQL directly from UI.
5. Do not create duplicate domain logic in multiple layers.
6. Prefer small focused services.
7. Prefer dependency injection.
8. Use async/await for I/O.
9. Do not block the UI thread.
10. Use cancellation tokens for long-running operations.
11. Validate input before executing operations.
12. Use transactions for financial/state-changing operations where required.
13. Never delete immutable financial history.
14. Never hardcode secrets.
15. Write tests for important business rules.
16. Preserve auditability.
17. Do not introduce unnecessary dependencies.
18. Do not over-engineer before a real requirement exists.

---

# 45. Change Discipline

Before introducing a new architectural dependency, OpenCode must check:

- Does the requirement already exist in this context?
- Does the master architecture already provide a solution?
- Can the existing architecture support it?
- Is the dependency necessary?

Do not introduce:

- New databases
- New messaging systems
- New UI frameworks
- New state-management frameworks
- New communication protocols

without a strong architectural reason.

---

# 46. Definition of Done

A Cashier feature is not considered complete merely because the UI works.

A feature is complete when appropriate:

- UI
- ViewModel
- Application use case
- Domain rules
- Persistence
- Validation
- Authorization
- Audit
- Logging
- Error handling
- Offline behavior
- Synchronization behavior
- Tests

have been considered.

Not every feature requires every item, but OpenCode must consciously evaluate them.

---

# 47. Current Scope

Current supported GameNet devices:

- Gaming PC
- PlayStation

Current Cashier responsibilities:

- Customers
- Sessions
- Reservations
- Devices
- Games
- Wallet
- Payments
- Pricing
- Reports
- Employees/permissions
- Local LAN communication
- Cloud synchronization
- Audit
- Recovery

Do not add unrelated features or device categories.

---

# 48. Implementation Priority

Build Cashier in this order:

### Phase 1 — Foundation

- Solution
- Projects
- Dependency Injection
- Configuration
- Logging
- Database
- EF Core
- Migrations
- Basic WPF shell
- MVVM infrastructure

### Phase 2 — Local Server

- Local API
- SignalR
- Authentication
- Device identity
- Health checks

### Phase 3 — Devices

- Device registration
- Device state
- Heartbeat
- Client connection
- Basic remote commands

### Phase 4 — Customers

- Customer management
- Authentication
- Search
- Profile

### Phase 5 — Sessions

- Start
- End
- Extend
- Transfer
- Recovery
- Pricing snapshot

### Phase 6 — Reservations

- Create
- Cancel
- Arrival
- Early/late rules
- Wallet adjustment

### Phase 7 — Wallet & Payments

- Wallet
- Transactions
- Recharge
- Cash
- Card
- Refund/reversal
- Audit

### Phase 8 — Games

- Game management
- Device-game mapping
- Launch commands

### Phase 9 — Reports & Employees

- Reports
- Roles
- Permissions
- Attendance

### Phase 10 — Sync

- Outbox
- Inbox
- Cursor/checkpoint
- Idempotency
- Cloud synchronization
- Recovery/resume

### Phase 11 — Hardening

- Security
- Testing
- Backup
- Crash recovery
- Performance
- Installer
- Auto-update

---

# 49. Golden Rules

These rules must always be respected:

1. Cashier is the local GameNet operational authority.
2. Cashier must work without Internet.
3. Client normally communicates with Cashier, not Cloud.
4. Cloud is synchronized with Cashier.
5. Financial history is immutable.
6. Corrections are new transactions.
7. Session price is frozen at session start.
8. Device power state and network connection state are separate concepts.
9. Do not confuse `Offline` with `Disconnected`.
10. Do not put business logic in the WPF UI.
11. Never block the UI thread.
12. Do not lose active sessions after crashes.
13. Sync must be resumable and idempotent.
14. Every important operational/financial action must be auditable.
15. Do not introduce unsupported device categories.
16. Do not over-engineer without a real requirement.
17. Prefer correctness and recoverability over cleverness.
18. When uncertain about an implementation detail, preserve the architecture and choose the simplest robust solution.