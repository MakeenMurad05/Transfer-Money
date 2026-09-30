using Application.Transfers.Common;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Transfers.Command.CreateCommand;

namespace API.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly ISender _sender;

    public TransfersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    public async Task<ActionResult<TransferDto>> Create(
        CreateTransferCommand command,
        CancellationToken cancellationToken)
    {
        
        var result = await _sender.Send(command, cancellationToken);
        return Created($"/api/transfers/{result.Id}", result);
    }
}