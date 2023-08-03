using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace Avid5.Net.Controllers
{
    public class MusicController : Controller
    {
        // GET: /Music/All
        public ActionResult All()
        {
            ViewBag.Mode = "Library";

            return View();
        }

        // GET: /Music/Playing
        public ActionResult Playing()
        {
            return View();
        }

        // GET: /Music/Queue
        public ActionResult Queue()
        {
            return View();
        }

        // GET: /Music/QueuePane
        public ActionResult QueuePane()
        {
            return PartialView();
        }

        // GET: /Music/Browser
        public ActionResult Browser(
            string mode,
            string id,
            string query)
        {
            ViewBag.Mode = mode;
            if (id != null)
            {
                ViewBag.Id = id;
            }
            if (query != null)
            {
                ViewBag.Query = query;
            }

            return View();
        }

        // GET: /Music/BrowserPane
        public ActionResult BrowserPane(
            string mode,
            string id,
            string query,
            string date,
            string station,
            string append)
        {
            ViewBag.Mode = mode;
            if (id != null)
            {
                ViewBag.Id = id;
            }
            if (query != null)
            {
                ViewBag.Query = query;
            }
            if (append != null)
            {
                ViewBag.Append = append;
            }
            if (date != null)
            {
                ViewBag.Date = date;
            }
            if (station != null)
            {
                ViewBag.Station = station;
            }

            return PartialView();
        }

        // GET: /Music/GetPlayingInfo
        public ContentResult GetPlayingInfo()
        {
            StringWriter writer = new StringWriter();
            var info = JRMC.GetPlaybackInfo();
            if (info != null)
            {
                info.Save(writer);
            }
            return this.Content(writer.ToString(), @"text/xml", writer.Encoding);
        }

        // GET: /Music/SendMCWS
        public ContentResult SendMCWS(
            string url)
        {
            XDocument doc = JRMC.GetXml(JRMC.Url + url);
            return this.Content(doc.ToString(), @"text/xml");
        }

        // GET: /Music/RemoveQueuedTrack
        public ContentResult RemoveQueuedTrack(
            string id)
        {
            JRMC.RemoveQueuedTrack(id);
            return this.Content("");
        }

    }
}
