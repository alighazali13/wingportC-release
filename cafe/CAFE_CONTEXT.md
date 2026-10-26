# GamePort Cafe — Development Context

## 1. Project Overview

GamePort is a professional gaming-cafe management platform.

The system consists of several separate applications:

1. Windows Cashier
2. Windows Client
3. Windows Cafe
4. Customer App
5. Manager PWA

This project is the **GamePort Cafe** Windows desktop application.

GamePort Cafe is responsible for food/beverage operations inside a gaming cafe.

It is NOT the Cashier application.

Its primary responsibilities are:

- Product management
- Product categories
- Order creation
- Order management
- Order status
- Customer association
- Payments
- Receipt printing
- Inventory
- Stock receiving
- Low-stock monitoring
- Cafe revenue
- Order history
- Cafe operational reports

---

# 2. Read These Context Files First

Before making any architectural or implementation decision:

1. Read `../GAMEPORT_CONTEXT.md`
2. Read this `CAFE_CONTEXT.md`

`GAMEPORT_CONTEXT.md` is the global source of truth.

This file contains Cafe-specific rules and implementation details.

If a decision is not specified here, follow the global context and choose the simplest production-ready architecture consistent with the GamePort system.

Do not invent a conflicting architecture.

---

# 3. Core Architecture

GamePort Cafe is a Windows desktop application.

The application follows:

**WPF + .NET 10 + C#**

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

Projects:

```text
GamePort.Cafe
GamePort.Cafe.Domain
GamePort.Cafe.Application
GamePort.Cafe.Infrastructure
GamePort.Cafe.Contracts
```

Tests:

```text
GamePort.Cafe.UnitTests
GamePort.Cafe.IntegrationTests
GamePort.Cafe.EndToEndTests
```

---

# 4. Technology Stack

Use:

- C#
- .NET 10 LTS
- WPF
- MVVM
- CommunityToolkit.Mvvm
- PostgreSQL
- Entity Framework Core
- Npgsql
- FluentValidation
- Serilog
- SignalR where real-time communication is required
- REST where request/response communication is appropriate
- xUnit for testing

Do not introduce another major framework without a clear architectural reason.

---

# 5. Role of Cafe Application

Cafe is a local operational application.

The primary operational environment is the GameNet local network.

The Cafe application must be able to continue essential operations when Internet connectivity is unavailable.

The Internet is NOT required for normal local cafe operations.

The application communicates primarily with the local GamePort environment.

Architecture:

```text
                    CLOUD
                      ↑
                      │
                   Sync
                      │
                 ┌────┴────┐
                 │ CASHIER │
                 │ Local   │
                 │ Server  │
                 └────┬────┘
                      │
                     LAN
                      │
                 ┌────┴────┐
                 │  CAFE   │
                 └─────────┘
```

Cafe should not become an independent competing authority over GamePort operational state.

Cashier remains the primary local operational authority for:

- Customer identity
- Gaming sessions
- Devices
- Wallet
- Gaming payments
- Reservations

Cafe owns cafe-specific operational data such as:

- Products
- Categories
- Orders
- Inventory
- Stock receiving

When data crosses application boundaries, use explicit Contracts/DTOs.

---

# 6. Local-First Requirement

Cafe must be usable when:

- Internet is unavailable
- Cloud is unavailable
- Temporary cloud synchronization is delayed

Local LAN communication should continue to work.

Do not make normal order creation dependent on Cloud availability.

Example:

```text
Customer orders Coca-Cola
        ↓
Cafe creates order locally
        ↓
Payment processed locally
        ↓
Inventory decreases locally
        ↓
Revenue updated locally
        ↓
Later synchronization
        ↓
Cloud
```

---

# 7. Data Ownership

Cafe-specific entities belong to Cafe.

Expected entities include:

```text
Product
ProductCategory
Order
OrderItem
InventoryItem
StockReceipt
StockReceiptItem
Supplier
CafePayment
```

Additional entities may be introduced when justified.

Do not duplicate ownership of entities that belong to Cashier.

For example:

Customer belongs to the GamePort customer domain.

Cafe may reference a CustomerId but should not create a second independent Customer entity containing duplicated customer information.

---

# 8. Product Management

Products should support:

- Name
- Description
- Category
- Sale price
- Purchase price where appropriate
- Current inventory
- Minimum inventory threshold
- Active/inactive state
- SKU/code where useful
- Image reference where useful
- Creation/update timestamps

Products can be disabled instead of deleted when historical orders reference them.

Historical orders must remain valid even if a product is later disabled.

---

# 9. Product Categories

Categories organize products.

Examples:

