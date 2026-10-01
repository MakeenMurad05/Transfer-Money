using Application.Transfers.Common;
using Application.Transfers.Query.GetTransfers;
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
        // return Created($"/api/transfers/{result.Id}", result);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransferDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTransfersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransferDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTransferByIdQuery(id), cancellationToken);
        return Ok(result);
    }
}