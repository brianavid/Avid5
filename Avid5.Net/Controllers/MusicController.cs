using System.Net;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using NLog.LayoutRenderers.Wrappers;

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
                if (info.HasElements)
                {
                    foreach (var xImageUrl in info.Elements().Where(e => e.Attribute("Name").Value == "ImageURL"))
                    {
                        xImageUrl.Value = xImageUrl.Value.Replace("MCWS/v1/File/GetImage?File=", "/Music/GetAlbumImage?id=");
                    }
                }
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

        // GET: /Music/GetAlbumImage
        public ActionResult GetAlbumImage(
            string id)
        {
            try
            {
                var requestUri = JRMC.Url + "File/GetImage?File=" + id;
                HttpWebRequest request =
                   (HttpWebRequest)HttpWebRequest.Create(requestUri);
                request.Method = WebRequestMethods.Http.Get;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                   byte[] bytes= new byte[response.ContentLength];
                   response.GetResponseStream().Read(bytes);
                   return base.File(bytes, response.ContentType);
                }
            }
            catch
            { 
            }

            return this.Content("");
        }

    }
}
