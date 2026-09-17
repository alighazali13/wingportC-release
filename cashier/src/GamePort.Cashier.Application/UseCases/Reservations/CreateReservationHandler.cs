using System.Text.Json;
using GamePort.Cashier.Application.Common;
using GamePort.Cashier.Application.Interfaces;
using GamePort.Cashier.Domain.Entities;
using GamePort.Cashier.Domain.Enums;

namespace GamePort.Cashier.Application.UseCases.Reservations;

public class CreateReservationHandler
{
    private readonly IReservationRepository _reservations;
    private readonly ICustomerRepository _customers;
    private readonly IDeviceRepository _devices;
    private readonly ISessionRepository _sessions;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IOutboxRepository _outbox;
    private readonly IUnitOfWork _unitOfWork;

    public CreateReservationHandler(
        IReservationRepository reservations,
        ICustomerRepository customers,
        IDeviceRepository devices,
        ISessionRepository sessions,
        IAuditLogRepository auditLogs,
        IOutboxRepository outbox,
        IUnitOfWork unitOfWork)
    {
        _reservations = reservations;
        _customers = customers;
        _devices = devices;
        _sessions = sessions;
        _auditLogs = auditLogs;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreateReservationResult>> HandleAsync(CreateReservationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.EndTime <= command.StartTime)
        {
            return Result<CreateReservationResult>.Failure("زمان پایان رزرو باید بعد از زمان شروع باشد.");
        }

        if (command.EndTime <= DateTime.UtcNow)
        {
            return Result<CreateReservationResult>.Failure("رزرو باید در آینده باشد.");
        }

        var customer = await _customers.GetByIdAsync(command.CustomerId);
        if (customer is null)
        {
            return Result<CreateReservationResult>.Failure("مشتری یافت نشد.");
        }

        var device = await _devices.GetByIdAsync(command.DeviceId);
        if (device is null)
        {
            return Result<CreateReservationResult>.Failure("دستگاه یافت نشد.");
        }

        var overlapping = await _reservations.GetOverlappingAsync(device.Id, command.StartTime, command.EndTime);
        if (overlapping.Any())
        {
            return Result<CreateReservationResult>.Failure("این دستگاه در بازه‌ی زمانی انتخابی رزرو شده است.");
        }

        var openSession = await _sessions.GetOpenByDeviceIdAsync(device.Id);
        if (openSession is not null)
        {
            var sessionEnd = openSession.PlannedEndTime ?? DateTime.MaxValue;
            if (sessionEnd > command.StartTime)
            {
                return Result<CreateReservationResult>.Failure("این دستگاه در بازه‌ی انتخابی نشست فعال دارد.");
            }
        }

        var reservation = new Reservation
        {
            CustomerId = customer.Id,
            DeviceId = device.Id,
            DeviceType = device.Type,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            Status = ReservationStatus.Reserved,
            Source = command.Source ?? "Cashier",
            Notes = command.Notes
        };

        await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _reservations.CreateAsync(reservation);

            await _auditLogs.AddAsync(new AuditLog
            {
                Action = "CreateReservation",
                EntityType = nameof(Reservation),
                EntityId = reservation.Id,
                ActorType = AuditActorType.Employee,
                ActorName = command.OperatorInfo,
                Source = "Cashier",
                DeviceId = device.Id,
                AfterState = JsonSerializer.Serialize(new
                {
                    reservation.CustomerId,
                    reservation.DeviceId,
                    reservation.StartTime,
                    reservation.EndTime
                })
            });

            await _outbox.AddAsync(new OutboxEvent
            {
                EventType = "reservation.created",
                IdempotencyKey = $"reservation.created:{reservation.Id}",
                Payload = JsonSerializer.Serialize(new
                {
                    ReservationId = reservation.Id,
                    reservation.CustomerId,
                    reservation.DeviceId,
                    reservation.StartTime,
                    reservation.EndTime,
                    reservation.Source
                })
            });
        }, cancellationToken);

        return Result<CreateReservationResult>.Success(new CreateReservationResult(
            reservation.Id,
            reservation.CustomerId,
            reservation.DeviceId,
            reservation.StartTime,
            reservation.EndTime,
            reservation.Status.ToString()));
    }
}
