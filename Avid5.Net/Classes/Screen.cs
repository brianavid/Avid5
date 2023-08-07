using NLog;

/// <summary>
/// Class to control the screen, using unofficially documented discrete power on/off IR Codes
/// </summary>
public static class Screen
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Turn the screen on by issuing the appropriate HDMI-CEC command to device 0 (which is always the TV screen).
    /// </summary>
    static void TurnOn()
    {
        //DesktopClient.TvScreenOn();

        isOn = true;
    }

    /// <summary>
    /// Is the screen really on (irrespective of our state)?
    /// </summary>
    /// <returns></returns>
    static bool TestScreenOn()
    {
        //  If we are watching an external source, it does not matter if the screen is on
        //if (Receiver.SelectedInput != "Computer")
        {
            return isOn;
        }

        //return DesktopClient.TvScreenIsOn();
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

        //DesktopClient.TvScreenOff();
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