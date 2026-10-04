using Application.Transfers.Common;
using Application.Transfers.Queries.GetTransfers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Application.Transfers.Commands.CreateTransfer;
using Application.Transfers.Queries.GetTransferById;

namespace API.Controllers;

[ApiController]
[Route("api/transfers")]
[Produces("application/json")]
public class TransfersController : ControllerBase
{
    private readonly ISender _sender;

    public TransfersController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TransferDto>> Create(
        CreateTransferCommand command,
        CancellationToken cancellationToken)
    {
        
        var result = await _sender.Send(command, cancellationToken);
        // return Created($"/api/transfers/{result.Id}", result);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TransferDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTransfersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransferDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTransferByIdQuery(id), cancellationToken);
        return Ok(result);
    }
}