```text
نوشیدنی
قهوه
غذا
تنقلات
دسر
سایر
```

Categories should support:

- Name
- Sort order
- Active/inactive
- Creation/update timestamps

Ordering UI should respect category sort order.

---

# 10. Orders

Order is the core Cafe operational entity.

An order should contain:

- OrderId
- Order number
- CustomerId (optional)
- Operator/employee
- Items
- Subtotal
- Discount where supported
- Final amount
- Payment status
- Order status
- CreatedAt
- UpdatedAt
- CompletedAt where applicable

Order items should preserve historical pricing.

If a product costs:

```text
100,000 تومان
```

when the order is created, the OrderItem must store that effective unit price.

Later changing the Product price must NOT change historical orders.

---

# 11. Order Status

Supported order states:

```text
New
Preparing
Ready
Delivered
Completed
Cancelled
```

The UI should display Persian labels:

```text
جدید
در حال آماده‌سازی
آماده
تحویل داده شد
تکمیل شد
لغو شد
```

State transitions should be explicit.

Do not allow arbitrary invalid transitions.

---

# 12. Order Creation

Order creation must be optimized for speed.

Typical workflow:

```text
New Order
    ↓
Select Category
    ↓
Select Product
    ↓
Add Quantity
    ↓
Review Cart
    ↓
Confirm Order
    ↓
Payment
    ↓
Inventory Update
    ↓
Receipt
```

The operator should be able to add products with minimal clicks.

The order/cart should remain visible while browsing products.

---

# 13. Inventory

Inventory is owned by Cafe.

Inventory changes must be traceable.

Typical inventory events:

```text
Stock Received
Sale
Correction
Return
Adjustment
```

Do not silently mutate inventory without recording why the quantity changed.

Whenever practical, inventory should be based on an auditable transaction/history model.

Example:

```text
+50 Stock Received
-2  Sale
-1  Sale
+1  Correction
```

Current stock can be derived/maintained from these operations.

---

# 14. Stock Receiving

Cafe staff can receive new inventory.

Example:

```text
نوشابه
Quantity: 48
Purchase price: 28,000 تومان
```

Receiving stock should:

1. Create a stock receipt
2. Create receipt items
3. Increase inventory
4. Record purchase information
5. Record operator
6. Record timestamp
7. Create an audit trail

The operation should be transactional.

Partial completion must not leave inventory inconsistent.

---

# 15. Low Stock

Each relevant product can have a minimum stock threshold.

Example:

```text
ردبول
موجودی: 4
حداقل: 10
```

The Cafe dashboard should clearly show low-stock products.

Do not make low-stock detection dependent on Cloud.

It must work locally.

---

# 16. Payments

Cafe payments are separate from gaming session payments, but should integrate with the GamePort payment architecture.

Supported methods may include:

```text
Card
Cash
Customer Wallet
```

For wallet payments, Cafe requests/uses the appropriate Cashier-controlled wallet operation rather than maintaining an independent wallet balance.

Payment records must be auditable.

Financial records should be immutable.

Never silently modify or delete historical payment records.

Corrections/refunds should create new compensating transactions/events.

---

# 17. Wallet Integration

Customer wallet belongs to the GamePort customer/payment domain.

Cafe should reference:

```text
CustomerId
```

and request wallet operations through the appropriate GamePort/Cashier contract.

Do NOT create:

```text
CafeWallet
```

as an independent balance system.

Example:

```text
Cafe
  ↓
Request wallet payment
  ↓
Cashier
  ↓
Wallet transaction
  ↓
Payment result
  ↓
Cafe Order
```

The exact transport can be REST, SignalR command, or another local contract depending on the final shared architecture.

---

# 18. Customer Association

An order may be:

```text
Anonymous / walk-in
```

or associated with a GamePort customer.

Cafe should not duplicate customer authentication.

If customer information is needed, request the minimum necessary data through Cashier/local contracts.

---

# 19. Cashier Communication

Cashier is the local GamePort authority.

Cafe may need to communicate with Cashier for:

- Customer lookup
- Customer identification
- Wallet payment
- Customer wallet status
- Shared operator/employee context
- Relevant financial synchronization
- Local GamePort status

Use explicit Contracts.

Do not directly access Cashier's database.

Do not create a shared database connection between Cafe and Cashier.

Applications communicate through defined interfaces/protocols.

---

# 20. Real-Time Communication

Use SignalR or an equivalent appropriate mechanism where real-time communication is required.

Potential examples:

- Cashier status
- Wallet payment result
- Order-related notifications
- Operational events
- Device/customer-related context when needed

