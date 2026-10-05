var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//----Mocking----//

app.MapPost("/api/bank/transfers", async (BankTransferRequest request) =>
{
    // Case: 400 Bad Request
    if (request.Amount == 400m)
        return Results.BadRequest(
            new BankTransferResponse("30", "Invalid request format", null));

    // Case: 500 Server Error
    if (request.Amount == 500m)
        return Results.Json(
            new BankTransferResponse("96", "Bank system error", null),
            statusCode: 500);

    // Case: Timeout (bank is too slow)
    if (request.Amount == 999m)
        await Task.Delay(TimeSpan.FromSeconds(10));

    // Case: Business Error (HTTP 200, but the bank says no)
    if (request.Amount > 10000m)
        return Results.Ok(
            new BankTransferResponse("51", "Insufficient funds", null));

    // Case: Success
    var bankReference = "BNK-" + Guid.NewGuid().ToString("N")[..10].ToUpper();

    // return Results.Ok(
    //     new BankTransferResponse("00", "Approved", bankReference));

    // TEMP: a message too long for our column (500)
    return Results.Ok(new BankTransferResponse("00", new string('x', 600), bankReference));

});

app.Run();

record BankTransferRequest(
    Guid TransactionId,
    string AccountNumber,
    decimal Amount,
    string Currency,
    string Reference);

record BankTransferResponse(
    string ResponseCode,
    string ResponseMessage,
    string? BankReference);