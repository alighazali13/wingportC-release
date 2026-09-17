using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Sessions;

public class StartSessionHandler
{
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly ISessionRepository _sessions;
    private readonly IPricingRuleRepository _pricingRules;
    private readonly IReservationRepository _reservations;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public StartSessionHandler(
        ICustomerRepository customers,
        IDeviceRepository devices,
        ISessionRepository sessions,
        IPricingRuleRepository pricingRules,
        IReservationRepository reservations,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _devices = devices;
        _sessions = sessions;
        _pricingRules = pricingRules;
        _reservations = reservations;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StartSessionResult>> HandleAsync(StartSessionCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId);
        if (customer is null)
        {
            return Result<StartSessionResult>.Failure("مشتری یافت نشد.");
        }

        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<StartSessionResult>.Failure("دستگاه یافت نشد.");
        }

        if (device.Status == DeviceStatus.InUse)
        {
            return Result<StartSessionResult>.Failure("دستگاه در حال حاضر در حال استفاده است.");
        }

        var customerSessions = await _sessions.GetByCustomerIdAsync(command.CustomerId);
        var activeCount = customerSessions.Count(s =>
            s.Status is SessionStatus.Active or SessionStatus.Paused or SessionStatus.Reserved);

        if (activeCount >= customer.MaxConcurrentSessions)
        {
            return Result<StartSessionResult>.Failure("حد مجاز نشست‌های همزمان این مشتری پر شده است.");
        }

        var pricingRule = await _pricingRules.GetActiveByDeviceTypeAsync(device.Type);
        if (pricingRule is null)
        {
            return Result<StartSessionResult>.Failure("برای این نوع دستگاه نرخ فعالی تعریف نشده است.");
        }

        Reservation? reservation = null;
        if (command.ReservationId.HasValue)
        {
            reservation = await _reservations.GetByIdAsync(command.ReservationId.Value);
            if (reservation is null)
            {
                return Result<StartSessionResult>.Failure("رزرو یافت نشد.");
            }

            if (reservation.Status != ReservationStatus.Reserved)
            {
                return Result<StartSessionResult>.Failure("این رزرو دیگر قابل استفاده نیست.");
            }

            if (reservation.CustomerId != customer.Id || reservation.DeviceId != device.Id)
            {
                return Result<StartSessionResult>.Failure("رزرو با مشتری یا دستگاه انتخاب‌شده مطابقت ندارد.");
            }
        }

        var startTime = DateTime.UtcNow;
        var session = new Session
        {
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = device.Type,
            StartTime = startTime,
            PlannedEndTime = command.PlannedEndTime,
            PriceAtStart = pricingRule.PricePerHour,
            Status = SessionStatus.Active,
            CreationSource = "Cashier",
            OperatorInfo = command.OperatorInfo
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _sessions.CreateAsync(session);

            device.Status = DeviceStatus.InUse;
            await _devices.UpdateAsync(device);

            if (reservation is not null)
            {
                reservation.Status = ReservationStatus.Converted;
                reservation.ConvertedSessionId = session.Id;
                reservation.ConvertedAt = startTime;
                await _reservations.UpdateAsync(reservation);

                await _auditLogs.AddAsync(new AuditLog
                {
                    Action = "ConvertReservation",
                    EntityType = nameof(Reservation),
                    EntityId = reservation.Id,
                    ActorType = AuditActorType.Employee,
                    ActorName = command.OperatorInfo,
                    Source = "Cashier",
                    SessionId = session.Id,
                    DeviceId = device.Id,
                    AfterState = JsonSerializer.Serialize(new { Status = nameof(ReservationStatus.Converted), reservation.ConvertedSessionId })
                });
            }

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "StartSession",
                EntityType = nameof(Session),
                EntityId = session.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                SessionId = session.Id,
                DeviceId = device.Id,
                AfterState = JsonSerializer.Serialize(new { session.CustomerId, session.DeviceId, session.PriceAtStart })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "session.started",
                IdempotencyKey = $"session.started:{session.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    SessionId = session.Id,
                    session.CustomerId,
                    session.DeviceId,
                    DeviceType = session.DeviceType.ToString(),
                    session.StartTime,
                    session.PlannedEndTime,
                    session.PriceAtStart
                })
            });
        }, cancellationToken);

        return Result<StartSessionResult>.Success(
            new StartSessionResult(session.Id, session.CustomerId, session.DeviceId, session.PriceAtStart, session.StartTime, session.PlannedEndTime));
    }
}
