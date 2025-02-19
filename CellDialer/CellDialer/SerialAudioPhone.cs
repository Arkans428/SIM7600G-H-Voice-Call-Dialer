using System.IO.Ports;
using System.Management;
using System.Runtime.Versioning;
using NAudio.Wave;

namespace ModemDialer
{
    public class SerialAudioPhone : IDisposable
    {
        #region Fields and Configuration

        // Serial ports for AT commands and audio data
        private readonly SerialPort? atPort; // Serial port for sending AT commands
        private readonly SerialPort? audioPort; // Serial port for asyncronous audio data

        private readonly WaveInEvent? waveIn; // Handles capturing audio input (Windows)
        private readonly WaveOutEvent? waveOut; // Handles playing audio output (Windows)
        private readonly BufferedWaveProvider? buffer; // Buffers the captured audio data (Windows)

        private bool isCallActive; // Flag indicating whether a call is currently active
        private bool disposed = false; // Tracks whether the object has been disposed
        // private bool isEchoSuppressionEnabled = true; // Flag to enable/disable echo suppression (Not Implemeted)
        private readonly float echoSuppressionFactor = 0.5f; // Echo suppression level (range 0 to 1)
        private readonly bool verboseOutput = false; // Flag to control verbose logging for debugging

        // Device identifiers for the AT port and the Audio port on Windows.
        private const string WindowsAtPortDeviceId = "USB\\VID_1E0E&PID_9005&MI_02";
        private const string WindowsAudioPortDeviceId = "USB\\VID_1E0E&PID_9005&MI_04";

        #endregion

        #region Constructor
        
        [SupportedOSPlatform("windows")]
        public SerialAudioPhone(int baudRate = 115200, int sampleRate = 8000, int channels = 1, bool verbose = false)
        {
            // Enable verbose output if requested
            verboseOutput = verbose;

            // Locate the serial ports based on their device IDs
            var (atPortName, audioPortName) = FindPortsWindows();

            // If either port is not found, throw an exception
            if (atPortName is null || audioPortName is null)
            {
                throw new InvalidOperationException("Unable to locate one or both required serial ports.");
            }

            // Initialize the serial ports using the detected port names
            atPort = new SerialPort(atPortName, baudRate);
            audioPort = new SerialPort(audioPortName, baudRate);

            try
            {
                atPort.Open();
                audioPort.Open();
                Console.WriteLine($"AT Port opened on {atPortName}");
                Console.WriteLine($"Audio Port opened on {audioPortName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening serial ports: {ex.Message}");
            }

            // Configure the input device for capturing audio (microphone) on Windows
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, channels),
                BufferMilliseconds = 30
            };

