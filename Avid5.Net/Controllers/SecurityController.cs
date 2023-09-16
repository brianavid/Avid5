using System.Globalization;
using Microsoft.AspNetCore.Mvc;


namespace Avid5.Net.Controllers
{
    public class SecurityController : Controller
    {
        static DateTime NoTickBefore = DateTime.MinValue;

        // GET: Security/GetProfiles
        public ActionResult GetProfiles()
        {
            return View();
        }

        // GET: Security/LoadProfile?id=NNN
        public ActionResult LoadProfile(
            string id)
        {
            Security.LoadProfile(Int32.Parse(id));
            return Content("OK");
        }

        // GET: Security/Away
        public ActionResult Away()
        {
            return View();
        }

        // GET: Security/AwayToday
        public ActionResult AwayToday()
        {
            Security.LoadProfile(1);
            return View("Away");
        }

        // GET: Security/AwayTrip
        public ActionResult AwayTrip()
        {
            Security.LoadProfile(2);
            return View("Away");
        }

        // GET: Security/GetSchedule
        public ActionResult GetSchedule()
        {
            return View();
        }

        // GET: Security/GetZones
        public ActionResult GetZones()
        {
            return View();
        }

        // GET: Security/TurnZoneOn
        public ActionResult TurnZoneOn(
            string id)
        {
            Security.TurnZoneOn(id);
            return Content("OK");
        }

        // GET: Security/TurnZoneOff
        public ActionResult TurnZoneOff(
            string id)
        {
            Security.TurnZoneOff(id);
            return Content("OK");
        }

        public ActionResult IsDefault()
        {
            return Content(Security.IsDefaultProfile() ? "Yes" : "No");
        }
    }
}