using System;
using System.IO.Ports;
using NAudio.Wave;

namespace SIMCOMVoiceDialer
{
    // Manages audio I/O between the computer and SIMCOM modem via serial port and system audio devices
    public class AudioBridge(string audioPortName, int baudRate, bool verbose = false) : IDisposable
    {
        // Store constructor parameters in read-only fields
        private readonly string audioPortName = audioPortName;
        private readonly int baudRate = baudRate;
        private readonly bool verboseOutput = verbose;

        // Audio components
        private WaveInEvent? waveIn;                      // Captures audio from microphone
        private WaveOutEvent? waveOut;                    // Plays audio to speaker
        private BufferedWaveProvider? buffer;             // Buffers incoming audio from the modem

        // Serial port connected to the modem's audio channel
        private SerialPort? audioPort;

        // Adjusts volume for outgoing audio to suppress echo
        public float EchoSuppressionFactor { get; set; } = 0.5f;

        /// <summary>
        /// Initializes and opens the audio port and sets up audio input/output components
        /// </summary>
        public void OpenAudio()
        {
            // Open the serial port for sending/receiving audio
            audioPort = new SerialPort(audioPortName, baudRate);
            audioPort.DataReceived += AudioPortOnDataReceived; // Event for receiving audio from modem
            audioPort.Open();

            if (verboseOutput) Console.WriteLine($"Audio Port opened on {audioPortName}");

            // Configure microphone input
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(8000, 1), // 8kHz mono PCM audio
                BufferMilliseconds = 30               // Buffer duration for mic capture
            };
            waveIn.DataAvailable += WaveInOnDataAvailable; // Event when mic has data

            // Configure speaker output
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 50,  // Target latency in ms
                NumberOfBuffers = 4   // Increase to reduce stutter at cost of latency
            };

            // Set up buffer to store audio received from the modem
            buffer = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferLength = 4096,                 // Size of the buffer
                DiscardOnBufferOverflow = true       // Prevent overflow buildup
            };
            waveOut.Init(buffer);                    // Bind buffer to speaker
            waveOut.Volume = 0.7f;                   // Set initial playback volume
        }

        /// <summary> Starts capturing from mic and playing to speaker </summary>
        public void StartAudio()
        {
            waveIn?.StartRecording(); // Begin mic capture
            waveOut?.Play();          // Begin speaker output
        }

        /// <summary> Stops audio input/output </summary>
        public void StopAudio()
        {
            waveIn?.StopRecording();
            waveOut?.Stop();
        }

        /// <summary> Clears both input and output buffers on the serial audio port </summary>
        public void ClearPortBuffers()
        {
            if (audioPort != null && audioPort.IsOpen)
            {
                audioPort.DiscardInBuffer();
                audioPort.DiscardOutBuffer();
            }
        }

        /// <summary>
        /// Called when audio is received from the microphone.
        /// Adjusts the volume (for echo suppression) and sends it to the modem.
        /// </summary>
        private void WaveInOnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                // Adjust outgoing audio volume to reduce echo
                float adjustedVolume = (waveOut != null && waveOut.PlaybackState == PlaybackState.Playing)
                    ? EchoSuppressionFactor
                    : 1.0f;

                byte[] adjustedBuffer = AdjustAudioVolume(e.Buffer, e.BytesRecorded, adjustedVolume);

                // Send adjusted audio to modem
                audioPort?.Write(adjustedBuffer, 0, adjustedBuffer.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Audio error (WaveInOnDataAvailable): " + ex.Message);
            }
        }

        /// <summary>
        /// Called when the modem sends audio data over the serial port.
        /// Buffers it to be played to the speaker.
        /// </summary>
        private void AudioPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytes = audioPort!.BytesToRead;
                if (bytes > 0)
                {
                    var audioData = new byte[bytes];
                    audioPort.Read(audioData, 0, bytes);

                    // If audio buffer is overloaded, clear it to prevent lag
                    if (buffer!.BufferedDuration.TotalMilliseconds >= 100)
                    {
                        if (verboseOutput) Console.WriteLine("Audio buffer full, removing old audio.");
                        buffer.ClearBuffer();
                    }

                    // Add received audio to the speaker buffer
                    buffer.AddSamples(audioData, 0, audioData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AudioPort data receive error: " + ex.Message);
            }
        }

        /// <summary>
        /// Scales raw PCM 16-bit audio samples by a volume factor to suppress echo or adjust gain.
        /// </summary>
        private byte[] AdjustAudioVolume(byte[] buffer, int length, float volumeFactor)
        {
            for (int i = 0; i < length; i += 2)
            {
                // Convert 2 bytes into a 16-bit signed sample
                short sample = BitConverter.ToInt16(buffer, i);
                sample = (short)(sample * volumeFactor); // Scale sample
                byte[] adjustedSample = BitConverter.GetBytes(sample);
                buffer[i] = adjustedSample[0];
                buffer[i + 1] = adjustedSample[1];
            }
            return buffer;
        }

        /// <summary> Closes the audio port if open </summary>
        public void CloseAudio()
        {
            if (audioPort != null && audioPort.IsOpen)
                audioPort.Close();
        }

        /// <summary> Disposes of audio devices and closes serial port </summary>
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
