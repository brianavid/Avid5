using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using NLog;
using System.Net.Http;

public static class Roku
{
    static Logger logger = LogManager.GetCurrentClassLogger();

	static HttpClient httpClient = new HttpClient();

	/// <summary>
	/// The HTTP Url of the Roku service
	/// </summary>
	public static string Url
    {
        get { return "http://" + Config.RokuAddress + ":8060/"; }
    }

    /// <summary>
    /// Send an HTTP GET request to the Roku service, expecting an XML response, which is returned
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    static XDocument GetXml(
        string url)
    {
        try
        {
            Uri requestUri = new Uri(Url + url);

			//make the sync GET request
			using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
			{
				var response = httpClient.Send(request);
				response.EnsureSuccessStatusCode();
				return XDocument.Load(new StreamReader(response.Content.ReadAsStream()));
			}
		}
		catch
        {
            return null;
        }
    }

    /// <summary>
    /// Send an HTTP POST request to the Roku service with no body, expecting no response
    /// </summary>
    /// <param name="url"></param>
    /// <param name="paramValue"></param>
    /// <returns></returns>
    static void PostRequest(
        string url,
        string paramValue)
    {
        try
        {
            Uri requestUri = new Uri(Url + url + (paramValue ?? ""));
			using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri))
			{
				var response = httpClient.Send(request);
			}
        }
        catch
        {
        }
    }

    public static Dictionary<string, string> Apps
    {
        get
        {
            var xApps = GetXml("query/apps");
            if (xApps == null)
            {
                return null;
            }

            return xApps.Element("apps").Elements("app").ToDictionary(x => x.Attribute("id").Value, x => x.Value);
        }
    }

    static Dictionary<string, string> PlayerInfo
    {
        get
        {
            var info = GetXml("query/media-player");
            if (info == null)
            {
                return null;
            }

            var infoDict = info.Root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);
            infoDict.Add("state", info.Root.Attribute("state").Value);
            return infoDict;
            //  Question - Do we add the attribute values too?
        }
    }

    public static bool IsActivelyPlaying()
    {
        var info = GetXml("query/media-player");
        if (info == null)
        {
            return true;
        }
        return info.Root.Attribute("state").Value == "play";
    }


    public static XElement GetPlayingInfo()
    {
        var info = PlayerInfo;
        var stateDisplay = "";
        var positionDisplay = "";
        var durationDisplay = "";

        if (info != null)
        {
            if (info.ContainsKey("state"))
            {
                stateDisplay = info["state"];
            }
            if (info.ContainsKey("position"))
            {
                var ms = int.Parse(new string(info["position"].TakeWhile(c => char.IsDigit(c)).ToArray()));
                positionDisplay = new TimeSpan(0, 0, 0, 0, ms).ToString(@"mm\:ss");
            }
            if (info.ContainsKey("duration"))
            {
                var ms = int.Parse(info["duration"].TakeWhile(c => char.IsDigit(c)).ToArray());
                durationDisplay = new TimeSpan(0, 0, 0, 0, ms).ToString(@"mm\:ss");
            }
        }
        return new XElement("Info", new List<XAttribute> {
            new XAttribute("state", stateDisplay ),
            new XAttribute("position", String.IsNullOrEmpty(durationDisplay) ? positionDisplay : positionDisplay + " / " + durationDisplay )
        });
    }

    public static void RunApp(
        string appId,
        Dictionary<string,string> args = null)
    {
        if (args != null && args.Count > 0)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var arg in args)
            {
                sb.AppendFormat("&{0}={1}", arg.Key, HttpUtility.UrlEncode(arg.Value));
            }

            appId += "?" + sb.ToString().Substring(1);

        }

        PostRequest("launch/", appId);
    }

    public static void KeyDown(
        string key)
    {
        PostRequest("keydown/", key);
    }

    public static void KeyUp(
        string key)
    {
        PostRequest("keyup/", key);
    }

    public static void KeyPress(
        string key)
    {
        PostRequest("keypress/", key);
    }

    public static void SendText(
        string text)
    {
        foreach (var c in text)
        {
            PostRequest("keypress/", "Lit_" + HttpUtility.UrlEncode(c.ToString()));
        }
        PostRequest("keypress/", "Enter");
    }

    public static string AppImageUrl(
        string id)
    {
        return Url + "query/icon/" + id;
    }

}