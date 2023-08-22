using Microsoft.AspNetCore.Mvc;
using NLog;
using System.Text;
using System.Xml.Linq;

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

        // GET: /Tv/GetLiveTVPlayingPositionInfo
        public ContentResult GetLiveTVPlayingPositionInfo()
        {
            var info = VideoTV.GetAllPlaybackInfo();
            if (info != null)
            {
                var currentlyWatching = VideoTV.CurrentlyWatching;
                var nowAndNext = currentlyWatching != null ? VideoTV.GetNowAndNext(currentlyWatching.Channel) : null;
                var nowTitle = nowAndNext != null && nowAndNext.Count() > 0 ? nowAndNext.First().Title : "";
                var nextStart = nowAndNext != null && nowAndNext.Count() > 1 ? nowAndNext.Skip(1).First().StartTime.ToLocalTime().ToShortTimeString() : "";
                var nextTitle = nowAndNext != null && nowAndNext.Count() > 1 ? nowAndNext.Skip(1).First().Title : "";
                var startTime = DateTime.Now - TimeSpan.FromMilliseconds(int.Parse(info["DurationMS"]));
                var positionTime = startTime + TimeSpan.FromMilliseconds(int.Parse(info["PositionMS"]));
                var doc = new XDocument(new XElement("Position",
                    new XAttribute("state", info["Status"]),
                    new XAttribute("startDisplay", startTime.ToString("HH:mm:ss")),
                    new XAttribute("endDisplay", DateTime.Now.ToString("HH:mm:ss")),
                    new XAttribute("positionDisplay", positionTime.ToString("HH:mm:ss")),
                    new XAttribute("positionMS", info["PositionMS"]),
                    new XAttribute("durationMS", info["DurationMS"]),
                    new XAttribute("channel", currentlyWatching != null ? currentlyWatching.ChannelName : ""),
                    new XAttribute("now", nowTitle),
                    new XAttribute("next", nextStart + ": " + nextTitle),
                    new XAttribute("isRecording", (currentlyWatching != null && currentlyWatching.IsScheduled).ToString())));
                return this.Content(doc.ToString(), @"text/xml", Encoding.UTF8);
            }

            return this.Content("");
        }

    }
}
