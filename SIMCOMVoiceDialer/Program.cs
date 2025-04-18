using System;

namespace SIMCOMVoiceDialer
{
    class Program
    {
        static void Main()
        {
            using var phone = new SerialAudioPhone(115200, verbose: true);
            phone.Initialize();

            
            bool done = false;
            while (!done)
            {
                Console.Clear();
                // Example usage:
                Console.WriteLine("++++++++++ Main Menu++++++++++");
                Console.WriteLine("Press D to dial, A to answer, H to hang up, Q to quit.");
                Console.WriteLine("Press F to configure call forwarding");
                Console.WriteLine("Press W to configure Call Waiting");
                Console.WriteLine("Press S for the SMS Menu");

                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.D:
                        Console.Write("Enter number to dial: ");
                        string number = Console.ReadLine()!;
                        phone.StartCall(number!);
                        break;
                    case ConsoleKey.A:
                        phone.AnswerCall();
                        break;
                    case ConsoleKey.H:
                        phone.EndCall();
                        break;
                    case ConsoleKey.F:
                        CallForwarding(phone);
                        break;
                    case ConsoleKey.S:
                        SmsMenu(phone);
                        break;
                    case ConsoleKey.W:
                        CallWaitingMenu(phone);
                        break;
                    case ConsoleKey.Q:
                        done = true;
                        break;
                }
            }

            phone.Dispose();
        }

        static void CallWaitingMenu(SerialAudioPhone phone)
        {
            Console.WriteLine("Call Waiting Menu:");
            Console.WriteLine("  W - Toggle call waiting (Enable if currently disabled, otherwise disable)");
            Console.WriteLine("  Q - Query call waiting status");
            Console.WriteLine("  X - Main Menu");

            bool done = false;
            while (!done)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.W:
                        // If it's off, enable it; if it's on, disable it
                        bool newSetting = !phone.IsCallWaitingEnabled;
                        phone.SetCallWaiting(newSetting);
                        break;

                    case ConsoleKey.Q:
                        phone.QueryCallWaiting();
                        break;

                    case ConsoleKey.M:
                        done = true;
                        break;
                }
            }
        }

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
                        Console.Write("Enter recipient: ");
                        string number = Console.ReadLine()!;
                        Console.Write("Enter SMS text: ");
                        string text = Console.ReadLine()!;
                        phone.SendSms(number, text);
                        break;

                    case ConsoleKey.R:
                        phone.ReadAllSms();
                        break;

                    case ConsoleKey.M:
                        done = true;
                        break;
                }
            }
        }
        
        static void CallForwarding(SerialAudioPhone phone)
        {
            bool done = false;
            while (!done)
            {
                Console.Clear();
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
                        Console.Write("Enter forward-to number: ");
                        var forwardNumber = Console.ReadLine();
                        phone.SetCallForwarding(
                            CallForwardReason.Unconditional,
                            enable: true,
                            forwardNumber: forwardNumber
                        );
                        break;

                    case ConsoleKey.D2:
                        phone.SetCallForwarding(CallForwardReason.Unconditional, enable: false);
                        break;

                    case ConsoleKey.D3:
                        Console.Write("Enter forward-to number: ");
                        var forwardNumberBusy = Console.ReadLine();
                        phone.SetCallForwarding(
                            CallForwardReason.Busy,
                            enable: true,
                            forwardNumber: forwardNumberBusy
                        );
                        break;

                    case ConsoleKey.D4:
                        phone.SetCallForwarding(CallForwardReason.Busy, enable: false);
                        break;

                    case ConsoleKey.M:
                        done = true;
                        break;
                }
            }
        }
    }
}
