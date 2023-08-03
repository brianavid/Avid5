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

    }
}
