using System.Runtime.Versioning;

namespace ModemDialer
{
    class Program
    {
        [SupportedOSPlatform("windows")]
        static void Main(string[] args)
        {
            SerialAudioPhone phone;
            
            try
            {
                // Attempt to initialize SerialAudioPhone
                phone = new SerialAudioPhone();
            }
            catch (Exception ex)
            {
                // If initialization fails, display the error and wait for a keypress before exiting
                Console.WriteLine("Failed to initialize SerialAudioPhone:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press any key to close the program...");
                Console.ReadKey();
                return; // Exit the program since initialization failed
            }
            
            try
            {
                while (true)
                {
                    // Display menu options to the user
                    Console.WriteLine("======SIM7600G-H Modem Tool======");
                    Console.WriteLine("Choose an option:");                  
                    Console.WriteLine("1. Place Audio Phone Call");
                    Console.WriteLine("2. Answer incoming Call");
                    Console.WriteLine("3. Exit");
                    Console.Write("Enter your choice: ");

                    // Read user input
                    string? choice = Console.ReadLine();

                    // Process user choice
                    switch (choice)
                    {
                        case "1":
                            SerialAudioPhoneCall(phone);
                            break;
                        case "2":
                            phone.AnswerCall();
                            return;
                        case "3":
                            // Properly dispose of resources before exiting
                            phone.Dispose();
                            Console.WriteLine("Exiting...");
                            return;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any unexpected errors during the program execution
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        // Method to test making a phone call using the serial audio phone functionality
        static void SerialAudioPhoneCall(SerialAudioPhone phone)
        {
            try
            {
                // Prompt the user to enter the phone number to dial
                Console.Write("Enter phone number to dial: ");
                string? phoneNumber = Console.ReadLine(); // Read user input

                // Validate that the phone number is not empty or whitespace
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return; // Exit the method if the input is invalid
                }

                // Start the call with the provided phone number
                phone.StartCall(phoneNumber);
            }
            catch (Exception ex)
            {
                // Handle errors that may occur during the phone call
                Console.WriteLine("Error during phone call: " + ex.Message);
            }
        }
    }
}
