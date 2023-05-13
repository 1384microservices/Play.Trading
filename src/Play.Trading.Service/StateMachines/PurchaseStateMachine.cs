using System;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.Activities;
using Play.Inventory.Contracts;
using Play.Identity.Contracts;
using MassTransit;
using Play.Trading.Service.SignalR;
using Microsoft.Extensions.Logging;

namespace Play.Trading.Service.StateMachines;

public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
{
    private readonly MessageHub _hub;
    private readonly ILogger<PurchaseStateMachine> _logger;
    public State Accepted { get; }
    public State ItemsGranted { get; }
    public State Completed { get; }
    public State Faulted { get; }
    public Event<PurchaseRequested> PurchaseRequested { get; }
    public Event<GetPurchaseState> GetPurchaseState { get; }
    public Event<InventoryItemsGranted> InventoryItemsGranted { get; }
    public Event<GilDebited> GilDebited { get; }
    public Event<Fault<GrantItems>> GrantItemsFaulted { get; }
    public Event<Fault<DebitGil>> DebitGilFaulted { get; }

    public PurchaseStateMachine(MessageHub hub, ILogger<PurchaseStateMachine> logger)
    {
        InstanceState(state => state.CurrentState);
        ConfigureEvents();
        ConfigureInitialState();
        ConfigureAny();
        ConfigureAccepted();
        ConfigureItemsGranted();
        ConfigureFaulted();
        ConfigureCompleted();

        _hub = hub;
        _logger = logger;
    }

    private void ConfigureEvents()
    {
        Event(() => PurchaseRequested);
        Event(() => GetPurchaseState);
        Event(() => InventoryItemsGranted);
        Event(() => GilDebited);
        Event(() => GrantItemsFaulted, x => x.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
        Event(() => DebitGilFaulted, x => x.CorrelateById(ctx => ctx.Message.Message.CorrelationId));
    }

    private void ConfigureInitialState()
    {
        Initially(
            When(PurchaseRequested)
                .Then(ctx =>
                {
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.ItemId = ctx.Message.ItemId;
                    ctx.Saga.Quantity = ctx.Message.Quantity;
                    ctx.Saga.Received = DateTimeOffset.Now;
                    ctx.Saga.LastUpdated = ctx.Saga.Received;
                    _logger.LogInformation("Calculating total price for purchase with CorrelationId {CorrelationId}...", ctx.Saga.CorrelationId);
                })
                .Activity(x => x.OfType<CalculatePurchaseTotalActivity>())
                .Send(ctx => new GrantItems(ctx.Saga.UserId, ctx.Saga.ItemId, ctx.Saga.Quantity, ctx.Saga.CorrelationId))
                .TransitionTo(Accepted)
                .Catch<Exception>(ex => ex.Then(ctx =>
                {
                    ctx.Saga.ErrorMessage = ctx.Exception.Message;
                    ctx.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    _logger.LogError(
                        ctx.Exception, 
                        "Could not calculate the total price of purchase with {CorrelationId}. Error: {ErrorMessage}", 
                        ctx.Saga.CorrelationId,
                        ctx.Saga.ErrorMessage
                    );
                })
                .TransitionTo(Faulted))
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Saga))
        );
    }

    private void ConfigureAccepted()
    {
        During(Accepted,
            Ignore(PurchaseRequested),
            When(InventoryItemsGranted)
                .Then(ctx =>
                {
                    ctx.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    _logger.LogInformation(
                        "Items of purchase with CorrelationId {CorrelationId} have been granted to user {UserId}.",
                        ctx.Saga.CorrelationId,
                        ctx.Saga.UserId
                    );
                })
                .Send(ctx => new DebitGil(
                    ctx.Saga.UserId,
                    ctx.Saga.PurchaseTotal.Value,
                    ctx.Saga.CorrelationId
                ))
                .TransitionTo(ItemsGranted),

            When(GrantItemsFaulted)
                .Then(ctx =>
                {
                    ctx.Saga.ErrorMessage = ctx.Message.Exceptions[0].Message;
                    ctx.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    _logger.LogError(
                        "Could not grant items for purchase with CorrelationId {CorrelationId}. Error: {ErrorMessage}",
                        ctx.Saga.CorrelationId,
                        ctx.Saga.ErrorMessage
                    );
                })
                .TransitionTo(Faulted)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Saga))
        );
    }

    private void ConfigureItemsGranted()
    {
        During(ItemsGranted,
            Ignore(PurchaseRequested),
            Ignore(InventoryItemsGranted),
            When(GilDebited)
                .Then(ctx =>
                {
                    ctx.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    _logger.LogInformation(
                        "The total price of purchase with CorrelationId {CorrelationId} has been debited from user {UserID}",
                        ctx.Saga.CorrelationId,
                        ctx.Saga.UserId
                    );
                })
                .TransitionTo(Completed)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Saga))
                ,

            When(DebitGilFaulted)
                .Send(ctx => new SubstractItems(
                    ctx.Saga.UserId,
                    ctx.Saga.ItemId,
                    ctx.Saga.Quantity,
                    ctx.Saga.CorrelationId
                ))
                .Then(ctx =>
                {
                    ctx.Saga.ErrorMessage = ctx.Message.Exceptions[0].Message;
                    ctx.Saga.LastUpdated = DateTimeOffset.UtcNow;
                    _logger.LogError(
                        "Could not debit the total price of purchase with CorrelationId {CorrelationId} from {UserId}. Error: {ErrorMessage}",
                        ctx.Saga.CorrelationId,
                        ctx.Saga.UserId,
                        ctx.Saga.ErrorMessage
                    );
                })
                .TransitionTo(Faulted)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Saga))
        );
    }

    private void ConfigureCompleted()
    {
        During(Completed,
            Ignore(PurchaseRequested),
            Ignore(InventoryItemsGranted),
            Ignore(GilDebited)
        );
    }

    private void ConfigureAny()
    {
        DuringAny(When(GetPurchaseState).Respond(r => { return r.Saga; }));
    }

    private void ConfigureFaulted()
    {
        During(Faulted,
            Ignore(PurchaseRequested),
            Ignore(InventoryItemsGranted),
            Ignore(GilDebited)
        );
    }
}