Do not use real-time communication for data that does not require real-time behavior.

---

# 21. Synchronization

Cafe participates in GamePort synchronization.

Synchronization must be:

- Resumable
- Idempotent
- Reliable
- Auditable

Use the GamePort global sync strategy:

```text
Outbox
Inbox
Cursor / Checkpoint
Idempotency
```

Example:

```text
100 events
67 synced
68-100 pending

Connection returns
        ↓
Resume from event 68
```

Never assume synchronization always succeeds in one attempt.

Do not duplicate orders, payments, inventory movements, or other financial events during retries.

---

# 22. Financial Integrity

Financial and inventory operations must be treated as high-integrity operations.

Important principles:

- No silent mutation of historical financial records
- No hard deletion of financially relevant history
- Corrections should be explicit
- Refunds should be explicit
- Every financial action should be auditable
- Use database transactions for multi-step operations

Example:

```text
Order
Payment
Inventory decrement
Audit event
```

must not leave the system half-completed.

---

# 23. Audit Logging

Important actions should be logged.

Audit information should include where applicable:

- Actor
- Action
- Entity
- EntityId
- Timestamp
- Before state
- After state
- Source
- CorrelationId
- Related order/payment/inventory identifier

Examples:

```text
ProductCreated
ProductUpdated
OrderCreated
OrderCancelled
PaymentCreated
PaymentRefunded
InventoryAdjusted
StockReceived
```

Do not log sensitive information unnecessarily.

---

# 24. UI/UX Direction

The UI must match the GamePort Cashier visual language.

Design goals:

- Premium
- Modern
- Professional
- Desktop-first
- Fast
- Clean
- High information density
- Not generic enterprise software

Visual inspiration:

Modern macOS desktop applications.

Do NOT literally copy Apple UI.

---

# 25. RTL

Primary UI language:

Persian

The application must use true RTL.

Sidebar:

**RIGHT**

Content:

RTL

Header:

RTL-aware

Icons associated with Persian labels:

Immediately to the right of the label.

Do not fake RTL by only changing text alignment.

---

# 26. Theme

Support:

- Dark mode
- Light mode

Dark mode is the primary design direction.

Use:

- Deep charcoal backgrounds
- Layered surfaces
- Subtle borders
- Soft shadows
- Restrained accent color
- Refined corner radius

Avoid:

- Neon gaming aesthetics
- Excessive gradients
- Excessive glassmorphism
- Heavy shadows
- Huge colorful cards
- Old POS software styling

---

# 27. Cafe Navigation

Main navigation:

```text
داشبورد
سفارش‌ها
ثبت سفارش
محصولات
موجودی
دریافت کالا
گزارش‌ها
تنظیمات
```

Potential future sections may be added without breaking the navigation architecture.

---

# 28. Dashboard

The Cafe dashboard should focus on operations.

Primary KPI cards:

```text
سفارش‌های امروز
درآمد امروز
سفارش‌های باز
محصولات کم‌موجودی
```

Main operational sections:

- Active orders
- Quick order
- Low-stock products
- Recent activity
- Relevant alerts

Do not create an analytics-heavy dashboard.

---

# 29. Order Screen

Order management should support:

- Search
- Filtering
- Status
- Date
- Customer
- Operator
- Payment status

Show active orders prominently.

Operators should be able to:

- Open
- Edit where allowed
- Change status
- Pay
- Print receipt
- Cancel where permitted

---

# 30. Quick Order Screen

This is a high-priority screen.

Structure:

```text
┌──────────────────────────────┬──────────────────┐
│                              │                  │
│ Categories + Products        │ Current Order    │
│                              │                  │
│                              │                  │
│                              │                  │
└──────────────────────────────┴──────────────────┘
```

The cart should remain visible while selecting products.

Product cards should show:

- Name
- Price
- Stock
- Optional image/icon

Quantity changes should be instant.

---

# 31. Reports

Reports should include relevant Cafe data:

- Daily revenue
- Weekly revenue
- Monthly revenue
- Product sales
- Category sales
- Popular products
- Payment methods
- Inventory status
- Low-stock items

Do not expose sensitive Cashier-only financial information unless explicitly authorized by the GamePort architecture.

---

# 32. Permissions

The application should support permission checks.

Potential roles:

```text
Cafe Operator
Cafe Manager
Administrator
```

Do not hard-code role names into business logic where avoidable.

Permissions should be represented explicitly.

Examples:

```text
Orders.View
Orders.Create
Orders.Cancel
Payments.Create
Payments.Refund
Products.View
Products.Manage
Inventory.View
Inventory.Adjust
Stock.Receive
Reports.View
```