            // Configure the output device for playing audio (speakers) on Windows
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 50,
                NumberOfBuffers = 4
            };

            // Initialize the buffer for storing audio data (Windows)
            buffer = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferLength = 4096,
                DiscardOnBufferOverflow = true
            };

            waveOut.Volume = 0.7f;

            // Attach event handler for capturing and processing audio data on Windows
            waveIn.DataAvailable += (sender, e) =>
            {
                try
                {
                    float adjustedVolume = waveOut.PlaybackState == PlaybackState.Playing ? echoSuppressionFactor : 1.0f;
                    byte[] adjustedBuffer = AdjustAudioVolume(e.Buffer, e.BytesRecorded, adjustedVolume);
                    audioPort.Write(adjustedBuffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Audio error: " + ex.Message);
                }
            };

            waveOut.Init(buffer);


            isCallActive = false;
        }

        #endregion
               

        #region Core Call Management Methods

        // Start a phone call to a specified phone number
        public void StartCall(string phoneNumber)
        {
            try
            {
                // Validate the phone number using a custom validation function
                if (!IsValidPhoneNumber(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return;
                }

                isCallActive = true; // Set the call as active

                // Open the AT command port if it's not already open
                if (atPort?.IsOpen == false)
                {
                    atPort.Open();
                }

                // Open the audio port if it's not already open
                if (audioPort?.IsOpen == false)
                {
                    audioPort.Open();
                }

                // Clear any leftover data in the serial buffers before starting the call
                atPort?.DiscardInBuffer();
                atPort?.DiscardOutBuffer();
                audioPort?.DiscardInBuffer();
                audioPort?.DiscardOutBuffer();

                Thread.Sleep(300); // Short delay to ensure the ports are fully initialized

                // Place the call
                PhoneLineControl($"ATD{phoneNumber};");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting the call: " + ex.Message);
            }
            finally
            {
                EndCall(); // Ensure the call is properly ended and resources are released
            }
        }

        // Answer an incoming call
        public void AnswerCall()
        {
            try
            {
                isCallActive = true; // Set the call as active

                // Open the AT command port if it's not already open
                if (atPort?.IsOpen == false)
                {
                    atPort.Open();
                }

                // Open the audio port if it's not already open
                if (audioPort?.IsOpen == false)
                {
                    audioPort.Open();
                }

                // Clear any leftover data in the serial buffers before starting the call
                // atPort?.DiscardInBuffer();
                // atPort?.DiscardOutBuffer();
                audioPort?.DiscardInBuffer();
                audioPort?.DiscardOutBuffer();

                Thread.Sleep(300); // Short delay to ensure the ports are fully initialized

                // Answer the call
                PhoneLineControl("ATA");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error answering the call: " + ex.Message);
            }
            finally
            {
                EndCall(); // Ensure the call is properly ended and resources are released
            }
        }

        public void PhoneLineControl(string command)
        {
            // Configuration
            // Send necessary AT commands to configure the call settings   
            SendCommand("AT+CGREG=0"); // Disable automatic gain control
            SendCommand("AT+CECM=7");
            SendCommand("AT+CECWB=0x0800");
            SendCommand("AT+CMICGAIN=5"); // Set microphone gain
            SendCommand("AT+COUTGAIN=4"); // Set output gain
            SendCommand("AT+CNSN=0x1000");

            SendCommand(command); // This should be either dialing a number (ATD777777777;) or answering a call (ATA)

            // Enable audio transmission over the serial port
            SendCommand("AT+CPCMREG=1");

            waveIn?.StartRecording(); // Start capturing audio from the microphone
            waveOut?.Play(); // Start playing received audio

            // Start a thread to monitor keyboard input for user interaction
            Thread inputThread = new(MonitorKeyboardInput)
            {
                Priority = ThreadPriority.Highest
            };
            inputThread.Start();

            // Main loop to manage call activity
            while (isCallActive)
            {
                try
                {
                    // Check if the call is still active
                    if (atPort != null && atPort.BytesToRead > 0)
                    {
                        string response = atPort.ReadExisting();
                        if (verboseOutput) Console.WriteLine(response);

                        // If modem says "NO CARRIER", exit loop
                        if (response.Contains("NO CARRIER") || response.Contains("BUSY") || response.Contains("ERROR"))
                        {
                            Console.WriteLine("Call Ended by Remote.");
                            isCallActive = false;
                        }
                    }

                    // Read and play incoming audio data from the audio port
                    if (audioPort != null && audioPort.BytesToRead > 0)
                    {
                        byte[] audioData = new byte[audioPort.BytesToRead];
                        audioPort.Read(audioData, 0, audioData.Length);

                        // Check if buffer is full, and remove old audio before adding new data
                        if (buffer?.BufferedDuration.TotalMilliseconds >= 100)
                        {
                            if (verboseOutput) Console.WriteLine("Buffer full, removing old audio to avoid overflow.");
                            buffer.ClearBuffer();  // This removes old audio before adding new samples
                        }
                        buffer?.AddSamples(audioData, 0, audioData.Length);
                    }

                    Thread.Sleep(10); // Short delay to reduce CPU usage while maintaining low latency
                }
                catch (IOException ex)
                {
                    Console.WriteLine("I/O Error during call handling: " + ex.Message);
                    isCallActive = false;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.WriteLine("Access Error during call handling: " + ex.Message);
                    isCallActive = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unexpected Error during call handling: " + ex.Message);
                    isCallActive = false;
                }
            }
        }

        // Method to send a DTMF (Dual-Tone Multi-Frequency) tone during a call
        public void SendDtmfTone(char tone)
        {
            // Validate that the tone is a valid DTMF character (0-9, *, #, A-D)
            if ("0123456789*#ABCD".Contains(tone))
            {
                // Send the AT command to generate the specified DTMF tone
                SendCommand($"AT+VTS={tone}");

                // Optionally display the sent tone if verbose output is enabled
                if (verboseOutput) Console.WriteLine($"Sent DTMF tone: {tone}");
            }
            else
            {
                // Display an error message if the input tone is not valid
                Console.WriteLine($"Invalid DTMF tone: {tone}");
            }
        }

        // Monitor user input for ending the call or sending DTMF tones
        private void MonitorKeyboardInput()
        {
            try
            {
                Console.WriteLine("Press 'Esc' to end the call. Press any number key, *, #, A, B, C, or D to send DTMF tones.");
                while (isCallActive)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        if (key == ConsoleKey.Escape)
                        {
                            isCallActive = false; // End the call if 'Esc' is pressed
                        }
                        else
                        {
                            // Check if the key corresponds to a valid DTMF tone
                            char dtmfTone = key switch
                            {
                                ConsoleKey.D1 => '1',
                                ConsoleKey.D2 => '2',
                                ConsoleKey.D3 => '3',
                                ConsoleKey.D4 => '4',
                                ConsoleKey.D5 => '5',
                                ConsoleKey.D6 => '6',
                                ConsoleKey.D7 => '7',
                                ConsoleKey.D8 => '8',
                                ConsoleKey.D9 => '9',
                                ConsoleKey.D0 => '0',
                                ConsoleKey.A => 'A',
                                ConsoleKey.B => 'B',
                                ConsoleKey.C => 'C',
                                ConsoleKey.D => 'D',
                                ConsoleKey.Oem1 => '*',
                                ConsoleKey.OemPlus => '#',
                                _ => '\0'
                            };

                            if (dtmfTone != '\0')
                            {
                                SendDtmfTone(dtmfTone); // Send the DTMF tone if it's valid
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error monitoring keyboard input: " + ex.Message);
            }
        }

        // End the call and release resources
        public void EndCall()
        {
            try
            {
                SendCommand("AT+CHUP"); // Hang up the call
                SendCommand("AT+CPCMREG=0,1"); // Disable the audio channel on the modem


                waveIn?.StopRecording(); // Stop capturing audio (Windows)
                waveOut?.Stop(); // Stop playing audio (Windows)

                if (verboseOutput) Console.WriteLine("Call ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ending the call: " + ex.Message);
            }
        }

        #endregion

        #region Utility Methods

        // Find the appropriate serial ports for Windows systems
        [SupportedOSPlatform("windows")]
        private static (string? AtPort, string? AudioPort) FindPortsWindows()
        {
            string? atPort = null;
            string? audioPort = null;

            try
            {
                // Query the system for USB devices using WMI (Windows Management Instrumentation)
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity");

                foreach (var device in searcher.Get())
                {
                    string? deviceId = device["DeviceID"]?.ToString();
                    string? name = device["Name"]?.ToString();
                    if (deviceId != null && name != null)
                    {
                        if (deviceId.Contains(WindowsAtPortDeviceId))
                        {
                            atPort = name.Split('(').LastOrDefault()?.Replace(")", ""); // Extract COM port name for AT port
                        }
                        else if (deviceId.Contains(WindowsAudioPortDeviceId))
                        {
                            audioPort = name.Split('(').LastOrDefault()?.Replace(")", ""); // Extract COM port name for Audio port
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting ports on Windows: {ex.Message}");
            }

            return (atPort, audioPort);
        }

        // Send an AT command through the serial port
        private void SendCommand(string command)
        {
            try
            {
                atPort?.WriteLine($"{command}\r"); // Send the command followed by a carriage return
                Thread.Sleep(60); // Short delay for command processing

                if (verboseOutput) Console.WriteLine($"Sent Command: {command}");
            }
            catch (IOException ex)
            {
                Console.WriteLine("I/O Error sending AT command: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("Access Error sending AT command: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected Error sending AT command: " + ex.Message);
            }
        }

        // Validate a phone number (simple validation)
        private static bool IsValidPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return false; // Invalid if null or empty
            }

            return true; // Valid if it passes basic checks
        }

        // Adjust the volume of an audio buffer by scaling the samples
        private static byte[] AdjustAudioVolume(byte[] buffer, int length, float volumeFactor)
        {
            for (int i = 0; i < length; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sample = (short)(sample * volumeFactor); // Scale the audio sample by the volume factor
                byte[] adjustedSample = BitConverter.GetBytes(sample);
                buffer[i] = adjustedSample[0];
                buffer[i + 1] = adjustedSample[1];
            }
            return buffer;
        }

        #endregion

        #region Disposal Methods

        // Dispose pattern implementation to release resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Suppress finalization since manual disposal is handled
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return; // If already disposed, exit

            if (disposing)
            {
                waveIn?.Dispose(); // Dispose managed resources (Windows)
                waveOut?.Dispose(); // Dispose managed resources (Windows)

                // Close and dispose serial ports if they are open
                if (atPort?.IsOpen == true)
                {
                    atPort.Close();
                }
                atPort?.Dispose();

                if (audioPort?.IsOpen == true)
                {
                    audioPort.Close();
                }
                audioPort?.Dispose();
            }

            disposed = true; // Mark as disposed
        }

        ~SerialAudioPhone()
        {
            Dispose(false); // Destructor calls Dispose(false) for unmanaged resource cleanup
        }

        #endregion
    }
}
