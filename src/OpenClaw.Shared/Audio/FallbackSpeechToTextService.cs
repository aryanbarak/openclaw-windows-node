namespace OpenClaw.Shared.Audio;

/// <summary>Uses the primary service and falls back only when the primary fails.</summary>
public sealed class FallbackSpeechToTextService : ISpeechToTextService
{
    private readonly ISpeechToTextService _primary;
    private readonly ISpeechToTextService _fallback;
    private readonly IOpenClawLogger _logger;

    public bool IsReady => _primary.IsReady || _fallback.IsReady;

    public FallbackSpeechToTextService(
        ISpeechToTextService primary,
        ISpeechToTextService fallback,
        IOpenClawLogger logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<List<TranscriptionResult>> TranscribeAsync(
        float[] samples,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.TranscribeAsync(samples, language, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (_fallback.IsReady && IsTransient(ex))
        {
            _logger.Warn($"Primary speech recognition failed; using the local fallback ({ex.GetType().Name}).");
            return await _fallback.TranscribeAsync(samples, language, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(Exception error)
    {
        if (error is not HttpRequestException httpError || httpError.StatusCode is null)
            return error is HttpRequestException or TimeoutException;

        var status = (int)httpError.StatusCode.Value;
        return status is 408 or 429 || status >= 500;
    }
}
