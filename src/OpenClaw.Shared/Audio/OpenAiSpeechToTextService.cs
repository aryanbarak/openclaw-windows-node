using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Shared.Audio;

/// <summary>OpenAI file-transcription client for bounded microphone utterances.</summary>
public sealed class OpenAiSpeechToTextService : ISpeechToTextService, IDisposable
{
    public const string ProviderName = "openai";
    public const string ModelName = "gpt-transcribe";
    private static readonly Uri TranscriptionsEndpoint = new("https://api.openai.com/v1/audio/transcriptions");

    private readonly IOpenClawLogger _logger;
    private readonly Func<string> _apiKey;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public bool IsReady => !string.IsNullOrWhiteSpace(_apiKey());

    public OpenAiSpeechToTextService(IOpenClawLogger logger, Func<string> apiKey)
        : this(logger, apiKey, CreateHttpClient(), ownsClient: true)
    {
    }

    internal OpenAiSpeechToTextService(
        IOpenClawLogger logger,
        Func<string> apiKey,
        HttpClient httpClient,
        bool ownsClient = false)
    {
        _logger = logger;
        _apiKey = apiKey;
        _httpClient = httpClient;
        _ownsClient = ownsClient;
    }

    public async Task<List<TranscriptionResult>> TranscribeAsync(
        float[] samples,
        string language = "auto",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Length == 0) return new();

        var key = _apiKey().Trim();
        if (key.Length == 0)
            throw new InvalidOperationException("An OpenAI API key is required for online speech recognition.");

        using var request = new HttpRequestMessage(HttpMethod.Post, TranscriptionsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var form = new MultipartFormDataContent();
        using var wav = EncodeWave(samples, 16_000);
        using var audio = new StreamContent(wav);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audio, "file", "speech.wav");
        form.Add(new StringContent(ModelName, Encoding.UTF8), "model");
        form.Add(new StringContent("json", Encoding.UTF8), "response_format");

        var normalizedLanguage = NormalizeLanguage(language);
        if (normalizedLanguage is not null)
            form.Add(new StringContent(normalizedLanguage, Encoding.UTF8), "languages[]");
        request.Content = form;

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Warn($"OpenAI speech transcription failed with HTTP {(int)response.StatusCode}.");
            throw new HttpRequestException(
                $"Online speech recognition failed with HTTP {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            responseStream,
            new JsonDocumentOptions { MaxDepth = 16 },
            cancellationToken).ConfigureAwait(false);
        var text = document.RootElement.TryGetProperty("text", out var textNode)
            ? textNode.GetString()?.Trim()
            : null;
        if (string.IsNullOrWhiteSpace(text)) return new();

        var duration = TimeSpan.FromSeconds((double)samples.Length / 16_000);
        return new()
        {
            new TranscriptionResult
            {
                Text = text,
                Start = TimeSpan.Zero,
                End = duration,
                Language = normalizedLanguage ?? "auto"
            }
        };
    }

    internal static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) ||
            string.Equals(language.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        return language.Trim().Replace('_', '-').ToLowerInvariant();
    }

    internal static MemoryStream EncodeWave(float[] samples, int sampleRate)
    {
        var stream = new MemoryStream(capacity: 44 + samples.Length * 2);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        const short channels = 1;
        const short bitsPerSample = 16;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = samples.Length * blockAlign;

        writer.Write("RIFF"u8);
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataSize);
        foreach (var sample in samples)
            writer.Write((short)(Math.Clamp(sample, -1.0f, 1.0f) * 32767));
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
    }

    public void Dispose()
    {
        if (_ownsClient) _httpClient.Dispose();
    }
}
