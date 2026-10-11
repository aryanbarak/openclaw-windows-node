using System.Net;
using System.Net.Http.Headers;
using OpenClaw.Shared.Audio;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class OpenAiSpeechToTextServiceTests
{
    [Theory]
    [InlineData("auto", null)]
    [InlineData("fa", "fa")]
    [InlineData("FA-IR", "fa-ir")]
    public void NormalizeLanguage_ProducesApiValue(string input, string? expected)
    {
        Assert.Equal(expected, OpenAiSpeechToTextService.NormalizeLanguage(input));
    }

    [Fact]
    public async Task TranscribeAsync_SendsAuthenticatedWavAndPersianLanguage()
    {
        HttpRequestMessage? captured = null;
        var parts = new Dictionary<string, (string Value, string? FileName)>();
        var handler = new StubHandler(async request =>
        {
            captured = request;
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition!.Name!.Trim('"');
                var fileName = part.Headers.ContentDisposition.FileName?.Trim('"');
                parts[name] = (await part.ReadAsStringAsync(), fileName);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"text\":\"سلام دنیا\"}")
            };
        });
        using var client = new HttpClient(handler);
        using var service = new OpenAiSpeechToTextService(
            NullLogger.Instance,
            () => "test-secret",
            client);

        var result = await service.TranscribeAsync(new float[16_000], "fa");

        Assert.NotNull(captured);
        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", captured!.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "test-secret"), captured.Headers.Authorization);
        Assert.Equal(OpenAiSpeechToTextService.ModelName, parts["model"].Value);
        Assert.Equal("fa", parts["languages[]"].Value);
        Assert.Equal("speech.wav", parts["file"].FileName);
        Assert.StartsWith("RIFF", parts["file"].Value);
        Assert.Single(result);
        Assert.Equal("سلام دنیا", result[0].Text);
        Assert.Equal("fa", result[0].Language);
    }

    [Fact]
    public async Task TranscribeAsync_DoesNotExposeResponseBodyOnFailure()
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("secret diagnostic body")
            })));
        using var service = new OpenAiSpeechToTextService(NullLogger.Instance, () => "bad-key", client);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.TranscribeAsync(new float[1_000], "fa"));

        Assert.DoesNotContain("secret diagnostic body", error.Message);
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task FallbackService_UsesReadyLocalServiceAfterOnlineFailure()
    {
        var primary = new StubSpeechService(true, _ => throw new HttpRequestException("offline"));
        var fallback = new StubSpeechService(true, language =>
            Task.FromResult(new List<TranscriptionResult>
            {
                new() { Text = "متن محلی", Language = language }
            }));
        var service = new FallbackSpeechToTextService(primary, fallback, NullLogger.Instance);

        var result = await service.TranscribeAsync(new float[800], "fa");

        Assert.Equal("متن محلی", Assert.Single(result).Text);
        Assert.Equal(1, primary.Calls);
        Assert.Equal(1, fallback.Calls);
    }

    [Fact]
    public async Task FallbackService_DoesNotHideInvalidApiKey()
    {
        var primary = new StubSpeechService(true, _ => throw new HttpRequestException(
            "unauthorized",
            null,
            HttpStatusCode.Unauthorized));
        var fallback = new StubSpeechService(true, _ =>
            Task.FromResult(new List<TranscriptionResult>()));
        var service = new FallbackSpeechToTextService(primary, fallback, NullLogger.Instance);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.TranscribeAsync(new float[800], "fa"));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(0, fallback.Calls);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }

    private sealed class StubSpeechService(
        bool ready,
        Func<string, Task<List<TranscriptionResult>>> transcribe) : ISpeechToTextService
    {
        public bool IsReady => ready;
        public int Calls { get; private set; }

        public Task<List<TranscriptionResult>> TranscribeAsync(
            float[] samples,
            string language = "auto",
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return transcribe(language);
        }
    }
}
