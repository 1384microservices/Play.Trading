using System;
using Automatonymous;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.Activities;
using Play.Inventory.Contracts;
using Play.Identity.Contracts;
using MassTransit;

namespace Play.Trading.Service.StateMachines;

public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
{
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

    public PurchaseStateMachine()
    {
        InstanceState(state => state.CurrentState);
        ConfigureEvents();
        ConfigureInitialState();
        ConfigureAny();
        ConfigureAccepted();
        ConfigureItemsGranted();
        ConfigureFaulted();
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
                }).TransitionTo(Faulted))
        );
    }

    private void ConfigureAccepted()
    {
        During(Accepted,
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
        );
    }

    private void ConfigureItemsGranted()
    {
        During(ItemsGranted,
            When(GilDebited)
                .Then(ctx =>
                {
                    ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Completed),

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
        );
    }

    private void ConfigureAny()
    {
        DuringAny(When(GetPurchaseState).Respond(r => { return r.Instance; }));
    }

    private void ConfigureFaulted() {
        During(Faulted, 
            Ignore(PurchaseRequested), 
            Ignore(InventoryItemsGranted), 
            Ignore(GilDebited)
        );
    }
}