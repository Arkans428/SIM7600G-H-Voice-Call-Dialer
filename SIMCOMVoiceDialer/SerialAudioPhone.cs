using System;
using System.Management;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace SIMCOMVoiceDialer
{
    public class SerialAudioPhone : IDisposable
    {
        private readonly ModemControl modem;
        private readonly AudioBridge audio;
        private SmsManager smsManager;
        private bool isCallActive;
        private bool phoneIsRinging;
        private bool disposed;
        private readonly bool verboseOutput;

        // Track whether call waiting is enabled
        // We can store the last known status here after we parse a +CCWA response
        public bool IsCallWaitingEnabled { get; private set; }

        // Device identifiers for the AT port and the Audio port on Windows.
        private const string WindowsAtPortDeviceId = "USB\\VID_1E0E&PID_9005&MI_02";
        private const string WindowsAudioPortDeviceId = "USB\\VID_1E0E&PID_9005&MI_04";

        [SupportedOSPlatform("windows")]
        public SerialAudioPhone(int baudRate = 115200,bool verbose = false)
        {
            verboseOutput = verbose;

            // Locate the serial ports based on their device IDs
            var (atPortName, audioPortName) = FindPortsWindows();

            // Initialize both
            modem = new ModemControl(atPortName!, baudRate, verboseOutput);
            audio = new AudioBridge(audioPortName!, baudRate, verboseOutput);
            // Now create the SmsManager using the same modem
            smsManager = new SmsManager(modem);
            smsManager.Verbose = verbose;

            // Subscribe to events
            modem.OnModemResponse += HandleModemResponse;
            // If you want to handle incoming SMS from here, subscribe:
            smsManager.OnSmsReceived += msg =>
            {
                Console.WriteLine("\n=== [SerialAudioPhone] New SMS ===");
                Console.WriteLine($"Index: {msg.Index}, From: {msg.Sender}, Time: {msg.Timestamp}");
                Console.WriteLine("Text: " + msg.Text);
            };
        }

        public void Initialize()
        {
            // Open both ports
            modem.OpenPort();
            audio.OpenAudio();

            // We can now init SMS
            smsManager.Initialize();

            modem.SendCommand("AT+CLIP=1");
            
            modem.SendCommand("AT+CECM=7");
            modem.SendCommand("AT+CECWB=0x0800");
            modem.SendCommand("AT+CGREG=0"); // Disable automatic gain control
            modem.SendCommand("AT+CMICGAIN=5"); // Set microphone gain
            modem.SendCommand("AT+COUTGAIN=6"); // Set output gain
            modem.SendCommand("AT+CNSN=0x1000");
        }

        // Then you can add helper methods or pass calls through:
        public void SendSms(string recipient, string text)
        {
            smsManager.SendSms(recipient, text);
        }

        public void ReadAllSms()
        {
            smsManager.ReadAllMessages();
        }

        public void DeleteSms(int index)
        {
            smsManager.DeleteMessage(index);
        }

        /// <summary> Dial a number (voice call). </summary>
        public void StartCall(string phoneNumber)
        {
            if (!IsValidPhoneNumber(phoneNumber))
            {
                Console.WriteLine("Invalid phone number.");
                return;
            }

            isCallActive = true;
            ClearBuffers();
            Thread.Sleep(2000);

            Console.WriteLine("Dialing " + phoneNumber);
            modem.Dial(phoneNumber);

            // Start capturing and playing audio
            audio.StartAudio();
        }

        public void AnswerCall()
        {
            if (!phoneIsRinging)
            {
                Console.WriteLine("There's no call to answer!");
                Thread.Sleep(1000);
            }
            else
            {
                phoneIsRinging = false;
                isCallActive = true;
                ClearBuffers();
                Thread.Sleep(2000);
                modem.Answer();

                // Start capturing and playing audio
                audio.StartAudio();
            }
        }

        /// <summary> Hangs up the call if active. </summary>
        public void EndCall()
        {
            if (!isCallActive && !phoneIsRinging)
            {
                Console.WriteLine("There is no active or incoming call");
                Thread.Sleep(1000);
            }
            
            if (phoneIsRinging)
            {
                phoneIsRinging = false;
                modem.HangUp();

                Console.WriteLine("Incoming Call rejected by local user/application.");
                Thread.Sleep(1000);
            }

            if (isCallActive)
            {                
                isCallActive = false;
                modem.HangUp();
                audio.StopAudio();

                if (verboseOutput) Console.WriteLine("Call ended by local user/application.");
            }
        }

        /// <summary>
        /// Handle URCs (Unsolicited Result Codes) from the modem.
        /// We listen for RING, +CLIP, NO CARRIER, etc. to detect state changes.
        /// </summary>
        private void HandleModemResponse(string response)
        {
            // Some lines may be partial or multi-line. If your code accumulates them, adapt accordingly.
            // For demonstration, we assume each 'response' is a chunk that might contain one or more lines.

            // split on typical line boundaries
            string[] lines = response.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {

                if (verboseOutput)
                    Console.WriteLine("[ModemResponse] " + line);

                // 1) Look for "RING"
                //    This indicates an incoming call is ringing.
                if (response.Contains("RING"))
                {
                    if (!phoneIsRinging)
                    {
                        phoneIsRinging = true;
                    }
                    
                    Console.WriteLine("Incoming call is ringing...");
                }

                // 2) Look for the caller’s phone number if CLIP is enabled
                //    For example: +CLIP: "123456789",129
                if (line.StartsWith("+CLIP:"))
                {
                    var clipRegex = new Regex(@"\+CLIP:\s*""([^""]+)"",\d+");
                    var clipMatch = clipRegex.Match(response);
                    if (clipMatch.Success)
                    {
                        string callerNumber = clipMatch.Groups[1].Value;
                        Console.WriteLine($"Incoming call from {callerNumber}");
                        Console.WriteLine("Press A to Answer or H to Reject");
                    }
                }
                

                // 3) Handle “call ended” scenarios
                if (response.Contains("NO CARRIER") ||
                    response.Contains("BUSY") ||
                    response.Contains("ERROR"))
                {
                    if (isCallActive)
                    {
                        Console.WriteLine("Call ended by remote side or an error occurred.");
                        EndCall();
                    }
                }

                // 4) Handle “VOICE CALL: BEGIN” if you want to note when the call is actually connected
                if (response.Contains("VOICE CALL: BEGIN"))
                {
                    Console.WriteLine("Voice call started.");
                    isCallActive = true;
                }

                // 5) If needed, watch for “VOICE CALL: END” in certain firmware versions
                if (response.Contains("VOICE CALL: END"))
                {
                    Console.WriteLine("Voice call ended.");
                    EndCall();
                }

                // If we see +CCWA, parse call waiting status
                if (line.StartsWith("+CCWA:"))
                {
                    // Typically looks like: +CCWA: 1,1  or +CCWA: 1,0
                    // Format can vary. We'll parse with a quick Regex.
                    //   +CCWA: <class>,<status>
                    // where <status> is 1=enabled, 0=disabled
                    var cwRegex = new Regex(@"\+CCWA:\s*(\d+),(\d+)");
                    var cwMatch = cwRegex.Match(response);
                    // var cwmatch = Regex.Match(line, @"\+CCWA:\s*(\d+),(\d+)");
                    if (cwMatch.Success)
                    {
                        // group 1 => class (1=voice)
                        // group 2 => status (0=disabled, 1=enabled)
                        int statusVal = int.Parse(cwMatch.Groups[2].Value);
                        IsCallWaitingEnabled = (statusVal == 1);

                        if (verboseOutput)
                        {
                            Console.WriteLine($"[CallWaiting] Voice call waiting => {(IsCallWaitingEnabled ? "ENABLED" : "DISABLED")}");
                        }
                    }
                }
            }
        }

        /// <summary> Clear leftover data in modem's serial buffers. </summary>
        private void ClearBuffers()
        {
            // Clear leftover data in both ports
            modem.ClearPortBuffers();
            audio.ClearPortBuffers();
        }

        private bool IsValidPhoneNumber(string number)
        {
            return !string.IsNullOrWhiteSpace(number);
        }

        /// <summary>
        /// Enable or disable call waiting for voice calls.
        /// </summary>
        public void SetCallWaiting(bool enable)
        {
            // mode = enable ? 1 : 0
            // class = 1 for voice
            // So the command is: AT+CCWA=1,<mode>,1
            int modeVal = enable ? 1 : 0;
            modem.SendCommand($"AT+CCWA=1,{modeVal},1");

            if (verboseOutput)
                Console.WriteLine($"[CallWaiting] Requested to {(enable ? "enable" : "disable")} call waiting.");
        }

        /// <summary>
        /// Query call waiting status. The response will come back in a +CCWA line, which we parse.
        /// </summary>
        public void QueryCallWaiting()
        {
            // mode = 2 => query
            // class = 1 => voice
            modem.SendCommand("AT+CCWA=1,2,1");
            Thread.Sleep(500);

            if (verboseOutput)
                Console.WriteLine("[CallWaiting] Queried call waiting status...");
        }

        public void SetCallForwarding(CallForwardReason reason, bool enable, string? forwardNumber = null)
        {
            // reason => 0..5
            // mode => enable=1 or disable=0
            int reasonVal = (int)reason;
            int modeVal = enable ? 1 : 0;

            // If enabling, we must supply a forwarding number:
            if (enable)
            {
                if (string.IsNullOrWhiteSpace(forwardNumber))
                {
                    Console.WriteLine("Error: Forwarding number must be provided if enabling call forward.");
                    return;
                }

                // For typical usage, <type> = 129 (unknown or international)
                // Example: enable unconditional call forward to 'forwardNumber'
                modem.SendCommand($"AT+CCFC={reasonVal},{modeVal},\"{forwardNumber}\",129");
            }
            else
            {
                // Disabling doesn't need a number
                // Example: disable unconditional call forward
                modem.SendCommand($"AT+CCFC={reasonVal},0");
            }
        }

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                EndCall();
                modem.Dispose();
                audio.Dispose();
            }
            disposed = true;
        }
    }
}
