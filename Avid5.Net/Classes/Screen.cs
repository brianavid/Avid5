﻿using NLog;
using System;
using System.Diagnostics;
using System.Web;
using System.Xml.Linq;

/// <summary>
/// Class to control the screen, using unofficially documented discrete power on/off IR Codes
/// </summary>
public static class Screen
{
    static Logger logger = LogManager.GetCurrentClassLogger();

	//	If during initialisation, the RunCECControlProcess mechanism fails,
	//	this is most likely to be the result of side-by-side operation with Avid4.
	//	Only one mechanism is allowed to access the CEC port.
	//	So (for now) fall back to the old "Tray App" API on port 89, which is still there for Avid4.
	static HttpClient trayAppClient = new HttpClient();

	public static void Initialise()
	{
		logger.Info($"Initialise");
		var result = RunCECControlProcess("pow 0", true);
		logger.Info($"CEC returns {result}");
		if (!result.StartsWith("power status"))
		{
			trayAppClient = new HttpClient();
			trayAppClient.BaseAddress = new Uri("http://localhost:89");
			trayAppClient.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue();
			trayAppClient.DefaultRequestHeaders.CacheControl.NoCache = true;
			trayAppClient.DefaultRequestHeaders.CacheControl.MaxAge = new TimeSpan(0);
		}
		else
		{
			isOn = result == "power status: on";
		}
	}

	static string RunCECControlProcess(string command, bool wait = false)
	{
		try
		{
			Uri requestUri = new Uri("http://localhost:5099/Cec/" + (wait ? "Get" : "Do") + "?parm=" + HttpUtility.UrlEncode(command));
			logger.Info($"{requestUri}");
			var httpClient = new HttpClient();

			//make the sync GET request
			using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
			{
				var response = httpClient.Send(request);
				response.EnsureSuccessStatusCode();
				return new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
			}
		}
		catch (System.Exception ex)
		{
			logger.Error(ex);
			return "";
		}
	}

	/// <summary>
	/// Turn the screen on by issuing the appropriate HDMI-CEC command to device 0 (which is always the TV screen).
	/// </summary>
	static void TurnOn()
    {
		if (trayAppClient != null)
		{
			try
			{
				HttpResponseMessage resp = trayAppClient.GetAsync("api/Desktop/TvScreenOn").Result;
				resp.EnsureSuccessStatusCode();
			}
			catch (System.Exception ex)
			{
				logger.Error(ex);
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
    /// <returns></returns>
    static bool TestScreenOn()
    {
		if (trayAppClient != null)
		{
			try
			{
				HttpResponseMessage resp = trayAppClient.GetAsync("api/Desktop/TvScreenIsOn").Result;
				resp.EnsureSuccessStatusCode();

				return bool.Parse(resp.Content.ReadAsStringAsync().Result);
			}
			catch (System.Exception ex)
			{
				logger.Error(ex);
				return isOn;
			}
		}
		else 
		{
			var result = RunCECControlProcess("pow 0", true);
			logger.Info($"CEC returns {result}");
			if (result.StartsWith("power status"))
			{
				isOn = result == "power status: on";
			}
			return isOn;
		}
	}

	/// <summary>
	/// Wait for the screen to turn on before any further activity (such as starting a full-screen player
	/// application that needs to know the screen size).
	/// </summary>
	public static void WaitForScreenOn()
    {
        logger.Info("WaitForScreenOn");

        for (int i = 0; i < 30; i++)
        {
            if (TestScreenOn())
            {
                logger.Info("Screen is now on");

                break;
            }

            System.Threading.Thread.Sleep(500);
        }
    }

	/// <summary>
	/// Ensure that the screen is on - we do this by turning it on!
	/// </summary>
	public static void EnsureScreenOn()
	{
		logger.Info("EnsureScreenOn");

        JRMC.GoTheater();
		TurnOn();
	}

	public static void EnsureScreenOff()
	{
		logger.Info("EnsureScreenOff");

		TurnOff();
		JRMC.CloseScreen();
	}

	/// <summary>
	/// Turn the screen off by issuing the appropriate HDMI-CEC command to device 0 (which is always the TV screen).
	/// </summary>
	static void TurnOff()
    {
        // if we've just turned the screen on, wait for the transition
        if (isOn)
        {
            WaitForScreenOn();
        }

		if (trayAppClient != null)
		{
			try
			{
				HttpResponseMessage resp = trayAppClient.GetAsync("api/Desktop/TvScreenOff").Result;
				resp.EnsureSuccessStatusCode();
			}
			catch (System.Exception ex)
			{
				logger.Error(ex);
			}
		}
		else
		{
			RunCECControlProcess("standby 0");
		}
		isOn = false;
    }

    /// <summary>
    /// Is the screen currently believed to be on?
    /// </summary>
    public static bool IsOn
    {
        get { return isOn; }
    }
    static bool isOn = false;
}