using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace Avid5.Net.Controllers
{
    public class HomeController : Controller
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

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
        // GET: /Home/Error

        public ActionResult Error()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            var error = feature?.Error;

            if (error != null)
            {
                logger.Error(error);
            }

            return View();
        }

        // GET: /Home/Problems

        public ActionResult Problems()
        {
            return View();
        }

        // GET: /Home/GoAway

        public ActionResult GoAway()
        {
            return View();
        }

    }
}
