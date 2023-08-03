using Microsoft.AspNetCore.Mvc;

namespace Avid5.Net.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            ViewBag.IsHome = true;
            return View("Wide");
        }

        //
        // GET: /Home/Home

        public ActionResult Home()
        {
            ViewBag.IsHome = true;
            return View();
        }

        //
        // GET: /Home/Wide

        public ActionResult Wide()
        {
            ViewBag.IsHome = true;
            return View();
        }

        //
        // GET: /Home/GoAway

        public ActionResult GoAway()
        {
            return View();
        }

    }
}
