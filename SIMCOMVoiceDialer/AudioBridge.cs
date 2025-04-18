using System;
using System.IO.Ports;
using NAudio.Wave;

namespace SIMCOMVoiceDialer
{
    public class AudioBridge(string audioPortName, int baudRate, bool verbose = false) : IDisposable
    {
        private readonly string audioPortName = audioPortName;
        private readonly int baudRate = baudRate;
        private readonly bool verboseOutput = verbose;

        private WaveInEvent? waveIn;
        private WaveOutEvent? waveOut;
        private BufferedWaveProvider? buffer;
        private SerialPort? audioPort;

        public float EchoSuppressionFactor { get; set; } = 0.5f; // example default

        public void OpenAudio()
        {
            // Open the serial port for audio
            audioPort = new SerialPort(audioPortName, baudRate);
            audioPort.DataReceived += AudioPortOnDataReceived;
            audioPort.Open();
            if (verboseOutput) Console.WriteLine($"Audio Port opened on {audioPortName}");

            // Configure wave devices
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(8000, 1),
                BufferMilliseconds = 30
            };
            waveIn.DataAvailable += WaveInOnDataAvailable;

            waveOut = new WaveOutEvent
            {
                DesiredLatency = 50,
                NumberOfBuffers = 4
            };

            buffer = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferLength = 4096,
                DiscardOnBufferOverflow = true
            };
            waveOut.Init(buffer);
            waveOut.Volume = 0.7f;
        }

        public void StartAudio()
        {
            waveIn?.StartRecording();
            waveOut?.Play();
        }

        public void StopAudio()
        {
            waveIn?.StopRecording();
            waveOut?.Stop();
        }

        public void ClearPortBuffers()
        {
            if (audioPort != null && audioPort.IsOpen)
            {
                audioPort.DiscardInBuffer();
                audioPort.DiscardOutBuffer();
            }
        }

        private void WaveInOnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // Simple approach: scale volume for echo suppression
                float adjustedVolume = (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                    ? EchoSuppressionFactor
                    : 1.0f;

                byte[] adjustedBuffer = AdjustAudioVolume(e.Buffer, e.BytesRecorded, adjustedVolume);

                // Send to the modem's audio port
                audioPort?.Write(adjustedBuffer, 0, adjustedBuffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Audio error (WaveInOnDataAvailable): " + ex.Message);
            }
        }

        private void AudioPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytes = audioPort!.BytesToRead;
                if (bytes > 0)
                {
                    var audioData = new byte[bytes];
                    audioPort.Read(audioData, 0, bytes);

                    // Optionally, check buffer size to avoid overflow
                    if (buffer!.BufferedDuration.TotalMilliseconds >= 100)
                    {
                        if (verboseOutput) Console.WriteLine("Audio buffer full, removing old audio.");
                        buffer.ClearBuffer();
                    }

                    buffer.AddSamples(audioData, 0, audioData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AudioPort data receive error: " + ex.Message);
            }
        }

        private byte[] AdjustAudioVolume(byte[] buffer, int length, float volumeFactor)
        {
            // Simple scaling of 16-bit samples in place
            for (int i = 0; i < length; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sample = (short)(sample * volumeFactor);
                byte[] adjustedSample = BitConverter.GetBytes(sample);
                buffer[i] = adjustedSample[0];
                buffer[i + 1] = adjustedSample[1];
            }
            return buffer;
        }

        public void CloseAudio()
        {
            if (audioPort != null && audioPort.IsOpen) audioPort.Close();
        }

        public void Dispose()
        {
            waveIn?.Dispose();
            waveOut?.Dispose();

            if (audioPort != null)
            {
                if (audioPort.IsOpen) audioPort.Close();
                audioPort.Dispose();
            }
        }
    }
}
