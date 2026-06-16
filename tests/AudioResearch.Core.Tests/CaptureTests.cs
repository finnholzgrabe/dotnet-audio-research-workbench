using AudioResearch.Core.Audio;
using Xunit;

namespace AudioResearch.Core.Tests;

public class CaptureTests
{
    [Fact]
    public void SyntheticCaptureSource_RespectsSecondsAndRate()
    {
        IAudioCaptureSource source = new SyntheticCaptureSource("tone", 440.0);
        AudioBuffer buffer = source.Capture(0.5, 16000);

        Assert.Equal(16000, buffer.SampleRate);
        Assert.Equal(8000, buffer.FrameCount);
        Assert.Contains("synthetic", source.Description);
    }

    [Fact]
    public void WavFileCaptureSource_RoundTripsAndTrims()
    {
        string path = Path.Combine(Path.GetTempPath(), "cap-" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            WavFile.Write(path, SignalGenerator.Sine(440.0, 1.0, 16000));
            IAudioCaptureSource source = new WavFileCaptureSource(path);

            Assert.Equal(16000, source.Capture(-1, 16000).FrameCount);   // full file
            Assert.Equal(8000, source.Capture(0.5, 16000).FrameCount);   // trimmed to 0.5 s
            Assert.Equal(16000, source.Capture(5.0, 16000).FrameCount);  // request longer -> full file
        }
        finally
        {
            File.Delete(path);
        }
    }
}
