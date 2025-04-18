using System;
using System.IO.Ports;
using System.Threading;

namespace SIMCOMVoiceDialer
{
    // Handles AT command communication with the modem over a serial port
    public class ModemControl : IDisposable
    {
        private readonly string atPortName;          // Name of the serial port to communicate with the modem (e.g., COM3)
        private readonly int baudRate;               // Baud rate for serial communication (e.g., 115200)
        private SerialPort atPort;                   // The actual serial port object
        private bool disposed;                       // Tracks whether the object has been disposed
        private readonly bool verboseOutput;         // Enables extra logging if true

        // Event triggered when a response is received from the modem
        public event Action<string> OnModemResponse;

        // Constructor initializes port configuration and verbosity
        public ModemControl(string atPortName, int baudRate, bool verbose = false)
        {
            this.atPortName = atPortName;
            this.baudRate = baudRate;
            this.verboseOutput = verbose;
        }

        // Opens and initializes the serial port connection
        public void OpenPort()
        {
            if (atPort == null)
            {
                atPort = new SerialPort(atPortName, baudRate);
                atPort.DataReceived += AtPortDataReceived; // Register data receive event handler
            }

            if (!atPort.IsOpen)
            {
                try
                {
                    atPort.Open(); // Attempt to open the port
                    if (verboseOutput) Console.WriteLine($"AT Port opened on {atPortName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error opening AT Port: " + ex.Message);
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadLine();
                }
            }
        }

        // Handles incoming data from the modem
        private void AtPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string response = atPort.ReadExisting(); // Read available data
                if (!string.IsNullOrEmpty(response))
                {
                    if (verboseOutput) Console.WriteLine($"[Modem] {response}");
                    OnModemResponse?.Invoke(response); // Raise event with received data
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AT Port data receive error: " + ex.Message);
            }
        }

        // Closes the serial port if it's open
        public void ClosePort()
        {
            if (atPort != null && atPort.IsOpen)
            {
                atPort.Close();
            }
        }

        // Sends an AT command to the modem
        public void SendCommand(string command)
        {
            if (atPort == null || !atPort.IsOpen)
            {
                Console.WriteLine("AT port is not open.");
                return;
            }
            try
            {
                atPort.WriteLine($"{command}\r"); // Write command with carriage return
                Thread.Sleep(800); // Short delay to allow command processing
                if (verboseOutput) Console.WriteLine($"Sent Command: {command}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending AT command: " + ex.Message);
            }
        }

        // Sends raw data to the modem without appending carriage return or line feed
        public void WriteRaw(string data)
        {
            if (atPort == null || !atPort.IsOpen)
            {
                Console.WriteLine("AT port is not open. Cannot write raw data.");
                return;
            }

            try
            {
                atPort.Write(data); // Write raw bytes or characters directly

                if (verboseOutput)
                {
                    // Show escaped version of non-printable characters
                    Console.WriteLine($"[ModemControl Raw TX] {EscapeNonPrintable(data)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while writing raw data: {ex.Message}");
            }
        }

        /// <summary>
        /// Replaces control characters in strings with readable tags (e.g., Ctrl+Z becomes <CTRL+Z>).
        /// This helps when logging command sequences or binary data.
        /// </summary>
        private string EscapeNonPrintable(string input)
        {
            return input
                .Replace("\x1A", "<CTRL+Z>")
                .Replace("\r", "<CR>")
                .Replace("\n", "<LF>");
        }

        // Sends a dial command to the modem for voice call
        public void Dial(string phoneNumber)
        {
            SendCommand($"ATD{phoneNumber};");    // AT dial command (with semicolon for voice)
            SendCommand("AT+CPCMREG=1");          // Enable PCM interface (audio)
        }

        // Answers an incoming call
        public void Answer()
        {
            SendCommand("ATA");                   // Answer call
            SendCommand("AT+CPCMREG=1");          // Enable PCM interface
        }

        // Hangs up the active call
        public void HangUp()
        {
            SendCommand("AT+CHUP");               // Hang up command
            SendCommand("AT+CPCMREG=0,1");        // Disable PCM interface
        }

        // Clears serial port input and output buffers to prevent old data from being processed
        public void ClearPortBuffers()
        {
            if (atPort != null && atPort.IsOpen)
            {
                atPort.DiscardInBuffer();         // Clear input buffer
                atPort.DiscardOutBuffer();        // Clear output buffer
            }
        }

        // Implements IDisposable to release resources cleanly
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);            // Prevents finalizer from running
        }

        // Handles cleanup logic
        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                ClosePort();                      // Close port gracefully
                atPort?.Dispose();                // Dispose of the serial port object
            }
            disposed = true;
        }
    }
}
