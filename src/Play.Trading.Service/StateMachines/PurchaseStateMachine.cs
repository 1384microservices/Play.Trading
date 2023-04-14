using System;
using Automatonymous;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.Activities;
using Play.Inventory.Contracts;
using Play.Identity.Contracts;
using MassTransit;
using Play.Trading.Service.SignalR;

namespace Play.Trading.Service.StateMachines;

public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
{
    private readonly MessageHub _hub;

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

    public PurchaseStateMachine(MessageHub hub)
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
                    ctx.Instance.UserId = ctx.Data.UserId;
                    ctx.Instance.ItemId = ctx.Data.ItemId;
                    ctx.Instance.Quantity = ctx.Data.Quantity;
                    ctx.Instance.Received = DateTimeOffset.Now;
                    ctx.Instance.LastUpdated = ctx.Instance.Received;
                })
                .Activity(x => x.OfType<CalculatePurchaseTotalActivity>())
                .Send(ctx => new GrantItems(ctx.Instance.UserId, ctx.Instance.ItemId, ctx.Instance.Quantity, ctx.Instance.CorrelationId))
                .TransitionTo(Accepted)
                .Catch<Exception>(ex => ex.Then(ctx =>
                {
                    ctx.Instance.ErrorMessage = ctx.Exception.Message;
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Faulted))
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Instance))
        );
    }

    private void ConfigureAccepted()
    {
        During(Accepted,
            Ignore(PurchaseRequested),
            When(InventoryItemsGranted)
                .Then(ctx =>
                {
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .Send(ctx => new DebitGil(
                    ctx.Instance.UserId,
                    ctx.Instance.PurchaseTotal.Value,
                    ctx.Instance.CorrelationId
                ))
                .TransitionTo(ItemsGranted),

            When(GrantItemsFaulted)
                .Then(ctx =>
                {
                    ctx.Instance.ErrorMessage = ctx.Data.Exceptions[0].Message;
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Faulted)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Instance))
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
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Completed)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Instance))
                ,

            When(DebitGilFaulted)
                .Send(ctx => new SubstractItems(
                    ctx.Instance.UserId,
                    ctx.Instance.ItemId,
                    ctx.Instance.Quantity,
                    ctx.Instance.CorrelationId
                ))
                .Then(ctx =>
                {
                    ctx.Instance.ErrorMessage = ctx.Data.Exceptions[0].Message;
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Faulted)
                .ThenAsync(async ctx => await _hub.SendStatusAsync(ctx.Instance))
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
        DuringAny(When(GetPurchaseState).Respond(r => { return r.Instance; }));
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