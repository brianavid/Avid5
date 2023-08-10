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
            if (VideoTV.CurrentlyWatching == null)
            {
                return (View("Channels"));
            }
            return View();
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

			//  The LogoUrl can eithewr be a locally cached file path or an http URL
			if (System.IO.File.Exists(channel.LogoUrl))
            {
                return base.File(System.IO.File.ReadAllBytes(channel.LogoUrl), channel.LogoUrl.EndsWith(".png") ? "image/png" : "image/jpeg");
            }
            else if (channel.LogoUrl.StartsWith("http"))
            {
				var httpClient = new HttpClient();

				//make the sync GET request
				using (var request = new HttpRequestMessage(HttpMethod.Get, channel.LogoUrl))
				{
					var response = httpClient.Send(request);
					response.EnsureSuccessStatusCode();
					byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
					return base.File(bytes, response.Content.Headers.ContentType.MediaType);
				}

			}
			return Redirect("/Avid5-192.png");
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
            var now = VideoTV.GetNowAndNext(VideoTV.CurrentlyWatching.Channel).FirstOrDefault();
            if (now != null)
            {
                VideoTV.AddTimer(now);
            }
            return this.Content("");
        }

    }
}
