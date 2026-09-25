#define USE_HOME_ASST

using NLog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Web;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Class to control the screen, using unofficially documented discrete power on/off IR Codes
/// </summary>
public static class Screen
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    static bool isOn = false;
#if USE_CEC
	static string ClientPath;
    static string TVAddress;
    static string TVMacAddress;
#endif
#if USE_HOME_ASST
    static HttpClient httpClient = null;
#endif


    public static void Initialise()
	{
		logger.Info($"Initialise");
#if USE_CEC
        ClientPath = Config.CECClientPath;
        TVAddress = Config.TVAddress;
        TVMacAddress = string.Concat(Config.TVMacAddress.Where(char.IsLetterOrDigit));
        logger.Info($"ClientPath = {ClientPath}");
        logger.Info($"TVAddress = {TVAddress}");
        logger.Info($"TVMacAddress = {TVMacAddress}");
#endif
#if USE_HOME_ASST
        httpClient = new HttpClient();
#endif
        TestScreenOn();
    }

#if USE_HOME_ASST
    /// <summary>
    /// Get a JSON response from an HTTP GET from Home Assistant
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    static string  GetHaState(
        string state)
    {
        Uri requestUri = new Uri("http://" + Config.HaIpAddress + ":8123/api/" + state);

        //make the sync POST request
        using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
        {
            request.Headers.Add("Authorization", "Bearer " + Config.HaToken);
            var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();
            return new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
        }
    }

    /// <summary>
    /// Post an action with a JSON body to an HTTP POST to Home Assistant
    /// </summary>
    /// <param name="action"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    static void PostHaAction(
        string action,
		string body)
    {
        Uri requestUri = new Uri("http://" + Config.HaIpAddress + ":8123/api/" + action);

        //make the sync POST request
        using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
        {
            request.Headers.Add("Authorization", "Bearer " + Config.HaToken);
            request.Content = new StringContent(body, System.Text.Encoding.UTF8);
            var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();
        }
    }
#endif

#if USE_CEC
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
#endif
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
#if USE_CEC
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
#endif

#if USE_HOME_ASST
        PostHaAction("services/media_player/turn_on", "{ \"entity_id\": \"" + Config.HaTvEntityId + "\"}");
#endif
        isOn = true;
    }

    /// <summary>
    /// Is the screen really on (irrespective of our state)?
    /// </summary>
    /// <returns></returns>
    static bool TestScreenOn()
    {
		logger.Info("TestScreenOn");
#if USE_CEC
		{
			var result = RunCECControlProcess("pow 0", true);
			logger.Info($"CEC returns {result}");
			if (result.Contains("power status"))
			{
				isOn = result.Contains("power status: on");
			}
		}
#endif

#if USE_HOME_ASST
        var result = GetHaState("states/" + Config.HaTvEntityId);
        JObject obj = JObject.Parse(result);
        string state = (string)obj["state"];
        logger.Info($"HA returns state =  {state}");
        isOn = (state ?? "") == "on";
#endif
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

#if USE_CEC
       RunCECControlProcess("standby 0");
#endif

#if USE_HOME_ASST
        PostHaAction("services/media_player/turn_off", "{ \"entity_id\": \"" + Config.HaTvEntityId + "\"}");
#endif
        isOn = false;
    }

    /// <summary>
    /// Wait for the screen to turn on before any further activity (such as starting a full-screen player
    /// application that needs to know the screen size).
    /// </summary>
    public static void WaitForScreenOn()
    {
		if (Receiver.SelectedInput == "Computer")
		{
			JRMC.GoTheaterScreen();
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
			JRMC.GoDisplayScreen();
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