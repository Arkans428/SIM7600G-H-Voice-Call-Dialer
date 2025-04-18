using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIMCOMVoiceDialer.DataModel
{
    public class SmsMessage
    {
        /// <summary>
        /// If the SMS was read from storage (like +CMGL or +CMGR),
        /// this is its index in the SIM or internal memory.
        /// For messages received via +CMT:, it can be -1 (not stored).
        /// </summary>
        public int Index { get; set; } = -1;

        /// <summary>
        /// The sender's phone number.
        /// </summary>
        public string Sender { get; set; }

        /// <summary>
        /// The timestamp from the SMSC or local parse. Format can vary.
        /// </summary>
        public string Timestamp { get; set; }

        /// <summary>
        /// The text body of the message.
        /// </summary>
        public string Text { get; set; }
    }
}