Financially sensitive operations should require appropriate permission.

---

# 33. Security

Follow secure application practices.

Never:

- Store passwords in plaintext
- Store secrets in source code
- Trust client input
- Bypass permission checks
- Log sensitive credentials

Local communication should use authenticated application/device identity where required.

---

# 34. Project Structure

Recommended structure:

```text
cafe/
├── GamePort.Cafe.sln
├── CAFE_CONTEXT.md
│
├── src/
│   ├── GamePort.Cafe/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Services/
│   │   ├── Helpers/
│   │   └── Resources/
│   │
│   ├── GamePort.Cafe.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   ├── ValueObjects/
│   │   └── Interfaces/
│   │
│   ├── GamePort.Cafe.Application/
│   │   ├── Interfaces/
│   │   ├── Services/
│   │   ├── DTOs/
│   │   ├── Mappings/
│   │   └── Validation/
│   │
│   ├── GamePort.Cafe.Infrastructure/
│   │   ├── Data/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   └── Extensions/
│   │
│   └── GamePort.Cafe.Contracts/
│       ├── Orders/
│       ├── Products/
│       ├── Inventory/
│       ├── Payments/
│       ├── Customers/
│       └── Common/
│
└── tests/
    ├── GamePort.Cafe.UnitTests/
    ├── GamePort.Cafe.IntegrationTests/
    └── GamePort.Cafe.EndToEndTests/
```

Do not create unnecessary folders before they are needed.

---

# 35. Repository Pattern

Do NOT create a generic:

```text
IRepository<T>
```

Use specialized repositories/query services where persistence abstraction is actually needed.

Examples:

```text
IProductRepository
IProductCategoryRepository
IOrderRepository
IInventoryRepository
IStockReceiptRepository
ICafePaymentRepository
```

Repositories should reflect real application use cases.

---

# 36. Domain Rules

Business rules belong in the Domain/Application layers, not in WPF code-behind.

Do not put important business logic inside:

```text
.xaml.cs
```

ViewModels should orchestrate UI state and invoke application services.

Domain should have no dependency on:

- WPF
- PostgreSQL
- EF Core
- SignalR
- HTTP

---

# 37. EF Core

Use EF Core for persistence.

Use:

- Explicit configurations
- Migrations
- Proper indexes
- Foreign keys
- Transactions
- Concurrency handling where appropriate

Avoid an uncontrolled giant DbContext configuration.

Keep database concerns inside Infrastructure.

---

# 38. WPF Architecture

Use MVVM.

Prefer:

```text
View
 ↓
ViewModel
 ↓
Application Service
 ↓
Domain
```

Avoid business logic in code-behind.

Use:

- CommunityToolkit.Mvvm
- ObservableObject
- RelayCommand / AsyncRelayCommand
- Dependency Injection

---

# 39. UI Component Philosophy

Build reusable components.

Examples:

```text
AppShell
Sidebar
Header
KpiCard
ProductCard
OrderCard
OrderList
StatusBadge
SearchField
FilterBar
CategoryTabs
QuantityControl
PaymentDialog
ConfirmationDialog
Toast
EmptyState
LoadingState
ConnectionStatus
SyncStatus
```

Do not duplicate styling across every screen.

---

# 40. Error Handling

Errors must be user-friendly.

Never expose raw exceptions to operators.

Example:

Bad:

```text
NpgsqlException: 23505 duplicate key...
```

Good:

```text
ثبت سفارش انجام نشد.

اطلاعات سفارش تکراری است.
لطفاً دوباره تلاش کنید.
```

Detailed technical errors belong in logs.

---

# 41. Logging

Use Serilog.

Log:

- Application startup/shutdown
- Database errors
- Communication failures
- Sync failures
- Important operational events
- Unexpected exceptions

Logs must not contain passwords, authentication tokens, or unnecessary sensitive information.

---

# 42. Testing

Write tests for important business rules.

Priority:

### Unit Tests

- Order calculations
- Quantity rules
- Price snapshot
- Inventory calculations
- Status transitions
- Payment rules

### Integration Tests

- PostgreSQL
- EF Core
- Transactions
- Persistence
- Communication contracts

### End-to-End Tests

Important complete workflows:

```text
Create Order
→ Pay
→ Decrease Inventory
→ Complete Order
```

and:

```text
Receive Stock
→ Increase Inventory
→ Verify Low Stock State
```

---

# 43. Performance

The Cafe app may process many orders during busy periods.

Optimize for:

- Fast product search
- Fast category switching
- Fast order creation
- Minimal UI blocking
- Async database operations
- Efficient queries

