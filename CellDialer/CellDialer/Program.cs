using System;
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
                    Console.WriteLine("1. Serial Audio Phone Call");
                    Console.WriteLine("2. Exit");
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
        /*
        // Method to send a text message
        static void SendTextMessage(SerialAudioPhone phone)
        {
            try
            {
                // Prompt the user to enter the recipient's phone number
                Console.Write("Enter recipient's phone number: ");
                string? phoneNumber = Console.ReadLine();

                // Validate that the phone number is not empty or whitespace
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return; // Exit the method if the input is invalid
                }

                // Prompt the user to enter the text message
                Console.Write("Enter your message: ");
                string? message = Console.ReadLine();

                // Validate that the message is not empty or whitespace
                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("Invalid message.");
                    return; // Exit the method if the input is invalid
                }

                // Send the text message
                phone.SendTextMessage(phoneNumber, message);
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when sending the text message
                Console.WriteLine("Error sending text message: " + ex.Message);
            }
        }

        // Method to read text messages
        static void ReadTextMessages(SerialAudioPhone phone)
        {
            try
            {
                // Read and display the stored text messages
                phone.ReadTextMessages();
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when reading the text messages
                Console.WriteLine("Error reading text messages: " + ex.Message);
            }
        }

        // Method to delete a specific SMS
        static void DeleteSpecificSms(SerialAudioPhone phone)
        {
            try
            {
                // Prompt the user to enter the index of the SMS to delete
                Console.Write("Enter the index of the SMS to delete: ");
                if (int.TryParse(Console.ReadLine(), out int messageIndex))
                {
                    phone.DeleteSms(messageIndex);
                }
                else
                {
                    Console.WriteLine("Invalid index.");
                }
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when deleting the SMS
                Console.WriteLine($"Error deleting SMS: {ex.Message}");
            }
        }

        // Method to delete all SMS messages
        static void DeleteAllSmsMessages(SerialAudioPhone phone)
        {
            try
            {
                // Confirm deletion with the user
                Console.Write("Are you sure you want to delete all SMS messages? (y/n): ");
                string? confirmation = Console.ReadLine();
                if (confirmation?.ToLower() == "y")
                {
                    phone.DeleteAllSms();
                }
                else
                {
                    Console.WriteLine("Deletion canceled.");
                }
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when deleting all SMS messages
                Console.WriteLine($"Error deleting all SMS messages: {ex.Message}");
            }
        }*/
    }
}
