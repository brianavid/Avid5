using NLog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;
using System.Xml.Linq;

/// <summary>
/// Class to control the screen, using unofficially documented discrete power on/off IR Codes
/// </summary>
public static class Screen
{
    static Logger logger = LogManager.GetCurrentClassLogger();
	static string ClientPath;
    static string TVAddress;
    static string TVMacAddress;
    static bool isOn = false;

    public static void Initialise()
	{
		logger.Info($"Initialise");
        ClientPath = Config.CECClientPath;
        TVAddress = Config.TVAddress;
        TVMacAddress = string.Concat(Config.TVMacAddress.Where(char.IsLetterOrDigit));
        logger.Info($"ClientPath = {ClientPath}");
        logger.Info($"TVAddress = {TVAddress}");
        logger.Info($"TVMacAddress = {TVMacAddress}");
        TestScreenOn();
    }


    static string RunCECControlProcess(string command, bool wait = false)
	{
		if (!String.IsNullOrEmpty(ClientPath))
		{
			using (Process myProcess = new Process())
			{
				logger.Info($"RunCECControlProcess: '{command}'");
				myProcess.StartInfo.FileName = ClientPath;
				myProcess.StartInfo.UseShellExecute = false;
				myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				myProcess.StartInfo.CreateNoWindow = true;
				myProcess.StartInfo.Arguments = "-s -d 1";
				myProcess.StartInfo.UseShellExecute = false;
				myProcess.StartInfo.RedirectStandardInput = true;
				myProcess.StartInfo.RedirectStandardOutput = true;

				myProcess.Start();

				StreamWriter myStreamWriter = myProcess.StandardInput;
				myStreamWriter.Write(command);
				myStreamWriter.Close();

				string result = "";
				if (wait)
				{
					result = myProcess.StandardOutput.ReadToEnd().Trim();
					myProcess.WaitForExit();
					logger.Info($"  Result: {myProcess.ExitCode} '{result}'");
					result = myProcess.ExitCode.ToString() + ": " + result;
				}

				return result;
			}

		}

		return "";
	}

    /// <summary>
    /// Turn the screen on
    /// </summary>
    /// <remarks>
    /// If possible, broadcast a Wake-on-LAN packet aimed at the TV's Mac address to see of it it running
    /// Otherwise fall back issuing the appropriate HDMI-CEC command to device 0 (which is always the TV screen).
    /// </remarks>
    static void TurnOn()
    {
		logger.Info("TurnOn");
		if ( !String.IsNullOrEmpty(TVMacAddress) && TVMacAddress.Length == 12)
		{
            //	Construct WOL packet
            int counter = 0;
            byte[] bytes = new byte[102];

            for (int x = 0; x < 6; x++)
                bytes[counter++] = 0xFF;

			for (int macPackets = 0; macPackets < 16; macPackets++)
			{
				for (int macBytes = 0; macBytes < 12; macBytes += 2)
				{
					bytes[counter++] = byte.Parse(TVMacAddress.Substring(macBytes, 2), NumberStyles.HexNumber);
				}
			}

            //	Broadcast WOL packet on port 9 (Echo/ping)
            logger.Info($"Send WoL for {TVMacAddress}");
            using (UdpClient client = new UdpClient() { EnableBroadcast = true })
			{
				client.Connect(IPAddress.Broadcast, 9);
				client.Send(bytes, bytes.Length);
			}
		}
		else
        {
            RunCECControlProcess("on 0");
        }

        isOn = true;
    }

    /// <summary>
    /// Is the screen really on (irrespective of our state)?
    /// </summary>
	/// <remarks>
	/// If possible, ping the TV's IP address to see of it it running
	/// Otherwise fall back to CEC
	/// </remarks>
    /// <returns></returns>
    static bool TestScreenOn()
    {
		logger.Info("TestScreenOn");
		if (!String.IsNullOrEmpty(TVAddress))
		{
			using (Ping ping = new Ping())
			{

				PingReply result = ping.Send(TVAddress, 500);
                logger.Info($"Ping {TVAddress} returns {result.Status}");
                isOn = result.Status == IPStatus.Success;
			}
		}
		else
		{
			var result = RunCECControlProcess("pow 0", true);
			logger.Info($"CEC returns {result}");
			if (result.Contains("power status"))
			{
				isOn = result.Contains("power status: on");
			}
		}
		return isOn;
	}

    /// <summary>
    /// Turn the screen off by issuing the appropriate HDMI-CEC command to device 0 (which is always the TV screen).
    /// </summary>
	/// <remarks>
	/// Unfortunately I have not found a way to do this over the network and must rely on CEC
	/// </remarks>
    static void TurnOff()
    {
        logger.Info("TurnOff");
        // if we've just turned the screen on, wait for the transition
        if (isOn)
        {
            WaitForScreenOn();
        }

        RunCECControlProcess("standby 0");
        isOn = false;
    }

    /// <summary>
    /// Wait for the screen to turn on before any further activity (such as starting a full-screen player
    /// application that needs to know the screen size).
    /// </summary>
    public static void WaitForScreenOn()
    {
#if false
		logger.Info("WaitForScreenOn");

		for (int i = 0; i < 15; i++)
		{
			if (TestScreenOn())
			{
				logger.Info("Screen is now on");
				break;
			}
			TurnOn();

			System.Threading.Thread.Sleep(500);
		}

		if (!isOn)
		{
			logger.Info("Given up waiting");
		}
#endif    

		if (Receiver.SelectedInput == "Computer")
		{
			JRMC.GoTheater();
		}
	}

		/// <summary>
		/// Ensure that the screen is on - we do this by turning it on!
		/// </summary>
		public static void EnsureScreenOn()
	{
		logger.Info("EnsureScreenOn");

		if (Receiver.SelectedInput == "Computer")
		{
			JRMC.GoFullScreen();
		}
        TurnOn();
	}

	public static void EnsureScreenOff()
	{
		logger.Info("EnsureScreenOff");

		TurnOff();
	}

    /// <summary>
    /// Is the screen currently believed to be on?
    /// </summary>
    public static bool IsOn
    {
        get { return isOn; }
    }
}