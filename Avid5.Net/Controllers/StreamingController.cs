using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Avid5.Net.Controllers
{
    public class StreamingController : Controller
    {
        void HideUnwantedUI()
        {
            switch (Running.RunningProgram)
            {
                case "Roku":
                case "SmartTV":
                case "Chromecast":
                    ViewBag.Title = Running.RunningProgram;
                    break;
                default:
                    ViewBag.Title = "";
                    break;
            }
            if (Running.RunningProgram != "")
            {
                ViewBag.HideStreamGuidanceClass = "startHidden";
            }
            if (Running.RunningProgram != "Roku")
            {
                ViewBag.HideRokuClass = "startHidden";
            }
            if (Running.RunningProgram != "SmartTv")
            {
                ViewBag.HideSmartClass = "startHidden";
            }
        }

        // GET: /Streaming/Controls
        public ActionResult Controls()
        {
            HideUnwantedUI();
            return View();
        }

        // GET: /Streaming/Browser
        public ActionResult Browser()
        {
            HideUnwantedUI();
            return View();
        }

        // GET: /Streaming/All
        public ActionResult All()
        {
            HideUnwantedUI();
            return View();
        }

        // GET: /Streaming/RokuLaunch
        public ContentResult RokuLaunch(
            string id)
        {
            Roku.RunApp(id);
            return this.Content("");
        }

        // GET: /Streaming/RokuGetPlayingInfo
        public ContentResult RokuGetPlayingInfo()
        {
            var info = Roku.GetPlayingInfo();
            return this.Content(info.ToString(), @"text/xml", Encoding.UTF8);
        }

        // GET: /Streaming/RokuGetAppImage
        public ActionResult RokuGetAppImage(
            string id)
        {
            try
            {
                var requestUri = Roku.AppImageUrl(id);
                //make the sync GET request
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                {
                    var response = Roku.httpClient.Send(request);
                    response.EnsureSuccessStatusCode();
                    byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
                    return base.File(bytes, response.Content.Headers.ContentType.MediaType);
                }
            }
            catch
            {
            }

            return this.Redirect("/Content/Roku.png");
        }

        public ContentResult KeyDown(
            string id)
        {
            Roku.KeyDown(id);
            return this.Content("");
        }

        public ContentResult KeyUp(
            string id)
        {
            Roku.KeyUp(id);
            return this.Content("");
        }

        public ContentResult KeyPress(
            string id)
        {
            Roku.KeyPress(id);
            return this.Content("");
        }

        public ContentResult SendText(
            string text)
        {
            Roku.SendText(text);
            return this.Content("");
        }

        // GET: /Streaming/SendTvKey
        public ActionResult SendTvKey(
            string keyName)
        {
            return Content("");
        }

        static bool isPlaying = true;

        // GET: /Streaming/SmartTvPlayPause
        public ActionResult SmartTvPlayPause()
        {
            isPlaying = !isPlaying;
            return Content("");
        }


    }
}
