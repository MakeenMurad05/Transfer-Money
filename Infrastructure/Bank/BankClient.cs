using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Bank;

public class BankClient : IBankClient
{
    private const string TransferPath = "api/bank/transfers";
    private const string SuccessCode = "00";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    private readonly ILogger<BankClient> _logger;

    public BankClient(HttpClient httpClient, ILogger<BankClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankTransferResult> TransferAsync(
        BankTransferRequest request,
        CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(request, JsonOptions);
        var stopwatch = Stopwatch.StartNew();

        try
        {


            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(TransferPath, content, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            var bankResponse = TryParse(responseBody);

            // Case: 200 + "00" → Success
            if (response.IsSuccessStatusCode && bankResponse?.ResponseCode == SuccessCode)
            {
                // Case: 200 + "00" but no BankReference → Invalid Response

                if (string.IsNullOrWhiteSpace(bankResponse.BankReference))
                {
                    _logger.LogError(
                        "Bank approved {TransactionId} but sent no BankReference",
                        request.TransactionId);

                    return Failed("INVALID_RESPONSE", "Bank approved the transfer but sent no bank reference",
                        requestBody, responseBody, statusCode, stopwatch.ElapsedMilliseconds);
                }




                return new BankTransferResult(
                    IsSuccess: true,
                    ResponseCode: bankResponse.ResponseCode,
                    ResponseMessage: bankResponse.ResponseMessage,
                    BankReference: bankResponse.BankReference,
                    RequestBody: requestBody,
                    ResponseBody: responseBody,
                    HttpStatusCode: statusCode,
                    DurationMs: stopwatch.ElapsedMilliseconds);
            }

            // Case: 200 but another code → Business Error
            // Case: 400 / 500 / anything else → HTTP Error
            var defaultMessage = statusCode switch
            {
                >= 200 and < 300 => "Bank rejected the transfer",
                400 => "Bank rejected the request as invalid",
                >= 500 => "Bank service error",
                _ => $"Unexpected response from bank ({statusCode})"
            };

            return Failed(
                bankResponse?.ResponseCode,
                bankResponse?.ResponseMessage ?? defaultMessage,
                requestBody, responseBody, statusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Case: Timeout
            stopwatch.Stop();
            _logger.LogWarning(
                    "Bank call timed out for {TransactionId} after {DurationMs} ms",
                    request.TransactionId, stopwatch.ElapsedMilliseconds);

            return Failed("TIMEOUT", "Bank did not respond in time",
                requestBody, null, null, stopwatch.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            // Case: Connection Error
            stopwatch.Stop();
            _logger.LogError(ex,
            "Could not connect to bank for {TransactionId}",
            request.TransactionId);

            return Failed("CONNECTION_ERROR", "Could not connect to the bank",
                requestBody, null, null, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Case: unexpected error

            stopwatch.Stop();
            _logger.LogError(ex,
            "Unexpected error while calling bank for {TransactionId}",
            request.TransactionId);

            return Failed("UNEXPECTED_ERROR", "unexpected error while calling bank",
                requestBody, null, null, stopwatch.ElapsedMilliseconds);
        }
    }


    private static BankTransferResult Failed(
        string? code, string message, string requestBody,
        string? responseBody, int? statusCode, long durationMs)
        => new(false, code, message, null, requestBody, responseBody, statusCode, durationMs);

    private static BankResponse? TryParse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<BankResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The bank's own response format. Only BankClient knows it.
    private record BankResponse(string ResponseCode, string ResponseMessage, string? BankReference);
}