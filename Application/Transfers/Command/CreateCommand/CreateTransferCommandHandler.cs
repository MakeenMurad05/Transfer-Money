using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Transfers.Common;
using Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Transfers.Command.CreateCommand;

namespace Application.Transfers.Command.CreateCommand;

public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand , TransferDto>
{
    

    private const string BankServiceName = "BankTransfer";

    private readonly IAppDbContext _context;
    private readonly IBankClient _bankClient;

    public CreateTransferCommandHandler(IAppDbContext context, IBankClient bankClient)
    {
        _context = context;
        _bankClient = bankClient;
    }

    public async Task<TransferDto> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {

        // Stop Duplicate Referances 
        await EnsureReferenceIsUniqueAsync(request.Reference , cancellationToken);

        //Save as Pending 
        var transfer = new Transfer(request.Reference , request.AccountNumber , request.Amount ,request.Currency);
        _context.Transfers.Add(transfer);

        await SavePendingAsync(request.Reference ,cancellationToken);


        // From here we finish the job even if the client disconnects
        var bankResult = await _bankClient.TransferAsync(
            new BankTransferRequest(
                transfer.TransactionId,
                transfer.AccountNumber,
                transfer.Amount,
                transfer.Currency,
                transfer.Reference),
            CancellationToken.None);

        //  log the request and the response 
        _context.ExternalServiceLogs.Add(new ExternalServiceLog(
                    transfer.TransactionId,
                    BankServiceName,
                    bankResult.RequestBody,
                    bankResult.ResponseBody,
                    bankResult.HttpStatusCode,
                    bankResult.DurationMs));

        if (bankResult.IsSuccess)
            transfer.MarkSuccess(
                bankResult.BankReference ?? string.Empty,
                bankResult.ResponseCode ?? string.Empty,
                bankResult.ResponseMessage);
        else
            transfer.MarkFailed(bankResult.ResponseCode, bankResult.ResponseMessage);

        // Log + status saved together: both or neither
        await _context.SaveChangesAsync(CancellationToken.None);

        //  return the result
        return transfer.ToDto();

    }


    private async Task EnsureReferenceIsUniqueAsync(string reference, CancellationToken cancellationToken)
    {
        var exists = await _context.Transfers.AnyAsync(t => t.Reference == reference, cancellationToken);

        if (exists)
            throw new ConflictException($"A transfer with reference '{reference}' already exists.");
    }

     private async Task SavePendingAsync(string reference, CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request with the same Reference was saved at the same moment.
            // The Unique Index stopped it. Check again to be sure that's the reason.
            await EnsureReferenceIsUniqueAsync(reference, cancellationToken);
            throw;
        }
    }
}