Never block the WPF UI thread with long-running operations.

---

# 44. Offline Behavior

If Internet is unavailable:

The operator should still be able to perform normal local operations.

The UI should clearly distinguish:

```text
شبکه محلی: متصل
صندوق: متصل
Cloud: بدون اینترنت
```

Do not show the whole application as unavailable just because Cloud is unavailable.

If Cashier itself becomes unreachable, show:

```text
ارتباط با صندوق قطع است
```

and preserve whatever local Cafe functionality is safe to continue.

Do not silently perform operations that require Cashier authority when Cashier is unavailable.

---

# 45. Startup

On application startup:

1. Load configuration
2. Initialize logging
3. Initialize dependency injection
4. Initialize local database
5. Validate required configuration
6. Initialize local services
7. Establish LAN/Cashier communication
8. Initialize synchronization
9. Load shell
10. Show connection/sync state

Startup failures should be handled gracefully.

---

# 46. Important Architectural Boundaries

Never:

- Access another application's database directly
- Put business logic in WPF Views
- Make Cafe dependent on Cloud for normal local operations
- Duplicate Customer wallet state
- Mutate historical financial records
- Delete historical orders that are financially relevant
- Put secrets in source code
- Create generic abstractions without a real need
- Over-engineer before the requirement exists

---

# 47. Implementation Strategy

Build Cafe incrementally.

Recommended order:

### Phase 1 — Foundation

- Solution
- Projects
- Dependency Injection
- Configuration
- Logging
- Base WPF shell
- Theme system
- RTL
- Navigation

### Phase 2 — Local Data

- PostgreSQL
- EF Core
- DbContext
- Migrations
- Product
- Category

### Phase 3 — Products

- Product management
- Categories
- Search
- Product activation/deactivation

### Phase 4 — Orders

- Order creation
- Product selection
- Cart
- Quantity management
- Order persistence
- Order status

### Phase 5 — Payments

- Payment methods
- Payment records
- Wallet integration contract
- Receipt

### Phase 6 — Inventory

- Inventory
- Stock receiving
- Inventory transactions
- Low-stock detection

### Phase 7 — Reports

- Revenue
- Product sales
- Category sales
- Order history

### Phase 8 — Communication

- Cashier communication
- SignalR where required
- Contracts
- Connection state

### Phase 9 — Synchronization

- Outbox
- Inbox
- Cursor/checkpoint
- Idempotency
- Retry

### Phase 10 — Hardening

- Permissions
- Audit
- Error handling
- Performance
- Tests
- Installer/update readiness

---

# 48. OpenCode Instructions

You are working as the implementation agent for GamePort Cafe.

Before modifying code:

1. Read `../GAMEPORT_CONTEXT.md`
2. Read `CAFE_CONTEXT.md`
3. Inspect the existing project structure
4. Understand existing code before changing it
5. Reuse existing abstractions when appropriate
6. Do not rewrite working architecture without a reason

When implementing a feature:

1. Explain/identify the architectural impact internally
2. Keep Domain independent
3. Keep UI logic in ViewModels
4. Keep persistence in Infrastructure
5. Keep communication DTOs in Contracts
6. Add validation
7. Add appropriate tests
8. Update relevant documentation when architecture changes

Do not create large amounts of speculative code.

Prefer small, coherent, testable changes.

---

# 49. UI Implementation Rule

The UI concept created in Google Stitch is the visual reference.

When implementing it:

- Preserve the intended layout
- Preserve RTL geometry
- Preserve right-side sidebar
- Preserve information hierarchy
- Preserve dark/light themes
- Preserve component consistency

Do not replace the design with generic WPF controls just because they are easier.

Create reusable WPF components/styles where necessary.

---

# 50. Golden Rules

1. Cashier remains the local GamePort operational authority.
2. Cafe owns cafe operations.
3. Cloud is not required for normal local Cafe operation.
4. Never directly access another application's database.
5. Financial records are immutable.
6. Inventory changes must be traceable.
7. Historical order prices must never change because of later product-price changes.
8. Customer wallet belongs to the shared GamePort wallet domain.
9. Sync must be resumable and idempotent.
10. Business logic does not belong in WPF code-behind.
11. Domain must remain independent from infrastructure/UI.
12. Use specialized repositories instead of generic repositories.
13. True RTL is mandatory.
14. UI should be fast for cafe operators.
15. Do not over-engineer.
16. Do not introduce unnecessary dependencies.
17. Test important business rules.
18. Never sacrifice data integrity for UI convenience.
19. Prefer explicit contracts between applications.
20. When uncertain, choose the simplest solution consistent with the global GamePort architecture.