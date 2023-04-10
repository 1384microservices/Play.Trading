using System;
using Automatonymous;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.Activities;

namespace Play.Trading.Service.StateMachines;

public class PurchaseStateMachine : MassTransitStateMachine<PurchaseState>
{
    public State Accepted { get; }
    public State ItemsGranted { get; }
    public State Completed { get; }
    public State Faulted { get; }

    public Event<PurchaseRequested> PurchaseRequested { get; }
    public Event<GetPurchaseState> GetPurchaseState { get; }

    public PurchaseStateMachine()
    {
        InstanceState(state => state.CurrentState);
        ConfigureEvents();
        ConfigureInitialState();
        ConfigureAny();
    }

    private void ConfigureEvents()
    {
        Event(() => PurchaseRequested);
        Event(() => GetPurchaseState);
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
            .TransitionTo(Accepted)
            .Catch<Exception>(ex => ex.Then(ctx =>
            {
                ctx.Instance.ErrorMessage = ctx.Exception.Message;
                ctx.Instance.LastUpdated = DateTimeOffset.UtcNow;
            }).TransitionTo(Faulted))
        );
    }

    private void ConfigureAny()
    {
        DuringAny(When(GetPurchaseState).Respond(r => { return r.Instance; }));
    }
}