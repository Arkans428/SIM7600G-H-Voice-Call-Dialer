using SIMCOMVoiceDialer.DataModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIMCOMVoiceDialer
{
    public class SmsManager : IDisposable
    {
        private readonly ModemControl modem;
        private bool disposed;

        /// <summary>
        /// Buffers partial lines if the modem provides partial chunks in OnModemResponse.
        /// We'll accumulate them until we can parse full lines.
        /// </summary>
        private string partialBuffer = string.Empty;

        /// <summary>
        /// Fires when a new SMS is fully parsed from either +CMT:, +CMGR, or +CMGL lines.
        /// </summary>
        public event Action<SmsMessage> OnSmsReceived;

        /// <summary>
        /// If true, extra debugging messages will appear in the console.
        /// </summary>
        public bool Verbose { get; set; } = false;

        /// <summary>
        /// Create the manager, hooking into the given ModemControl instance.
        /// The modem must already be created in your SerialAudioPhone.
        /// </summary>
        public SmsManager(ModemControl modemControl)
        {
            this.modem = modemControl ?? throw new ArgumentNullException(nameof(modemControl));

            // Subscribe to all unsolicited responses from the modem
            modem.OnModemResponse += HandleModemResponse;
        }

        /// <summary>
        /// Call this once after the modem is opened, to set text-mode SMS and so on.
        /// </summary>
        public void Initialize()
        {
            SetupSmsEnvironment();
        }

        /// <summary>
        /// Configures the modem for text-mode SMS, sets the character set,
        /// and enables new-message indications in +CMT: lines.
        /// </summary>
        private void SetupSmsEnvironment()
        {
            // 1. Set text mode
            modem.SendCommand("AT+CMGF=1"); // Text mode

            // 2. Set the default character set
            modem.SendCommand("AT+CSCS=\"GSM\"");

            // 3. Set text mode parameters if needed (example: <fo>=17, <vp>=167, <pid>=0, <dcs>=0)
            modem.SendCommand("AT+CSMP=17,167,0,0");

            // 4. Enable URC for incoming SMS in text mode
            // "2,2,0,0,0" => direct display via +CMT:
            modem.SendCommand("AT+CNMI=2,2,0,0,0");
        }

        /// <summary>
        /// Sends a text-mode SMS to the specified recipient, with the given message content.
        /// </summary>
        /// <param name="recipient">The phone number (e.g. "+123456789")</param>
        /// <param name="message">The text body of the SMS.</param>
        public void SendSms(string recipient, string message)
        {
            if (string.IsNullOrWhiteSpace(recipient))
            {
                Console.WriteLine("No recipient specified. SMS not sent.");
                return;
            }
            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Cannot send an empty message.");
                return;
            }

            // AT+CMGS="recipient"
            modem.SendCommand($"AT+CMGS=\"{recipient}\"");
            // The modem should respond with '>' prompt. We'll wait a bit
            Thread.Sleep(500);

            // Now send the text plus Ctrl+Z (ASCII 26)
            // We can't use modem.SendCommand here because that adds \r\n
            // So we can call modem.SendRaw if you have one, or add a small helper:
            modem.WriteRaw(message + "\x1A"); // Ctrl+Z => \x1A

            if (Verbose)
                Console.WriteLine($"[SMS Out -> {recipient}] {message}");
        }

        /// <summary>
        /// Reads all messages from the SIM or internal memory (status "ALL").
        /// The modem returns +CMGL lines which we parse in HandleModemResponse.
        /// </summary>
        public void ReadAllMessages()
        {
            modem.SendCommand("AT+CMGL=\"ALL\"");
        }

        /// <summary>
        /// Reads a single message from the given index.
        /// The modem returns +CMGR line(s) which we parse in HandleModemResponse.
        /// </summary>
        public void ReadMessage(int index)
        {
            modem.SendCommand($"AT+CMGR={index}");
        }

        /// <summary>
        /// Delete a single message by index from the SIM or internal memory.
        /// </summary>
        public void DeleteMessage(int index)
        {
            modem.SendCommand($"AT+CMGD={index}");
        }

        /// <summary>
        /// Delete up to 50 messages in a loop. Adjust max if needed.
        /// This is a brute force approach to clearing messages from memory.
        /// </summary>
        public void DeleteAllMessages()
        {
            for (int i = 1; i <= 50; i++)
            {
                modem.SendCommand($"AT+CMGD={i}");
                Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Callback that handles *all* lines from the modem. We parse partial lines, 
        /// watch for +CMT: or +CMTI: or +CMGR: or +CMGL:, etc., indicating SMS data.
        /// </summary>
        private void HandleModemResponse(string responseChunk)
        {
            // The 'responseChunk' might contain partial lines, multiple lines, or leftover data.
            // We'll accumulate in partialBuffer, then split on newlines to parse fully formed lines.
            partialBuffer += responseChunk;

            var lines = partialBuffer.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // The last element might be incomplete, so store it for next time:
            partialBuffer = lines[lines.Length - 1];

            // parse all lines except the last partial
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                //if (Verbose) Console.WriteLine($"[SMSManager RX] {line}");

                // 1) +CMT: => immediate new message
                if (line.StartsWith("+CMT:"))
                {
                    // The next line (i+1) might be the message text (assuming the chunk has it).
                    // But we have to be sure we don't go out of range.
                    string textBody = "";
                    if (i + 1 < lines.Length - 1)
                    {
                        textBody = lines[i + 1];
                        // We'll skip that line in the loop
                        lines[i + 1] = "";
                    }

                    var sms = ParseCmtLine(line, textBody);
                    OnSmsReceived?.Invoke(sms);
                }
                // 2) +CMTI: => new message index reported, e.g. +CMTI: "SM",3
                else if (line.StartsWith("+CMTI:"))
                {
                    var match = Regex.Match(line, @"\+CMTI:\s*""[^""]+"",(\d+)");
                    if (match.Success)
                    {
                        int msgIndex = int.Parse(match.Groups[1].Value);
                        if (Verbose) Console.WriteLine($"[SMS] New message at index {msgIndex}");
                        // If you want to read it automatically:
                        ReadMessage(msgIndex);
                    }
                }
                // 3) +CMGR: or +CMGL: => reading stored messages
                else if (line.StartsWith("+CMGR:") || line.StartsWith("+CMGL:"))
                {
                    // the text is presumably on the next line
                    string textLine = "";
                    if (i + 1 < lines.Length - 1)
                    {
                        textLine = lines[i + 1];
                        lines[i + 1] = "";
                    }

                    var sms = ParseCmgrOrCmglLine(line, textLine);
                    OnSmsReceived?.Invoke(sms);
                }
                else
                {
                    // Some other URC or response
                }
            }
        }

        /// <summary>
        /// Parse a +CMT: line and the subsequent text line.
        /// Typical format:
        /// +CMT: ,145,"+123456789",,"23/05/14,10:30:10+08"
        /// Hello from me
        /// </summary>
        private SmsMessage ParseCmtLine(string header, string textLine)
        {
            var msg = new SmsMessage { Index = -1 }; // immediate messages are not stored yet

            // Try to find phone number in quotes
            var phoneMatch = Regex.Match(header, @"""(\+?\d+)""");
            if (phoneMatch.Success)
            {
                msg.Sender = phoneMatch.Groups[1].Value;
            }

            // Try to find date/time
            var dtMatch = Regex.Match(header, @"(\d{2}/\d{2}/\d{2},\d{2}:\d{2}:\d{2}\+\d{2})");
            if (dtMatch.Success)
            {
                msg.Timestamp = dtMatch.Value;
            }

            msg.Text = textLine;

            return msg;
        }

        /// <summary>
        /// Parse +CMGR: or +CMGL: line plus the next line containing the text.
        /// Example:
        /// +CMGR: "REC UNREAD","+123456789",,"23/05/14,10:30:10+08"
        /// Hello from me
        /// or
        /// +CMGL: 1,"REC UNREAD","+123456789",,"23/05/14,10:32:01+08"
        /// Another message
        /// </summary>
        private SmsMessage ParseCmgrOrCmglLine(string header, string textLine)
        {
            var msg = new SmsMessage();

            // If it's +CMGL: <index>, ...
            var cmglMatch = Regex.Match(header, @"\+CMGL:\s*(\d+)");
            if (cmglMatch.Success)
            {
                msg.Index = int.Parse(cmglMatch.Groups[1].Value);
            }
            // If it's +CMGR:, there's no index. We'll keep Index = -1.

            // Phone number:
            var phoneMatch = Regex.Match(header, @"""(\+?\d+)""");
            if (phoneMatch.Success)
            {
                msg.Sender = phoneMatch.Groups[1].Value;
            }

            // Date/time:
            var dtMatch = Regex.Match(header, @"(\d{2}/\d{2}/\d{2},\d{2}:\d{2}:\d{2}\+\d{2})");
            if (dtMatch.Success)
            {
                msg.Timestamp = dtMatch.Value;
            }

            msg.Text = textLine;
            return msg;
        }

        public void Dispose()
        {
            if (disposed) return;
            // Unsubscribe from modem events
            modem.OnModemResponse -= HandleModemResponse;
            disposed = true;
        }
    }

}
