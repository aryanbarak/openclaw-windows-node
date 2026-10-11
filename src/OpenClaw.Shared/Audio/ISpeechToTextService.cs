using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.Audio;

/// <summary>Transcribes 16 kHz mono PCM samples without exposing provider details to capture code.</summary>
public interface ISpeechToTextService
{
    bool IsReady { get; }

    Task<List<TranscriptionResult>> TranscribeAsync(
        float[] samples,
        string language = "auto",
        CancellationToken cancellationToken = default);
}
