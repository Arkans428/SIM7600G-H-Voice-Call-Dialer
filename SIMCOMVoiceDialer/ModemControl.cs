using System;
using System.IO.Ports;
using System.Threading;

namespace SIMCOMVoiceDialer
{
    public class ModemControl : IDisposable
    {
        private readonly string atPortName;
        private readonly int baudRate;
        private SerialPort atPort;
        private bool disposed;
        private readonly bool verboseOutput;

        public event Action<string> OnModemResponse;

        public ModemControl(string atPortName, int baudRate, bool verbose = false)
        {
            this.atPortName = atPortName;
            this.baudRate = baudRate;
            this.verboseOutput = verbose;
        }

        public void OpenPort()
        {
            if (atPort == null)
            {
                atPort = new SerialPort(atPortName, baudRate);
                atPort.DataReceived += AtPortDataReceived;
            }

            if (!atPort.IsOpen)
            {
                try
                {
                    atPort.Open();
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

        private void AtPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string response = atPort.ReadExisting();
                if (!string.IsNullOrEmpty(response))
                {
                    if (verboseOutput) Console.WriteLine($"[Modem] {response}");
                    OnModemResponse?.Invoke(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AT Port data receive error: " + ex.Message);
            }
        }

        public void ClosePort()
        {
            if (atPort != null && atPort.IsOpen)
            {
                atPort.Close();
            }
        }

        public void SendCommand(string command)
        {
            if (atPort == null || !atPort.IsOpen)
            {
                Console.WriteLine("AT port is not open.");
                return;
            }
            try
            {
                atPort.WriteLine($"{command}\r");
                Thread.Sleep(800); // small delay for command processing
                if (verboseOutput) Console.WriteLine($"Sent Command: {command}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending AT command: " + ex.Message);
            }
        }

        public void WriteRaw(string data)
        {
            // Check if the port is open before trying to write
            if (atPort == null || !atPort.IsOpen)
            {
                Console.WriteLine("AT port is not open. Cannot write raw data.");
                return;
            }

            try
            {
                // Write exactly the provided data to the serial port (no extra CR/LF)
                atPort.Write(data);

                // Optional: Log what was written if verbose output is enabled
                if (verboseOutput)
                {
                    // Use a helper to make control characters (e.g. Ctrl+Z) visible in logs
                    Console.WriteLine($"[ModemControl Raw TX] {EscapeNonPrintable(data)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while writing raw data: {ex.Message}");
            }
        }

        /// <summary>
        /// A helper to make control characters visible in logs.
        /// For example, ASCII 26 (Ctrl+Z) will appear as "<CTRL+Z>" in console output.
        /// </summary>
        private string EscapeNonPrintable(string input)
        {
            return input
                .Replace("\x1A", "<CTRL+Z>")
                .Replace("\r", "<CR>")
                .Replace("\n", "<LF>");
        }


        public void Dial(string phoneNumber)
        {
            SendCommand($"ATD{phoneNumber};");
            SendCommand("AT+CPCMREG=1");
        }

        public void Answer()
        {
            SendCommand("ATA");
            SendCommand("AT+CPCMREG=1");
        }

        public void HangUp()
        {
            SendCommand("AT+CHUP");
            SendCommand("AT+CPCMREG=0,1");
        }

        public void ClearPortBuffers()
        {
            if (atPort != null && atPort.IsOpen)
            {
                atPort.DiscardInBuffer();
                atPort.DiscardOutBuffer();
            }
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
                ClosePort();
                atPort?.Dispose();
            }
            disposed = true;
        }
    }
}
