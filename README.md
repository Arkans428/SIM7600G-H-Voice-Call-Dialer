# SIM7600G-H Modem Voice Call Dialer

This project contains C# classes designed to manage audio streaming and serial communication with the SIMCOM SIM7600G-H modem. This is more Proof-of-concept than a robust commercial program.

## Overview

**`SerialAudioPhone`**: Manages serial communication with the SIMCOM SIM7600G-H modem, handling AT commands and audio streaming for phone calls. It now also includes the ability to send DTMF tones during a call.

## Key Features

- **SIMCOM SIM7600G-H Modem Integration**: Send AT commands over the AT Command Port and transmit/receive audio data over the serial audio port. The program can send DTMF tones during an active call, including the standard tones (0-9, *, #) and extended tones (A-D).
- **DTMF Generation**: Sends all DTMF Tones from 0-9 and A-D.

## Hardware Requirements

This project is designed for use with the **SIMCOM SIM7600G-H** modem. It assumes you have two serial interfaces configured for the modem:
- One serial interface for AT command communication.
- One serial interface for audio data transmission.

If you encounter errors stating it can't find the serial ports, you may need to change your USB PID configuration of the modem. Open the AT Command or modem port if available and enter:
```bash
AT+CUSBPIDSWITCH=9005,1,1
```
The modem will reboot automatically after receiving the command, and you should see both ports now.

## Software Requirements

- **NAudio Library**: This project uses the NAudio library for handling audio device management and streaming in C#. You can install the necessary packages via NuGet:
  ```bash
  dotnet add package NAudio.WinMM 
  dotnet add package NAudio.Wasapi
  ```
  NOTE: The NAudio.WinMM package will automatically install NAudio.Core as a prerequisite. You do not need to install it separately.

- **System.IO.Ports Library**: You'll also need to import the System.IO.Ports library to avoid an invalid reference error to the .NET Framework version of this library with the same name.
  ```bash
  dotnet add package System.IO.Ports
  ```
- **System.Management Library**: You'll also need to import the System.Management library so the program can search for the COM ports using the device ID of the modem.
  ```bash
  dotnet add package System.Management
  ```

## Project Structure

### `SerialAudioPhone`

The `SerialAudioPhone` class is designed specifically for working with the SIMCOM SIM7600G-H modem. It handles sending AT commands, initiating calls, and streaming audio through the modem’s serial audio port. The call continues until the user presses the "Esc" key or the call ends due to a "NO CARRIER" response from the modem.

#### Key Features:
- **NAudio**: Uses NAudio for audio input/output.
- **Improved DTMF Tone Support**: The program now properly detects and sends all DTMF tones (including `*` and `#`) regardless of whether they are entered using Shift + 8/3 or the number pad.
- **Automatic Port Detection**: The program automatically finds the correct serial ports for AT commands and audio.

#### Usage Example:
```csharp
var phone = new SerialAudioPhone();
phone.StartCall("15005555555");
```

### Important Notes:
- The critical AT command `AT+CPCMREG=1` is sent right after dialing the phone number to ensure that audio transmission is enabled on the modem.
- You can end the call manually by pressing the "Esc" key.
- DTMF tones, including A, B, C, and D, can be sent during an active call by pressing the corresponding keys on the keyboard.

## How to Run

1. Clone this repository:
   ```bash
   git clone https://github.com/Arkans428/SIM7600G-H-Serial-Audio.git
   ```
2. Install dependencies:
   ```bash
   dotnet add package NAudio.WinMM
   dotnet add package NAudio.Wasapi
   dotnet add package System.IO.Ports
   dotnet add package System.Management
   ```
3. Build and run the project (You'll need to be in the directory with the csproj file):
   ```bash
   dotnet build 'SIM7600G-H Modem Tool.csproj'
   dotnet run
   ```

4. Follow the console prompts to test the different functionalities.

## Compatibility

- The project is compatible with the SIMCOM SIM7600G-H modem and runs on Windows systems with .NET Core 8.0 (or later versions) installed.

## Contributing

If you have any suggestions or improvements, feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License.
