using System.Text.Json;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Application.Security;
using GamePort.Cashier.Application.UseCases.Attendance;
using GamePort.Cashier.Application.UseCases.Auth;
using GamePort.Cashier.Application.UseCases.Customers;
using GamePort.Cashier.Application.UseCases.Devices;
using GamePort.Cashier.Application.UseCases.Employees;
using GamePort.Cashier.Application.UseCases.Games;
using GamePort.Cashier.Application.UseCases.Reports;
using GamePort.Cashier.Application.UseCases.Reservations;
using GamePort.Cashier.Application.UseCases.Sessions;
using GamePort.Cashier.Application.UseCases.Sync;
using GamePort.Cashier.Application.UseCases.Wallets;
using GamePort.Cashier.Contracts.Common;
using GamePort.Cashier.Contracts.Hub;
using GamePort.Cashier.Contracts.Requests;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace GamePort.Cashier.Infrastructure.Hosting;

public static class CashierEndpoints
{
    public static void MapCashierApi(this IEndpointRouteBuilder endpoints)
    {
        MapAuth(endpoints);
        MapDevices(endpoints);
        MapCustomers(endpoints);
        MapSessions(endpoints);
        MapWallets(endpoints);
        MapReservations(endpoints);
        MapGames(endpoints);
        MapPricing(endpoints);
        MapEmployees(endpoints);
        MapAttendance(endpoints);
        MapSystem(endpoints);
    }

    private static void MapAuth(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new LoginCommand(request.Username, request.Password), ct);

