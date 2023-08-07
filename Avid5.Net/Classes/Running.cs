using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading;
using NLog;

/// <summary>
/// Class to keep track of what player application is currently running
/// </summary>
public static class Running
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Avid name for the currently running player application
    /// </summary>
    static string runningProgram = "";

    /// <summary>
    /// Avid name for the currently running player application
    /// </summary>
    public static String RunningProgram { get { return runningProgram; } }

    /// <summary>
    /// When was there last activity with the running program?
    /// </summary>
    static DateTime lastActive = DateTime.UtcNow;

    static bool spotifyRunning = false;

    /// <summary>
    /// Initialize
    /// </summary>
    public static void Initialize()
    {
        if (Receiver.SelectedInput == "Roku")
        {
            runningProgram = "Roku";
        }

        if (Receiver.SelectedInput == "TV")
        {
            runningProgram = "SmartTv";
        }

        if (Receiver.SelectedInput == "Chromecast")
        {
            runningProgram = "Chromecast";
        }

        //  Start a background thread to poll for an inactive screen-off player and so turn it off after
        //  a short while
        var activityChecker = new Thread(ActivityChecker);
        activityChecker.Start();
    }

    /// <summary>
    /// Return a CSS class name which can be used to style (colour) the UI top bar based on the running player application
    /// </summary>
    public static string RunningProgramTopBarClass
    {
        get
        {
            switch (runningProgram)
            {
                default:
                    return "topBarNone";
                case "Music":
                    return "topBarMusic";
                case "TV":
                case "Radio":
                    return "topBarTv";
                case "Roku":
                    return "topBarRoku";
                case "SmartTv":
                    return "topBarSmartTv";
                case "Chromecast":
                    return "topBarChromecast";
                case "Spotify":
                    return "topBarSpotify";
                case "Video":
                    return "topBarVideo";
                case "Photo":
                    return "topBarPhotos";
            }
        }
    }

    /// <summary>
    /// Launch a specified player application, closing any others as appropriate, and configuring
    /// the screen and receiver to suit the preferred outputs fr that player
    /// </summary>
    /// <param name="name"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static bool LaunchProgram(
        string name,
        string args)
    {
        logger.Info("LaunchProgram {0} -> {1} {2}", runningProgram, name, args ?? "");

        lastActive = DateTime.UtcNow;

        if (name != "Roku")
        {
            StopRoku();
        }
        Receiver.SelectComputerInput();

        if (runningProgram == name && String.IsNullOrEmpty(args))
        {
            logger.Info("LaunchProgram OK {0}", runningProgram);
            return true;
        }

        if (spotifyRunning)
        {
            Spotify.Stop();
			spotifyRunning = false;

		}
        StopAndHideJRMC();

        runningProgram = name;

        switch (name)
        {
            default:
                return false;

            case "TV":
                if (args != null && args == "Radio")
                {
                    Screen.EnsureScreenOff();
					Receiver.SelectRoomsOutput();
                }
                else
                {
                    Screen.EnsureScreenOn();
                    Receiver.SelectTVOutput();
                    Screen.WaitForScreenOn();
                }
                VideoTV.WatchLive();
                logger.Info("LaunchProgram OK {0}", runningProgram);
                return true;

            case "Video":
                Screen.EnsureScreenOn();
                Receiver.SelectTVOutput();

                if (args == null)
                {
                    return true;
                }

                Screen.WaitForScreenOn();
                logger.Info("LaunchProgram OK {0}", runningProgram);
				return true;

			case "Photos":
				Screen.EnsureScreenOn();
				Receiver.SelectRoomsOutput();
				Screen.WaitForScreenOn();
				logger.Info("LaunchProgram OK {0}", runningProgram);
				return true;

			case "Spotify":
				Screen.EnsureScreenOff();
				Receiver.SelectComputerInput(); //  ??? Or select Spotify input??
                Receiver.SelectRoomsOutput();
                spotifyRunning = true;
                logger.Info("LaunchProgram OK {0}", runningProgram);
                return true;
        }
    }

    /// <summary>
    /// Exit all running programmes
    /// </summary>
    /// <returns></returns>
    public static bool ExitAllPrograms()
    {
        logger.Info("ExitAllPrograms");

        lastActive = DateTime.UtcNow;

        Receiver.TurnOff();
        Screen.EnsureScreenOff();

        if (spotifyRunning)
        {
            Spotify.ExitPlayer();
            spotifyRunning = false;
        }

		VideoTV.Stop();
		StopAndHideJRMC();

		if (runningProgram == "Roku")
        {
            Roku.KeyPress("Home");
            Receiver.SelectComputerInput();
        }

		NothingRunning();

        return true;
    }

    /// <summary>
    /// Command the JRMC player to stop and and hide itself
    /// </summary>
    private static void StopAndHideJRMC()
    {
		VideoTV.Stop();
		JRMC.StopAndHide();
    }

    /// <summary>
    ///
    /// </summary>
    private static void StopRoku()
    {
        if (runningProgram == "Roku")
        {
            Roku.KeyPress("Home");
        }
    }

    /// <summary>
    /// Note that we are starting streaming, and so stop all media PC player applications
    /// </summary>
    /// <returns></returns>
    public static bool StartStream(
        string streamSource)
    {
        logger.Info("StartStream: " + streamSource);

        if (runningProgram != streamSource)
        {
			StopAndHideJRMC();
            StopRoku();
            NothingRunning();
        }

        runningProgram = streamSource;
        return true;
    }

    /// <summary>
    /// Assert (and ensure) that nothing is running
    /// </summary>
    static void NothingRunning()
    {
		StopAndHideJRMC();
		if (spotifyRunning)
        {
            Spotify.Stop();
        }
        runningProgram = "";
        logger.Info("NothingRunning");
    }


    /// <summary>
    /// Is the currently running player showing signs of activity?
    /// </summary>
    /// <returns></returns>
    static Boolean IsActive()
    {
        //  If a music player is stopped or paused, it may have been forgotten
        switch (runningProgram)
        {
            default:
                //  If the screen is off and the volume is muted, it may have been forgotten
                //  So treat as inactive
                return Screen.IsOn || !Receiver.VolumeMuted;
            case "Music":
            case "Video":
            case "TV":
            case "Radio":
                return JRMC.IsActivelyPlaying();
            case "Spotify":
                return Spotify.GetPlaying() > 0;
        }

    }


    /// <summary>
    /// On a background thread, poll for a silent player and turn everything off after a short while.
    /// </summary>
    static void ActivityChecker()
    {
        for (;;)
        {
            Thread.Sleep(60 * 1000);   //  Every minute, check for activity
            if (IsActive())
            {
                lastActive = DateTime.UtcNow;
            }

            //  If the receiver is on and there has been no activity for 15 minutes,
            //  turn everything off
            if (Receiver.IsOn() && lastActive.AddMinutes(15) < DateTime.UtcNow)
            {
                logger.Info("No activity from {0} since {1} - Exiting", runningProgram, lastActive.ToLocalTime().ToShortTimeString());
                ExitAllPrograms();
            }
        }
    }
}