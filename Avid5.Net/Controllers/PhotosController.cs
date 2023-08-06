using Microsoft.AspNetCore.Mvc;

namespace Avid5.Net.Controllers
{
    public class PhotosController : Controller
    {

        // GET: /Photos/Display
        public ActionResult Display()
        {
            return View();
        }

        // GET: /Photos/Browse
        public ActionResult Browse()
        {
            return View();
        }

        // GET: /Photos/Images
        public ActionResult Images()
        {
            return View();
        }

        // GET: /Photos/ImagesPane
        public ActionResult ImagesPane()
        {
            return PartialView();
        }

        // GET: /Photos/All
        public ActionResult All()
        {
            return View();
        }


        // GET: /Photos/GetThumbnail
        public ActionResult GetThumbnail(
            string id)
        {
            try
            {
                var requestUri = JRMC.Url + "File/GetImage?ThumbnailSize=small&Width=80&Height=80&Pad=1&FillTransparency=FFFFFF&File=" + id;
				var httpClient = new HttpClient();

				//make the sync GET request
				using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
				{
					var response = httpClient.Send(request);
					response.EnsureSuccessStatusCode();
                    byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
					return base.File(bytes, response.Content.Headers.ContentType.MediaType);
                }
            }
            catch
            {
            }

            return this.Content("");
        }
    }
}
