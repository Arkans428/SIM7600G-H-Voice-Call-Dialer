using System;

namespace SIMCOMVoiceDialer
{
    class Program
    {
        // Entry point of the application
        static void Main()
        {
            // Initialize the SerialAudioPhone instance with 115200 baud rate and verbose logging enabled
            using var phone = new SerialAudioPhone(115200, verbose: true);
            phone.Initialize(); // Set up and configure the phone hardware

            bool done = false; // Control flag for the main loop

            // Main interactive menu loop
            while (!done)
            {
                Console.Clear(); // Clear the console for fresh display
                Console.WriteLine("++++++++++ Main Menu++++++++++");
                Console.WriteLine("Press D to dial, A to answer, H to hang up, Q to quit.");
                Console.WriteLine("Press F to configure call forwarding");
                Console.WriteLine("Press W to configure Call Waiting");
                Console.WriteLine("Press S for the SMS Menu");

                // Read user's key press
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.D:
                        Console.Write("Enter number to dial: ");
                        string number = Console.ReadLine()!;
                        phone.StartCall(number!); // Dial the specified number
                        break;

                    case ConsoleKey.A:
                        phone.AnswerCall(); // Answer incoming call
                        break;

                    case ConsoleKey.H:
                        phone.EndCall(); // Hang up active call
                        break;

                    case ConsoleKey.F:
                        CallForwarding(phone); // Go to call forwarding menu
                        break;

                    case ConsoleKey.S:
                        SmsMenu(phone); // Go to SMS menu
                        break;

                    case ConsoleKey.W:
                        CallWaitingMenu(phone); // Go to call waiting menu
                        break;

                    case ConsoleKey.Q:
                        done = true; // Exit the main loop
                        break;
                }
            }

            phone.Dispose(); // Clean up and release the phone resources
        }

        // Menu to manage Call Waiting features
        static void CallWaitingMenu(SerialAudioPhone phone)
        {
            Console.WriteLine("Call Waiting Menu:");
            Console.WriteLine("  W - Toggle call waiting (Enable if currently disabled, otherwise disable)");
            Console.WriteLine("  Q - Query call waiting status");
            Console.WriteLine("  X - Main Menu");

            bool done = false; // Control flag for the menu loop
            while (!done)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.W:
                        // Toggle call waiting based on current setting
                        bool newSetting = !phone.IsCallWaitingEnabled;
                        phone.SetCallWaiting(newSetting);
                        break;

                    case ConsoleKey.Q:
                        phone.QueryCallWaiting(); // Display current call waiting status
                        break;

                    case ConsoleKey.M:
                        done = true; // Return to main menu
                        break;
                }
            }
        }

        // Menu to manage SMS functionality
        static void SmsMenu(SerialAudioPhone phone)
        {
            Console.Clear();
            Console.WriteLine("Press 'S' to send SMS, 'R' to read all, 'M' for Main Menu.");
            bool done = false;
            while (!done)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.S:
                        // Prompt user to input recipient and message text
                        Console.Write("Enter recipient: ");
                        string number = Console.ReadLine()!;
                        Console.Write("Enter SMS text: ");
                        string text = Console.ReadLine()!;
                        phone.SendSms(number, text); // Send the SMS
                        break;

                    case ConsoleKey.R:
                        phone.ReadAllSms(); // Read and display all received SMS messages
                        break;

                    case ConsoleKey.M:
                        done = true; // Return to main menu
                        break;
                }
            }
        }

        // Menu to manage call forwarding options
        static void CallForwarding(SerialAudioPhone phone)
        {
            bool done = false;
            while (!done)
            {
                Console.Clear(); // Clear previous menu display
                Console.WriteLine("\nCall Forwarding Menu:");
                Console.WriteLine("1) Enable unconditional CF");
                Console.WriteLine("2) Disable unconditional CF");
                Console.WriteLine("3) Enable busy CF");
                Console.WriteLine("4) Disable busy CF");
                Console.WriteLine("M) Main Menu");

                var choice = Console.ReadKey(true).Key;

                switch (choice)
                {
                    case ConsoleKey.D1:
                        // Enable unconditional call forwarding
                        Console.Write("Enter forward-to number: ");
                        var forwardNumber = Console.ReadLine();
                        phone.SetCallForwarding(
                            CallForwardReason.Unconditional,
                            enable: true,
                            forwardNumber: forwardNumber
                        );
                        break;

                    case ConsoleKey.D2:
                        // Disable unconditional call forwarding
                        phone.SetCallForwarding(CallForwardReason.Unconditional, enable: false);
                        break;

                    case ConsoleKey.D3:
                        // Enable call forwarding when line is busy
                        Console.Write("Enter forward-to number: ");
                        var forwardNumberBusy = Console.ReadLine();
                        phone.SetCallForwarding(
                            CallForwardReason.Busy,
                            enable: true,
                            forwardNumber: forwardNumberBusy
                        );
                        break;

                    case ConsoleKey.D4:
                        // Disable call forwarding when line is busy
                        phone.SetCallForwarding(CallForwardReason.Busy, enable: false);
                        break;

                    case ConsoleKey.M:
                        done = true; // Return to main menu
                        break;
                }
            }
        }
    }
}
