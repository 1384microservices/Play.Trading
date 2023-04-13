using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Play.Trading.Service.Contracts;
using Play.Trading.Service.Dtos;
using Play.Trading.Service.StateMachines;

namespace Play.Trading.Service.Controllers;

[ApiController]
[Route("purchase")]
// [Authorize]
public class PurchaseController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IRequestClient<GetPurchaseState> _purchaseClient;

    public PurchaseController(IPublishEndpoint publishEndpoint, IRequestClient<GetPurchaseState> purchaseClient)
    {
        _publishEndpoint = publishEndpoint;
        _purchaseClient = purchaseClient;
    }

    [HttpGet("status/{idempotencyId}")]
    public async Task<ActionResult<PurchaseDto>> GetStatusAsync(Guid idempotencyId)
    {
        var response = await _purchaseClient.GetResponse<PurchaseState>(new GetPurchaseState(idempotencyId));
        var state = response.Message;
        var purchase = new PurchaseDto(
            state.UserId,
            state.ItemId,
            state.PurchaseTotal,
            state.Quantity,
            state.CurrentState,
            state.ErrorMessage,
            state.Received,
            state.LastUpdated
        );

        return Ok(purchase);
    }

    [HttpPost]
    public async Task<IActionResult> PostAsync(SubmitPurchaseDto purchase)
    {
        var userId = Guid.Parse(User.FindFirstValue("sub"));
        var message = new PurchaseRequested(userId, purchase.ItemId.Value, purchase.Quantity, purchase.IdempotencyId.Value);

        await _publishEndpoint.Publish(message);

        return AcceptedAtAction(
            nameof(GetStatusAsync),
            new { purchase.IdempotencyId },
            new { purchase.IdempotencyId }
        );
    }
}
