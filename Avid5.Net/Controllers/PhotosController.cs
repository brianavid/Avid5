using Microsoft.AspNetCore.Mvc;
using System.Net;

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
                HttpWebRequest request =
                   (HttpWebRequest)HttpWebRequest.Create(requestUri);
                request.Method = WebRequestMethods.Http.Get;
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    byte[] bytes = new byte[response.ContentLength];
                    response.GetResponseStream().Read(bytes);
                    return base.File(bytes, response.ContentType);
                }
            }
            catch (Exception ex)
            {
            }

            return this.Content("");
        }
    }
}
