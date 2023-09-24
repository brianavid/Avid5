using System.Diagnostics;
using System.Net;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NLog.LayoutRenderers.Wrappers;

namespace Avid5.Net.Controllers
{
    public class MusicController : Controller
    {
		static Logger logger = LogManager.GetLogger("MusicController");

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

        static DateTime timeStartedPlaying = DateTime.MinValue;

        // GET: /Music/GetPlayingInfo
        public ContentResult GetPlayingInfo()
        {
            StringWriter writer = new StringWriter();
            var info = JRMC.GetPlaybackInfo();
            if (info != null)
            {
                if (info.HasElements)
                {
                    if (timeStartedPlaying != DateTime.MinValue)
                    {
                        var state = info.Elements().Where(e => e.Attribute("Name").Value == "State").FirstOrDefault()?.Value ?? "";
                        var secondsSinceStart = Math.Round((DateTime.Now - timeStartedPlaying).TotalSeconds);
                        if (state == "2")
                        {
                            timeStartedPlaying = DateTime.MinValue;
                            if (secondsSinceStart >= 10)
                            {
                                logger.Info($"Waited {secondsSinceStart}s to start");
                                var activeLogFilePath = "/home/brian/.jriver/Media Center 31/Log.txt";
                                var copyLogFileName = "JR-" + DateTime.Now.ToString("HHmm") + ".txt";
                                var copyLogFilePath = Config.FilePath(copyLogFileName);
                                if (System.IO.File.Exists(activeLogFilePath))
                                {
                                    logger.Info($"Preserve JR log file to {copyLogFilePath}");
                                    Task.Run(() =>
                                    {
                                        //  Allow time for the log to flush buffering
                                        Thread.Sleep(30000);
                                        System.IO.File.Copy(activeLogFilePath, copyLogFilePath, true);
                                        logger.Info($"Preserved JR log file to {copyLogFilePath}");
                                    });
                                }
                            }
                        }
                        else if (secondsSinceStart > 2)
                        { 
                            foreach (var status in info.Elements().Where(e => e.Attribute("Name").Value == "Status"))
                            {
                                status.Value = $"Waiting {secondsSinceStart}s";
                            }
                        }
                    }

                    //  Re-write the URL for the album image from a MCWS URL to the Avid MVC URL (which will fetch the same content).
                    //  This avoids problems in the client browser that will reject the MCWS URL for a "cross-site scripting" security risk
                    foreach (var xImageUrl in info.Elements().Where(e => e.Attribute("Name").Value == "ImageURL"))
                    {
                        xImageUrl.Value = xImageUrl.Value.Replace("MCWS/v1/File/GetImage?File=", "/Music/GetAlbumImage?id=");
                    }
                }
				info.Save(writer);
            }

            if (timeStartedPlaying != DateTime.MinValue)
            {
                if ((DateTime.Now - timeStartedPlaying).TotalSeconds > 2)
                {
                    try
                    {
                        var diagnosticScript = Config.FilePath("OnSlowResponse.sh");
                        if (System.IO.File.Exists(diagnosticScript))
                        {
                            using (Process myProcess = new Process())
                            {
                                myProcess.StartInfo.FileName = diagnosticScript;
                                myProcess.StartInfo.UseShellExecute = false;
                                myProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                myProcess.StartInfo.CreateNoWindow = true;
                                myProcess.StartInfo.RedirectStandardOutput = true;

                                myProcess.Start();

                                string result = myProcess.StandardOutput.ReadToEnd();
                                myProcess.WaitForExit();
                                logger.Info($"OnSlowResponse: '\n{result}'");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.ToString());
                    }
                }
            }
            return this.Content(writer.ToString(), @"text/xml", writer.Encoding);
        }

        // GET: /Music/SendMCWS
        public ContentResult SendMCWS(
            string url)
        {
            logger.Info($"MCWS {url}");
            if (url.Contains("PlayByKey"))
            {
                timeStartedPlaying = DateTime.Now;
            }
            XDocument doc = JRMC.GetXml(JRMC.Url + url);
            return this.Content(doc == null ? "" : doc.ToString(), @"text/xml");
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
				//make the sync GET request
				using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
				{
					var response = JRMC.httpClient.Send(request);
					response.EnsureSuccessStatusCode();
					byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
					return base.File(bytes, response.Content.Headers.ContentType.MediaType);
				}
			}
			catch
            { 
            }

            return this.Redirect("/Content/JRMC.png");
        }

    }
}
