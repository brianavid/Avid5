using System.Web;
using Microsoft.AspNetCore.Mvc;
using System.Xml.Linq;

namespace Avid5.Net.Controllers
{
    public class VideoController : Controller
    {
        // GET: /Video/Watch
        public ActionResult Watch()
        {
            return View();
        }

        // GET: /Video/WatchPane
        public ActionResult WatchPane()
        {
            return PartialView();
        }

        // GET: /Video/All
        public ActionResult All()
        {
            return View();
        }

        // GET: /Video/Recordings
        public ActionResult Recordings()
        {
            return View();
        }

        // GET: /Video/Recording
        public ActionResult Recording(
            string id)
        {
            ViewBag.Id = id;
            return View();
        }

        // GET: /Video/Videos
        public ActionResult Videos()
        {
            return View();
        }

        // GET: /Video/DVDs
        public ActionResult DVDs()
        {
            return View();
        }

        // GET: /Video/RecordingsPane
        public ActionResult RecordingsPane(
            string series)
        {
            if (series != null)
            {
                ViewBag.GroupSeries = series;
            }
            return PartialView();
        }

        // GET: /Video/VideosPane
        public ActionResult VideosPane()
        {
            return PartialView();
        }

        // GET: /Video/DVDsPane
        public ActionResult DVDsPane()
        {
            return PartialView();
        }

        // GET: /Video/PlayRecording
        public ContentResult PlayRecording(
            string id)
        {
            VideoTV.IsDvdMode = false;

            if (VideoTV.AllRecordings.ContainsKey(id))
            {
                var recording = VideoTV.AllRecordings[id];
                VideoTV.Title = recording.Title;
                JRMC.GoFullScreen();
                SendMCWS("Playback/PlayByFilename?Location=-1&Filenames=" + HttpUtility.UrlEncode(recording.Filename).Replace("+", "%20"));

            }
            return this.Content("");
        }

        // GET: /Video/DeleteRecording
        public ContentResult DeleteRecording(
            string id)
        {
            //  Can't delete a recording that's still playing
            VideoTV.Stop();

            if (VideoTV.AllRecordings.ContainsKey(id))
            {
                var recording = VideoTV.AllRecordings[id];
                VideoTV.DeleteRecording(recording);
            }
            return this.Content("");
        }

        // GET: /Video/PlayDvdDirectory
        public ContentResult PlayDvdDirectory(
            string path,
            string title)
        {
            VideoTV.IsDvdMode = true;
            VideoTV.Title = title;

			JRMC.GoFullScreen();
            SendMCWS("Playback/PlayByFilename?Location=-1&Filenames=" + HttpUtility.UrlEncode(Path.Combine(path,  "VIDEO_TS", "VIDEO_TS.IFO")).Replace("+", "%20"));
            return this.Content("");
        }

        // GET: /Video/PlayVideoFile
        public ContentResult PlayVideoFile(
            string path,
            string title)
        {
            VideoTV.IsDvdMode = false;
            VideoTV.Title = title;

			JRMC.GoFullScreen();
            SendMCWS("Playback/PlayByFilename?Location=-1&Filenames=" + HttpUtility.UrlEncode(path).Replace("+","%20"));
            return this.Content("");
        }

        // GET: /Video/GetPlayingInfo
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

        // GET: /Video/SendMCWS
        public ContentResult SendMCWS(
            string url)
        {
            XDocument doc = JRMC.GetXml(JRMC.Url + url);
            return this.Content(doc == null ? "" : doc.ToString(), @"text/xml");
        }

    }
}