            return result.IsSuccess
                ? Ok(new LoginResponse
                {
                    Token = result.Value!.Token,
                    ExpiresAt = result.Value.ExpiresAt,
                    EmployeeId = result.Value.EmployeeId,
                    Name = result.Value.Name,
                    Username = result.Value.Username,
                    Role = result.Value.Role
                })
                : Fail(result.Error!, StatusCodes.Status401Unauthorized);
        }).AllowAnonymous().RequireRateLimiting("login").WithValidation<LoginRequest>();

        group.MapPost("/change-password", async (ChangePasswordRequest request, ChangePasswordHandler handler, HttpContext httpContext, CancellationToken ct) =>
        {
            var employeeId = GetEmployeeId(httpContext);
            if (employeeId is null)
            {
                return Fail("ابتدا وارد شوید.", StatusCodes.Status401Unauthorized);
            }

            var result = await handler.HandleAsync(
                new ChangePasswordCommand(employeeId.Value, request.CurrentPassword, request.NewPassword), ct);

            return result.IsSuccess ? Ok(new { Changed = true }) : Fail(result.Error!);
        }).RequireAuthorization().WithValidation<ChangePasswordRequest>();

        group.MapGet("/me", (HttpContext httpContext) => Ok(new
        {
            EmployeeId = GetEmployeeId(httpContext),
            Name = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
            Username = httpContext.User.FindFirst("unique_name")?.Value,
            Role = httpContext.User.FindFirst("role")?.Value,
            Permissions = httpContext.User.FindAll("perm").Select(c => c.Value).ToList()
        })).RequireAuthorization();
    }

    private static void MapDevices(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/devices");

        group.MapGet("", async (IDeviceRepository devices) =>
        {
            var all = await devices.GetAllAsync();
            return Ok(all.Select(d => d.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.DevicesView);

        group.MapGet("/{id:guid}", async (Guid id, IDeviceRepository devices) =>
        {
            var device = await devices.GetByIdAsync(id);
            return device is null ? NotFound("دستگاه یافت نشد.") : Ok(device.ToResponse());
        }).RequireAuthorization(Permissions.DevicesView);

        group.MapPost("", async (RegisterDeviceRequest request, RegisterDeviceHandler handler, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DeviceType>(request.Type, true, out var type))
            {
                return Fail("نوع دستگاه نامعتبر است.");
            }

            var result = await handler.HandleAsync(
                new RegisterDeviceCommand(request.Name, type, request.IpAddress, request.MacAddress, request.HardwareInfo), ct);

            return result.IsSuccess
                ? Ok(result.Value!.Device.ToRegisteredResponse(result.Value.ClientSecret))
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.DevicesManage).WithValidation<RegisterDeviceRequest>();

        group.MapDelete("/{id:guid}", async (Guid id, IDeviceRepository devices) =>
        {
            var device = await devices.GetByIdAsync(id);
            if (device is null)
            {
                return NotFound("دستگاه یافت نشد.");
            }

            await devices.DeleteAsync(id);
            return Ok(new { id });
        }).RequireAuthorization(Permissions.DevicesManage);

        group.MapPost("/{id:guid}/commands", async (
            Guid id,
            SendDeviceCommandRequest request,
            IDeviceRepository devices,
            IDeviceCommandSender commandSender,
            IAuditLogRepository auditLogs,
            CancellationToken ct) =>
        {
            var device = await devices.GetByIdAsync(id);
            if (device is null)
            {
                return NotFound("دستگاه یافت نشد.");
            }

            if (!Enum.TryParse<DeviceCommandType>(request.CommandType, true, out var commandType))
            {
                return Fail("نوع فرمان نامعتبر است.");
            }

            var command = new DeviceCommandMessage
            {
                DeviceId = id,
                CommandType = commandType,
                Payload = request.Payload
            };

            var delivered = await commandSender.SendAsync(id, command, ct);

            await auditLogs.AddAsync(new AuditLog
            {
                Action = "SendDeviceCommand",
                EntityType = nameof(Device),
                EntityId = id,
                ActorType = AuditActorType.Employee,
                Source = "Cashier",
                DeviceId = id,
                AfterState = JsonSerializer.Serialize(new { command.CommandType, Delivered = delivered })
            });

            return delivered
                ? Ok(new { command.CommandId, Delivered = true })
                : Fail("دستگاه متصل نیست؛ فرمان ارسال نشد.", StatusCodes.Status409Conflict);
        }).RequireAuthorization(Permissions.DevicesCommand);
    }

    private static void MapCustomers(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/customers");

        group.MapGet("", async (ICustomerRepository customers, string? query) =>
        {
            var result = string.IsNullOrWhiteSpace(query)
                ? await customers.GetAllAsync()
                : await customers.SearchAsync(query.Trim());

            return Ok(result.Select(c => c.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.CustomersView);

        group.MapGet("/{id:guid}", async (Guid id, ICustomerRepository customers, IWalletRepository wallets) =>
        {
            var customer = await customers.GetByIdAsync(id);
            if (customer is null)
            {
                return NotFound("مشتری یافت نشد.");
            }

            var wallet = await wallets.GetByCustomerIdAsync(id);
            return Ok(customer.ToResponse(wallet?.Balance));
        }).RequireAuthorization(Permissions.CustomersView);

        group.MapPost("", async (RegisterCustomerRequest request, RegisterCustomerHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new RegisterCustomerCommand(request.Name, request.PhoneNumber), ct);

            return result.IsSuccess
                ? Ok(result.Value!.Customer.ToResponse(result.Value.Wallet.Balance))
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.CustomersEdit).WithValidation<RegisterCustomerRequest>();
    }

    private static void MapSessions(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sessions");

        group.MapGet("/active", async (ISessionRepository sessions) =>
        {
            var active = await sessions.GetActiveSessionsAsync();
            return Ok(active.Select(s => s.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.SessionsView);

        group.MapGet("/{id:guid}", async (Guid id, ISessionRepository sessions) =>
        {
            var session = await sessions.GetByIdAsync(id);
            return session is null ? NotFound("نشست یافت نشد.") : Ok(session.ToResponse());
        }).RequireAuthorization(Permissions.SessionsView);

        group.MapPost("", async (StartSessionRequest request, StartSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new StartSessionCommand(request.CustomerId, request.DeviceId, request.PlannedEndTime, "Cashier"), ct);

            if (!result.IsSuccess)
            {
                return Fail(result.Error!);
            }

            var value = result.Value!;
            return Ok(new SessionResponse
            {
                Id = value.SessionId,
                CustomerId = value.CustomerId,
                DeviceId = value.DeviceId,
                StartTime = value.StartTime,
                PlannedEndTime = value.PlannedEndTime,
                PriceAtStart = value.PriceAtStart,
                Status = nameof(SessionStatus.Active)
            });
        }).RequireAuthorization(Permissions.SessionsStart).WithValidation<StartSessionRequest>();

        group.MapPost("/end", async (EndSessionRequest request, EndSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new EndSessionCommand(request.SessionId, request.Reason, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.ActualEndTime, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsEnd);

        group.MapPost("/extend", async (ExtendSessionRequest request, ExtendSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new ExtendSessionCommand(request.SessionId, request.NewPlannedEndTime, request.AdditionalMinutes, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.PlannedEndTime, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsExtend);

        group.MapPost("/pause", async (SessionIdRequest request, PauseSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new PauseSessionCommand(request.SessionId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.PausedAt, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsPause);

        group.MapPost("/resume", async (SessionIdRequest request, ResumeSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ResumeSessionCommand(request.SessionId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.TotalPausedSeconds, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsPause);

        group.MapPost("/transfer", async (TransferSessionRequest request, TransferSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new TransferSessionCommand(request.SessionId, request.TargetDeviceId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.FromDeviceId, result.Value.ToDeviceId, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsTransfer);

        group.MapPost("/cancel", async (CancelSessionRequest request, CancelSessionHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new CancelSessionCommand(request.SessionId, request.Reason, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.SessionId, result.Value.ActualEndTime, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.SessionsEnd);
    }

    private static void MapWallets(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/wallets");

        group.MapGet("/{customerId:guid}", async (Guid customerId, IWalletRepository wallets) =>
        {
            var wallet = await wallets.GetByCustomerIdAsync(customerId);
            return wallet is null ? NotFound("کیف پول یافت نشد.") : Ok(wallet.ToResponse());
        }).RequireAuthorization(Permissions.CustomersView);

        group.MapGet("/{customerId:guid}/transactions", async (Guid customerId, IWalletRepository wallets, int? limit) =>
        {
            var wallet = await wallets.GetByCustomerIdAsync(customerId);
            if (wallet is null)
            {
                return NotFound("کیف پول یافت نشد.");
            }

            var transactions = await wallets.GetTransactionsAsync(wallet.Id, limit is > 0 and <= 500 ? limit.Value : 50);
            return Ok(transactions.Select(t => t.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.CustomersView);

        group.MapPost("/recharge", async (RechargeWalletRequest request, RechargeWalletHandler handler, CancellationToken ct) =>
        {
            if (!Enum.TryParse<PaymentMethod>(request.Method, true, out var method))
            {
                return Fail("روش پرداخت نامعتبر است.");
            }

            var result = await handler.HandleAsync(
                new RechargeWalletCommand(request.CustomerId, request.Amount, method, "Cashier", request.IdempotencyKey), ct);

            return result.IsSuccess
                ? Ok(new
                {
                    result.Value!.WalletId,
                    result.Value.Balance,
                    result.Value.PaymentId,
                    result.Value.AlreadyApplied
                })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.WalletRecharge).WithValidation<RechargeWalletRequest>();
    }

    private static void MapReservations(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/reservations");

        group.MapGet("", async (IReservationRepository reservations, DateTime? date) =>
        {
            var result = date.HasValue
                ? await reservations.GetByDateAsync(date.Value)
                : await reservations.GetAllAsync();

            return Ok(result.Select(r => r.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.ReservationsView);

        group.MapGet("/upcoming", async (IReservationRepository reservations) =>
        {
            var result = await reservations.GetUpcomingAsync(DateTime.UtcNow);
            return Ok(result.Select(r => r.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.ReservationsView);

        group.MapGet("/{id:guid}", async (Guid id, IReservationRepository reservations) =>
        {
            var reservation = await reservations.GetByIdAsync(id);
            return reservation is null ? NotFound("رزرو یافت نشد.") : Ok(reservation.ToResponse());
        }).RequireAuthorization(Permissions.ReservationsView);

        group.MapPost("", async (CreateReservationRequest request, CreateReservationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new CreateReservationCommand(
                request.CustomerId, request.DeviceId, request.StartTime, request.EndTime,
                request.Notes, "Cashier", "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new
                {
                    result.Value!.ReservationId,
                    result.Value.CustomerId,
                    result.Value.DeviceId,
                    result.Value.StartTime,
                    result.Value.EndTime,
                    result.Value.Status
                })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.ReservationsManage).WithValidation<CreateReservationRequest>();

        group.MapPost("/cancel", async (ReservationIdRequest request, CancelReservationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new CancelReservationCommand(request.ReservationId, request.Reason, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.ReservationId, result.Value.Status })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.ReservationsManage);

        group.MapPost("/arrive", async (ReservationIdRequest request, ArriveReservationHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ArriveReservationCommand(request.ReservationId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new
                {
                    result.Value!.ReservationId,
                    result.Value.SessionId,
                    result.Value.StartTime,
                    result.Value.PlannedEndTime,
                    result.Value.WasEarly
                })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.ReservationsManage);
    }

    private static void MapGames(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/games");

        group.MapGet("", async (IGameRepository games) =>
        {
            var all = await games.GetAllAsync();
            return Ok(all.Select(g => g.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.GamesView);

        group.MapGet("/{id:guid}", async (Guid id, IGameRepository games) =>
        {
            var game = await games.GetByIdAsync(id);
            return game is null ? NotFound("بازی یافت نشد.") : Ok(game.ToResponse());
        }).RequireAuthorization(Permissions.GamesView);

        group.MapPost("", async (CreateGameRequest request, CreateGameHandler handler, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DeviceType>(request.SupportedDeviceType, true, out var deviceType))
            {
                return Fail("نوع دستگاه نامعتبر است.");
            }

            var result = await handler.HandleAsync(new CreateGameCommand(
                request.Name, request.Version, request.ExecutablePath, request.IconPath,
                deviceType, request.LaunchConfiguration, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.GameId, result.Value.Name, result.Value.IsActive })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesManage);

        group.MapPut("", async (UpdateGameRequest request, UpdateGameHandler handler, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DeviceType>(request.SupportedDeviceType, true, out var deviceType))
            {
                return Fail("نوع دستگاه نامعتبر است.");
            }

            var result = await handler.HandleAsync(new UpdateGameCommand(
                request.GameId, request.Name, request.Version, request.ExecutablePath, request.IconPath,
                deviceType, request.LaunchConfiguration, request.IsActive, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.GameId, result.Value.Name, result.Value.IsActive })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesManage);

        group.MapDelete("/{id:guid}", async (Guid id, DeleteGameHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new DeleteGameCommand(id, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.GameId, result.Value.Name, result.Value.IsActive })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesManage).WithValidation<CreateGameRequest>();

        group.MapGet("/devices/{id:guid}", async (Guid id, IDeviceGameRepository deviceGames) =>
        {
            var mappings = await deviceGames.GetByDeviceAsync(id);
            return Ok(mappings.Select(m => m.ToResponse()).ToList());
        }).RequireAuthorization(Permissions.GamesView);

        group.MapPost("/devices/{id:guid}", async (Guid id, AssignGameToDeviceRequest request, AssignGameToDeviceHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new AssignGameToDeviceCommand(
                id, request.GameId, request.IsInstalled, request.InstalledVersion, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.DeviceId, result.Value.GameId, result.Value.GameName, result.Value.IsInstalled })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesManage);

        group.MapDelete("/devices/{id:guid}/{gameId:guid}", async (Guid id, Guid gameId, UnassignGameFromDeviceHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new UnassignGameFromDeviceCommand(id, gameId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.DeviceId, result.Value.GameId })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesManage);

        group.MapPost("/launch", async (LaunchGameRequest request, LaunchGameHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new LaunchGameCommand(request.DeviceId, request.GameId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new
                {
                    result.Value!.DeviceId,
                    result.Value.GameId,
                    result.Value.CommandId,
                    result.Value.Delivered
                })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesLaunch);

        group.MapPost("/close", async (DeviceIdRequest request, CloseGameHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new CloseGameCommand(request.DeviceId, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.DeviceId, result.Value.CommandId, result.Value.Delivered })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.GamesLaunch);
    }

    private static void MapPricing(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/pricing");

        group.MapGet("", async (IPricingRuleRepository pricing) =>
        {
            var rules = await pricing.GetAllAsync();
            return Ok(rules.Select(r => new
            {
                r.Id,
                r.Name,
                DeviceType = r.DeviceType.ToString(),
                r.PricePerHour,
                r.IsActive,
                r.EffectiveFrom
            }).ToList());
        }).RequireAuthorization(Permissions.PricingView);

        group.MapPost("", async (CreatePricingRuleRequest request, IPricingRuleRepository pricing, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DeviceType>(request.DeviceType, true, out var type))
            {
                return Fail("نوع دستگاه نامعتبر است.");
            }

            if (request.PricePerHour <= 0)
            {
                return Fail("نرخ ساعتی باید بزرگ‌تر از صفر باشد.");
            }

            var rule = await pricing.AddAsync(new PricingRule
            {
                Name = request.Name,
                DeviceType = type,
                PricePerHour = request.PricePerHour,
                IsActive = true,
                EffectiveFrom = DateTime.UtcNow
            });

            return Ok(new { rule.Id, rule.Name, DeviceType = rule.DeviceType.ToString(), rule.PricePerHour, rule.IsActive });
        }).RequireAuthorization(Permissions.PricingManage).WithValidation<CreatePricingRuleRequest>();
    }

    private static void MapEmployees(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/employees").RequireAuthorization(Permissions.EmployeesManage);

        group.MapGet("", async (IEmployeeRepository employees) =>
        {
            var all = await employees.GetAllAsync();
            return Ok(all.Select(e => new
            {
                e.Id,
                e.Name,
                e.Username,
                e.Role,
                e.IsActive,
                e.LastLoginAt,
                e.CreatedAt
            }).ToList());
        });

        group.MapPost("", async (CreateEmployeeRequest request, CreateEmployeeHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new CreateEmployeeCommand(
                request.Name, request.Username, request.Password, request.Role, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.EmployeeId, result.Value.Name, result.Value.Username, result.Value.Role, result.Value.IsActive })
                : Fail(result.Error!);
        }).WithValidation<CreateEmployeeRequest>();

        group.MapPost("/active", async (SetEmployeeActiveRequest request, SetEmployeeActiveHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(
                new SetEmployeeActiveCommand(request.EmployeeId, request.IsActive, "Cashier"), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.EmployeeId, result.Value.IsActive })
                : Fail(result.Error!);
        });
    }

    private static void MapAttendance(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/attendance");

        group.MapPost("/clock-in", async (ClockInRequest request, ClockInHandler handler, HttpContext httpContext, CancellationToken ct) =>
        {
            var employeeId = request.EmployeeId ?? GetEmployeeId(httpContext);
            if (employeeId is null)
            {
                return Fail("کارمند مشخص نیست.");
            }

            var result = await handler.HandleAsync(new ClockInCommand(employeeId.Value, request.Notes), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.AttendanceId, result.Value.EmployeeId, result.Value.ClockInAt })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.AttendanceManage);

        group.MapPost("/clock-out", async (ClockOutRequest request, ClockOutHandler handler, HttpContext httpContext, CancellationToken ct) =>
        {
            var employeeId = request.EmployeeId ?? GetEmployeeId(httpContext);
            if (employeeId is null)
            {
                return Fail("کارمند مشخص نیست.");
            }

            var result = await handler.HandleAsync(new ClockOutCommand(employeeId.Value), ct);

            return result.IsSuccess
                ? Ok(new { result.Value!.AttendanceId, result.Value.EmployeeId, result.Value.ClockOutAt })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.AttendanceManage);

        group.MapGet("", async (IAttendanceRepository attendance, DateTime? date) =>
        {
            var day = date?.Date ?? DateTime.UtcNow.Date;
            var records = await attendance.GetByDateAsync(day);

            return Ok(records.Select(r => new
            {
                r.Id,
                r.EmployeeId,
                EmployeeName = r.Employee?.Name,
                r.ClockInAt,
                r.ClockOutAt,
                r.Notes
            }).ToList());
        }).RequireAuthorization(Permissions.AttendanceManage);
    }

    private static void MapSystem(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/audit/recent", async (IAuditLogRepository auditLogs, int? limit) =>
        {
            var logs = await auditLogs.GetRecentAsync(limit is > 0 and <= 500 ? limit.Value : 50);
            return Ok(logs.Select(l => new
            {
                l.Id,
                l.CreatedAt,
                l.Action,
                l.EntityType,
                l.EntityId,
                ActorType = l.ActorType.ToString(),
                l.ActorName,
                l.Source,
                l.DeviceId,
                l.SessionId
            }).ToList());
        }).RequireAuthorization(Permissions.AuditView);

        endpoints.MapGet("/api/sync/outbox/pending", async (IOutboxRepository outbox, int? batchSize) =>
        {
            var pending = await outbox.GetPendingAsync(batchSize is > 0 and <= 500 ? batchSize.Value : 100);
            return Ok(pending.Select(e => new
            {
                e.Sequence,
                e.EventType,
                e.IdempotencyKey,
                Status = e.Status.ToString(),
                e.Attempts,
                e.CreatedAt
            }).ToList());
        }).RequireAuthorization(Permissions.SyncView);

        endpoints.MapGet("/api/sync/status", async (GetSyncStatusHandler handler, CancellationToken ct) =>
        {
            var status = await handler.HandleAsync(ct);
            return Ok(new SyncStatusResponse
            {
                CloudConfigured = status.CloudConfigured,
                OutboxCursor = status.OutboxCursor,
                PendingOutbox = status.PendingOutbox,
                FailedOutbox = status.FailedOutbox,
                LastOutboxSyncAt = status.LastOutboxSyncAt,
                InboxCursor = status.InboxCursor,
                PendingInbox = status.PendingInbox,
                LastInboxSyncAt = status.LastInboxSyncAt
            });
        }).RequireAuthorization(Permissions.SyncView);

        endpoints.MapPost("/api/sync/run", async (ProcessOutboxHandler outbox, ProcessInboxHandler inbox, CancellationToken ct) =>
        {
            var outboxResult = await outbox.HandleAsync(ct);
            var inboxResult = await inbox.HandleAsync(ct);

            return Ok(new
            {
                Outbox = new { outboxResult.Pushed, outboxResult.Failed, outboxResult.CloudUnavailable },
                Inbox = new { inboxResult.Received, inboxResult.Applied, inboxResult.Skipped, inboxResult.Failed, inboxResult.CloudUnavailable }
            });
        }).RequireAuthorization(Permissions.SyncView);

        endpoints.MapPost("/api/system/backup", async (IBackupService backup, CancellationToken ct) =>
        {
            var result = await backup.CreateBackupAsync(ct);
            return result.Success
                ? Ok(new { result.FilePath })
                : Fail(result.Error!);
        }).RequireAuthorization(Permissions.ConfigManage);

        endpoints.MapGet("/api/system/backups", async (IBackupService backup, CancellationToken ct) =>
        {
            var backups = await backup.ListBackupsAsync(ct);
            return Ok(backups);
        }).RequireAuthorization(Permissions.ConfigManage);

        endpoints.MapGet("/api/reports/financial", async (
            GetFinancialReportHandler handler, DateTime? from, DateTime? to, CancellationToken ct) =>
        {
            var toDate = to ?? DateTime.UtcNow;
            var fromDate = from ?? toDate.Date;

            var report = await handler.HandleAsync(fromDate, toDate, ct);

            return Ok(new FinancialReportResponse
            {
                From = report.From,
                To = report.To,
                ServiceRevenue = report.ServiceRevenue,
                PcRevenue = report.PcRevenue,
                PsRevenue = report.PsRevenue,
                WalletTopUps = report.WalletTopUps,
                CashCollected = report.CashCollected,
                SessionCount = report.SessionCount,
                TransactionCount = report.TransactionCount,
                Transactions = report.Transactions.Select(t => new FinancialTransactionDto
                {
                    Id = t.Id,
                    CreatedAt = t.CreatedAt,
                    Type = t.Type,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    CustomerName = t.CustomerName,
                    DeviceType = t.DeviceType,
                    PaymentMethod = t.PaymentMethod,
                    Description = t.Description
                }).ToList()
            });
        }).RequireAuthorization(Permissions.ReportsView);

        endpoints.MapGet("/api/system/info", (
            IConfiguration configuration, IBackupService backup, ICloudSyncClient cloud) =>
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";

            return Ok(new SystemInfoResponse
            {
                Version = version,
                MachineName = Environment.MachineName,
                ServerUrls = configuration.GetServerUrls().ToList(),
                DatabaseProvider = configuration.GetValue("Database:UseInMemory", true) ? "InMemory" : "PostgreSQL",
                AutoMigrate = configuration.GetValue("Database:AutoMigrate", true),
                CloudConfigured = cloud.IsConfigured,
                CloudBaseUrl = configuration.GetValue<string>("Cloud:BaseUrl"),
                BackupSupported = backup.IsSupported,
                ApiKeyConfigured = !string.IsNullOrWhiteSpace(configuration.GetValue<string>("Server:ApiKey")),
                ServerTime = DateTime.UtcNow
            });
        }).RequireAuthorization(Permissions.ConfigManage);
    }

    private static Guid? GetEmployeeId(HttpContext context)
    {
        var value = context.User.FindFirst("employee_id")?.Value;
        return Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
    }

    private static IResult Ok<T>(T data) => Results.Ok(new BaseResponse<T> { Success = true, Data = data });

    private static IResult Fail(string message, int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Json(
            new BaseResponse<object> { Success = false, Message = message, Errors = new List<string> { message } },
            statusCode: statusCode);

    private static IResult NotFound(string message) => Fail(message, StatusCodes.Status404NotFound);
}
