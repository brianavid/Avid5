using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Avid5.Net.Controllers
{
    public class TvController : Controller
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        // GET: /Tv/Watch
        public ActionResult Watch()
        {
            return View();
        }

        // GET: /Tv/WatchOrChannels
        public ActionResult WatchOrChannels()
        {
            if (VideoTV.CurrentlyWatching == null)
            {
                return (View("Channels"));
            }
            return View("Watch");
        }

        // GET: /Tv/ControlPane
        public ActionResult ControlPane()
        {
            return PartialView();
        }

        // GET: /Tv/Channels
        public ActionResult Channels()
        {
            return View();
        }

        // GET: /Tv/ChannelsPane
        public ActionResult ChannelsPane()
        {
            return PartialView();
        }

        // GET: /Tv/Radio
        public ActionResult Radio()
        {
            return View();
        }

        // GET: /Tv/RadioPane
        public ActionResult RadioPane()
        {
            return PartialView();
        }

        // GET: /Tv/NowAndNext
        public ActionResult NowAndNext(
            string channelKey)
        {
            ViewBag.channelKey = channelKey;
            return PartialView();
        }

        // GET: /Tv/ChangeChannel
        public ContentResult ChangeChannel(
            string channelKey)
        {
            var channel = VideoTV.AllChannels[channelKey];
            VideoTV.SelectChannel(channel);
            return this.Content("");
        }

        // GET: /Tv/GetChannelLogo
        public ActionResult GetChannelLogo(
            string channelKey)
        {
            var channel = VideoTV.AllChannels[channelKey];
            var bytes = channel.LogoImageBytes;
            if (bytes == null || bytes.Length == 0) 
            {
                return this.Content("");
            }
            return base.File(bytes, channel.LogoMediaType);
        }

        // GET: /Tv/Action
        public ContentResult Action(
            string command)
        {
            VideoTV.SendCommand(command);
            return this.Content("");
        }

        // GET: /Tv/Buttons
        public ActionResult Buttons()
        {
            return View();
        }

        // GET: /Tv/All
        public ActionResult All()
        {
            return View();
        }

        // GET: /Tv/RecordNow
        public ContentResult RecordNow()
        {
            var currentlyWatching = VideoTV.CurrentlyWatching;
            if (currentlyWatching != null)
            {
                var now = VideoTV.GetNowAndNext(currentlyWatching.Channel).FirstOrDefault();
                if (now != null)
                {
                    VideoTV.AddTimer(now);
                }
            }
            return this.Content("");
        }

    }
}
