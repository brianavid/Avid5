using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Avid5.Net.Controllers
{
    public class ActionController : Controller
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        // GET: /Action/GetRunning
        public ContentResult GetRunning()
        {
            return this.Content(Running.RunningProgram);
        }

        // GET: /Action/VolumeUp
        public ActionResult VolumeUp()
        {
            Receiver.IncreaseVolume();
            return Content(Receiver.VolumeDisplay);
        }

        // GET: /Action/VolumeDown
        public ActionResult VolumeDown()
        {
            Receiver.DecreaseVolume();
            return Content(Receiver.VolumeDisplay);
        }

        // GET: /Action/VolumeMute
        public ActionResult VolumeMute()
        {
            Receiver.ToggleMute();
            return Content(Receiver.VolumeDisplay);
        }

        // GET: /Action/VolumeGet
        public ActionResult VolumeGet()
        {
            return Content(Receiver.VolumeDisplay);
        }

        // GET: /Action/Launch
        public ActionResult Launch(
            string name,
            string args)
        {
            Security.ClearSavedProfile();

            if (String.IsNullOrEmpty(Running.RunningProgram))
            {
            }

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(args))
                {
                    args = HttpUtility.UrlDecode(args);
                }
                Running.LaunchProgram(name, args);
            }

            return Content("OK");
        }

        // GET: /Action/AllOff
        public ActionResult AllOff()
        {
            try
            {
                Security.ClearSavedProfile();

                Running.ExitAllPrograms();
	            return Content(Receiver.VolumeDisplay);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Error in AllOff: {0}", ex.Message);
                return Content("Error");
            }
        }

        // GET: /Action/ScreenOff
        public ActionResult ScreenOff()
        {
            Screen.EnsureScreenOff();
			return Content("");
        }

        // GET: /Action/ScreenOn
        public ActionResult ScreenOn()
        {
            Screen.EnsureScreenOn();
            return Content("");
        }

        // GET: /Action/StartStream
        public ActionResult StartStream()
        {
            Screen.EnsureScreenOn();
            var streamProgram = "";
            switch (Running.RunningProgram)
            {
                case "Chromecast":
                case "Roku":
                case "SmartTv":
                    streamProgram = Running.RunningProgram;
                    break;
            }
            Running.StartStream(streamProgram);
            Receiver.TurnOn();
            return Content("");
        }

        // GET: /Action/GoChromecast
        public ActionResult GoChromecast()
        {
            Receiver.SelectChromecastInput();
            Receiver.SelectTVOutput();
            Screen.EnsureScreenOn();
            Running.StartStream("Chromecast");
			Screen.WaitForScreenOn();

			return Content("");
        }

        // GET: /Action/GoChromecastAudio
        public ActionResult GoChromecastAudio()
        {
            Receiver.SelectChromecastInput();
            Receiver.SelectRoomsOutput();
            Screen.EnsureScreenOff();
            Running.StartStream("Chromecast");
			return Content("");
        }

        // GET: /Action/GoPC
        public ActionResult GoPC()
        {
            Screen.EnsureScreenOn();
            Receiver.SelectTVOutput();
            Running.StartStream("PC");
            Receiver.SelectComputerInput();
			Screen.WaitForScreenOn();
            JRMC.CloseScreen();
            return Content("");
        }

        // GET: /Action/GoRoku
        public ActionResult GoRoku()
        {
            Roku.KeyPress("Home");
            Receiver.SelectRokuInput();
            Receiver.SelectTVOutput();
            Screen.EnsureScreenOn();
            Running.StartStream("Roku");
			Screen.WaitForScreenOn();
			return Content("");
        }

        // GET: /Action/GoSmart
        public ActionResult GoSmart()
        {
            Receiver.SelectTvInput();
            Receiver.SelectTVOutput();
            Screen.EnsureScreenOn();
            Running.StartStream("SmartTv");
            Screen.WaitForScreenOn();
            return Content("");
        }

        // GET: /Action/SoundTV
        public ActionResult SoundTV(
            string mode)
        {
            Receiver.SelectTVOutput(mode);
            return Content("");
        }

        // GET: /Action/SoundRooms
        public ActionResult SoundRooms()
        {
            Receiver.SelectRoomsOutput();
            return Content("");
        }

        // GET: /Action/RebuildMediaDb
        public ActionResult RebuildMediaDb()
        {
            JRMC.LoadAndIndexAllAlbums(new string[] { "1", "2" }, true);
            Spotify.LoadAndIndexAllSavedTracks();
            return Content("");
        }

		// GET: /Action/RecycleApp
		public ActionResult RecycleApp()
		{
			Config.StopApplication();
			return Content("");
		}

        // GET: /Action/Exit
        public ActionResult Exit()
		{
			Spotify.ExitPlayer();

			Config.StopApplication(false);
			return Content("");
		}

        // GET: /Action/RebootSystems
        public ActionResult RebootSystems()
        {
            Receiver.Reboot();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("reboot");
            }
            else
            {
                Process.Start("shutdown", "/r /t 0");
            }
            return Content("");
        }

        // GET: /Action/SelectView
        public ActionResult SelectView(string view)
        {
            if (!String.IsNullOrEmpty(view))
            {
                switch (view)
                {
                    case "Standard":
                        JRMC.GoShowUI();
                        break;
                    case "Display":
                        JRMC.GoFullScreen();
                        break;
                    case "Theater":
                        JRMC.GoTheater();
                        break;
                    case "Closed":
                        JRMC.CloseScreen();
                        break;
                }
            }
            return Content("");
        }



    }
}
