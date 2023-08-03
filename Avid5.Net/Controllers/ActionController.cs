using System.Diagnostics;
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
            string args,
            string title,
            string detach)
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
                if (!string.IsNullOrEmpty(detach))
                {
                    Running.LaunchNewProgram(name, args);
                }
                else
                {
                    Running.LaunchProgram(name, args);
                }
            }

            return Content("OK");
        }

        // GET: /Action/AllOff
        public ActionResult AllOff(
            string keep)
        {
            try
            {
                Security.ClearSavedProfile();

                Running.ExitAllPrograms(!string.IsNullOrEmpty(keep));
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
            Screen.SetScreenDisplayMode(0);
            return Content("");
        }

        // GET: /Action/ScreenOn
        public ActionResult ScreenOn()
        {
            Screen.SetScreenDisplayMode(1);
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
                case "Music":
                case "Spotify":
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
            Screen.EnsureScreenOn();
            Running.StartStream("Chromecast");
            Receiver.SelectChromecastInput();
            Receiver.SelectTVOutput();
            return Content("");
        }

        // GET: /Action/GoChromecastAudio
        public ActionResult GoChromecastAudio()
        {
            Running.StartStream("Chromecast");
            Receiver.SelectChromecastInput();
            Receiver.SelectRoomsOutput();
            Screen.SetScreenDisplayMode(0);
            return Content("");
        }

        // GET: /Action/GoPC
        public ActionResult GoPC()
        {
            Screen.EnsureScreenOn();
            Running.StartStream("PC");
            Receiver.SelectComputerInput();
            Receiver.SelectTVOutput();
            return Content("");
        }

        // GET: /Action/GoRoku
        public ActionResult GoRoku()
        {
            Roku.KeyPress("Home");
            Screen.EnsureScreenOn();
            Running.StartStream("Roku");
            Receiver.SelectRokuInput();
            Receiver.SelectTVOutput();
            return Content("");
        }

        // GET: /Action/GoSmart
        public ActionResult GoSmart()
        {
            Screen.EnsureScreenOn();
            Running.StartStream("SmartTv");
            Screen.WaitForScreenOn();
            Receiver.SelectTvInput();
            Receiver.SelectTVOutput();
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
            if (Running.RunningProgram != "Video" && Running.RunningProgram != "Spotify")
            {
                JRMC.LoadAndIndexAllAlbums(new string[] { "1", "2" }, true);
            }
            Spotify.LoadAndIndexAllSavedTracks();
            return Content("");
        }

        // GET: /Action/RecycleApp
        public ActionResult RecycleApp()
        {
            Spotify.ExitPlayer();

            Config.StopApplication();
            return Content("");
        }

        // GET: /Action/RebootSystems
        public ActionResult RebootSystems()
        {
            Receiver.Reboot();

            Process.Start("shutdown", "/r /t 0");
            return Content("");
        }

    }
}
