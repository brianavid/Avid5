using NLog;
using System.Globalization;
using System.Xml.Linq;
using System.Text;
using System.Linq;

/// <summary>
/// The VideoTV class encapsulates access to the J River Media Center player when used for
/// cataloging and playing, watching and recording TV and Video
/// </summary>
public class VideoTV
{
    static Logger logger = LogManager.GetCurrentClassLogger();
    static Dictionary<string, String> MakeDict(XElement x)
    {
        var items = x.Elements("Item");
        if (items.First().HasAttributes)
        {
            return items.Select(f => (f.Attribute("Name").Value, f.Value)).ToDictionary(i => i.Item1, i => i.Item2);
        }
        return items.First().Elements("Field").Select(f => (f.Attribute("Name").Value, f.Value)).ToDictionary(i => i.Item1, i => i.Item2);
    }

    static DateTime ParsePythonDateTimeString(string s)
    {
        var mins = Math.Round(double.Parse(s) * 24 * 60);
        return new DateTime(1899, 12, 30).AddMinutes(mins).ToUniversalTime();
    }

    public class Channel
    {
        public const String UnknownName = "Unknown";
        public int Index { get; private set; }
        public string Key { get; private set; }
        public string FullName { get; private set; }
        public int Number { get; private set; }
        public string Name { get; private set; } 
        public bool IsRadio {  get { return Number >= 700; } }
        public bool IsFavourite { get { return Info.ContainsKey("Keywords") && Info["Keywords"].Contains("Favorite"); } }
        public Dictionary<string,String> Info { get; internal set; }
        public string LogoUrl
        {
            get
            {
                string infoPath;
                return Info.TryGetValue("Image File", out infoPath) ? infoPath : "";
            }
        }

        public Channel(string key, Dictionary<string, string> info, int index)
        {
            Key = key;
            Index = index;
            Info = info;
            FullName = info["Name"];
            Name = FullName.Split(new[] { ' ' }, 2)[1];
            Number = int.Parse(FullName.Split(' ')[0]);
            Index = index;
        }
    }

    public static Dictionary<string, Channel> AllChannels { get; internal set; }

    /// <summary>
    /// A recorded TV or Radio programme stored in a file
    /// </summary>
    public class Recording
    {
        public String Id { get; private set; }
        public String Title { get; private set; }
        public String Description { get; private set; }
        public Channel Channel { get; private set; }
        public DateTime StartTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public DateTime StopTime { get { return StartTime + Duration; } }
        public String Filename { get; private set; }
        public bool InError { get; internal set; }
        public bool IsOld { get; internal set; }
        public string SidecarPath {  get; private set; }
        public Dictionary<string, String> Info { get; internal set; }
        public String ChannelName { get { return Channel == null ? (ChannelDisplayName ?? Channel.UnknownName) : Channel.Name; } }
        public String ChannelDisplayName { get; internal set; }
        public bool IsVeryOld { get {
                return DateTime.Now.Date - StartTime.Date > TimeSpan.FromDays(200) && StartTime.Year != DateTime.Now.Year;
            } }

        public Recording(
            XElement xRecording,
            bool isOld,
            string sidecarPath)
        {
            IsOld = isOld;
            SidecarPath = sidecarPath;

            if (isOld)
            {
                try
                {
                    Id = xRecording.Attribute("id").Value;
                    Title = xRecording.Element("title").Value;
                    Description = xRecording.Element("info").Value;
                    Channel = AllChannels.Values.FirstOrDefault(c => c.Name.Equals(xRecording.Element("channel").Value, StringComparison.CurrentCultureIgnoreCase));
                    ChannelDisplayName = xRecording.Element("channel").Value;
                    StartTime = DateTime.ParseExact(
                        xRecording.Attribute("start").Value,
                        new[] { "yyyyMMddHHmmss" },
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                    Duration = TimeSpan.ParseExact(xRecording.Attribute("duration").Value,
                        "hhmmss",
                        CultureInfo.InvariantCulture);
                    Filename = xRecording.Element("file").Value;
                    InError = false;
                    Info = new Dictionary<string, string>();
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, "Error parsing Recording XML");
                    InError = true;
                }
            }
            else
            {
                try
                {
                    var dict = MakeDict(xRecording);

                    Id = dict["Program ID"];
                    Filename = dict["Filename"];
                    Title = System.IO.Path.GetFileNameWithoutExtension(Filename);
                    Description = dict["Description"];
                    Channel = AllChannels.Values.FirstOrDefault(c => c.FullName.Equals(dict["Artist"], StringComparison.CurrentCultureIgnoreCase));
                    ChannelDisplayName = dict["Artist"];
                    StartTime = ParsePythonDateTimeString(dict["Date Recorded"]);
                    Duration = new TimeSpan(0, 0, int.Parse(dict["Duration"]) - 900);
                    InError = false;
                    Info = dict;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex, "Error parsing Recording XML");
                    InError = true;
                }
            }
        }

        public bool IsRecording
        {
            get
            {
                return false;
            }
        }
    }

    static Dictionary<string, Recording> OldRecordings = new Dictionary<string, Recording>();

    /// <summary>
    /// A TV or Radio programme in the EPG
    /// </summary>
    public class Programme
    {
        public String Id { get; private set; }
        private string Name { get; set; }
        public String Title { get 
             {
                string series;
                return Info.TryGetValue("Series", out series) && series != Name ? series + ": " + Name : Name;
            } 
        }
        public String Description { get { return Info["Description"]; } }
        public Channel Channel { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime StopTime { get; private set; }
        public TimeSpan Duration { get { return StopTime - StartTime; } }
        public bool InError { get; internal set; }
        public bool IsScheduled { get { return VideoTV.IsScheduled(Id); } }
        public String ChannelName { get { return Channel == null ? Channel.UnknownName : Channel.Name; } }
        private Dictionary<string, string> _info;
        public Dictionary<string, string> Info
        {
            get
            {
                if (_info == null)
                {
                    var info = JRMC.GetXml(JRMC.Url + "File/GetInfo?File=" + Id);
                    if (info != null)
                    {
                        _info = MakeDict(info.Root);
                    }
                }

                return _info;
            }
        }

        public Programme(
            XElement xProg)
        {
            try
            {
                Id = xProg.Attribute("Key").Value;
                Name = xProg.Element("Name").Value;
                Channel = AllChannels[xProg.Element("TV_Channel").Value];
                StartTime = ParsePythonDateTimeString(xProg.Element("Date_Recorded").Value);
                StopTime = StartTime.AddSeconds(int.Parse(xProg.Element("Duration").Value));
                InError = false;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Error parsing Programme XML");
                InError = true;
            }
        }
    }

    /// <summary>
    /// A scheduled timer to record a TV or Radio programme
    /// </summary>
    public class Timer
    {
        public String Id { get; private set; }
        public String Name { get; private set; }
        public String Title
        {
            get
            {
                string series = Name;
                Info.TryGetValue("Series", out series);
                return series != Name ? series + ": " + Name : Name;
            }
        }
        public Channel Channel { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime StopTime { get; private set; }
        public TimeSpan PrePad { get; private set; }
        public TimeSpan PostPad { get; private set; }
        public TimeSpan Duration { get { return StopTime - StartTime; } }
        public String EventId { get; private set; }
        public bool InSeries { get { return Series.Find(Name, Channel, StartTime) != null; } }
        public bool IsRecording { get; private set; }
        public bool InError { get; internal set; }
        public String ChannelName { get { return Channel == null ? Channel.UnknownName : Channel.Name; } }
        private Dictionary<string, string> _info;
        public Dictionary<string, string> Info
        {
            get
            {
                if (_info == null)
                {
                    var info = JRMC.GetXml(JRMC.Url + "File/GetInfo?File=" + EventId);
                    if (info != null)
                    {
                        _info = MakeDict(info.Root);
                    }
                }

                return _info;
            }
        }

        public Timer(
            XElement xTimer)
        {
            try
            {
                Id = xTimer.Element("RecordingID").Value;
                Name = xTimer.Element("ProgName").Value;
                PrePad = TimeSpan.FromMinutes(5);
                PostPad = TimeSpan.FromMinutes(10);
                Channel = AllChannels[xTimer.Element("ChannelKey").Value];
                StartTime = DateTime.ParseExact(
                    xTimer.Element("StartTime").Value,
                    new[] { "dd/MM/yyyy HH:mm" },
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal) + PrePad;
                StopTime = StartTime.AddMinutes(int.Parse(xTimer.Element("Duration").Value)) - PrePad - PostPad;
                EventId = xTimer.Element("ProgKey") == null ? "" : xTimer.Element("ProgKey").Value;
                IsRecording = xTimer.Element("IsRecordingNow").Value != "0";
                InError = false;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Error parsing Timer XML");
                InError = true;
            }
        }
    }

    /// <summary>
    /// A Series definition
    /// </summary>
    public class Series
    {
        public String Id { get; private set; }
        public String Name { get; private set; }
        private Boolean WeakNaming { get; set; }
        public Channel Channel { get; private set; }
        public DateTime StartTime { get; private set; }
        public DateTime StartTimeLow { get; private set; }
        public DateTime StartTimeHigh { get; private set; }
        bool isDeleted = false;
        public String ChannelName { get { return Channel == null ? Channel.UnknownName : Channel.Name; } }

        static string XmlFilename = Config.FilePath("Series-v5.xml");
        const string Format = "dd-MM-yyyy HH:mm";
        const int PreWindowMinutes = 90;
        const int PostWindowMinutes = 180;

        Series(
            XElement xSeries)
        {
            try
            {
                Id = xSeries.Attribute("Id").Value;
                WeakNaming = xSeries.Attribute("Weak") != null;
                Name = xSeries.Attribute("Name").Value;
                Channel = NamedChannel(xSeries.Attribute("Channel").Value);
                StartTime = DateTime.ParseExact(xSeries.Attribute("StartTime").Value,
                    Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                StartTimeLow = DateTime.ParseExact(xSeries.Attribute("StartTimeLow").Value,
                    Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
                StartTimeHigh = DateTime.ParseExact(xSeries.Attribute("StartTimeHigh").Value,
                    Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Error parsing Series XML");
            }
        }

        Series(
            string id,
            string name,
            Channel channel,
            DateTime startTime)
        {
            Id = id;
            Name = name;
            WeakNaming = false;
            Channel = channel;
            StartTime = startTime;
            var prePad = Math.Min(startTime.Hour * 60 + startTime.Minute, PreWindowMinutes);
            var postPad = Math.Min(23 * 60 + 59 - startTime.Hour * 60 + startTime.Minute, PostWindowMinutes);
            StartTimeLow = startTime.AddMinutes(-prePad);
            StartTimeHigh = startTime.AddMinutes(postPad);
        }

        static List<Series> seriesDefinitions = null;

        public static IEnumerable<Series> All { get { Load(); return seriesDefinitions.Where(s => !s.isDeleted); } }

        /// <summary>
        /// Load the ...
        /// </summary>
        static void Load()
        {
            if (seriesDefinitions == null)
            {
                if (File.Exists(Series.XmlFilename))
                {
                    XElement seriesDoc = XDocument.Load(Series.XmlFilename, LoadOptions.None).Root;
                    seriesDefinitions = seriesDoc.Elements("Series")
                        .Select(s => new Series(s))
                        .ToList();
                }
                else
                {
                    seriesDefinitions = new List<Series>();
                }
            }
        }

        static void Save()
        {
            XElement root = new XElement("SeriesDefinitions",
                All.Select(s => s.ToXml));
            root.Save(Series.XmlFilename);
        }

        XElement ToXml
        {
            get
            {
                return new XElement("Series",
                    new XAttribute("Id", Id),
                    new XAttribute("Name", Name),
                    WeakNaming ? new XAttribute("Weak", "Yes") : null,
                    new XAttribute("Channel", Channel.Name),
                    new XAttribute("StartTime", StartTime.ToString(Format)),
                    new XAttribute("StartTimeLow", StartTimeLow.ToString(Format)),
                    new XAttribute("StartTimeHigh", StartTimeHigh.ToString(Format)));
            }
        }

        public static void Add(
            string id,
            string name,
            Channel channel,
            DateTime startTime)
        {
            Load();
            if (Find(name, channel, startTime) == null)
            {
                seriesDefinitions.Add(new Series(id, name, channel, startTime));
                Save();
            }
        }

        string StripNew(string title)
        {
            return title.Length > 3 && title.StartsWith("New") && !Char.IsLetter(title[3]) ?
                String.Concat(title.Substring(3).SkipWhile(c => !Char.IsLetter(c))) :
                title;
        }

        public bool MatchesProgrammeTitle(
            string title)
        {
            return WeakNaming ?
                title.ToLower().Contains(Name.ToLower()) :
                StripNew(title) == StripNew(Name);
        }

        public static Series Find(
            string name,
            Channel channel,
            DateTime startTime)
        {
            Load();
            foreach (Series series in seriesDefinitions)
            {
                if (!series.isDeleted &&
                    series.Name == name &&
                    series.Channel == channel &&
                    series.StartTimeLow.DayOfWeek == startTime.DayOfWeek &&
                    series.StartTimeLow.TimeOfDay <= startTime.TimeOfDay &&
                    (series.StartTimeLow.TimeOfDay > series.StartTimeHigh.TimeOfDay ||
                     series.StartTimeHigh.TimeOfDay >= startTime.TimeOfDay))
                {
                    return series;
                }
            }
            return null;
        }

        public static void Delete(
            string id)
        {
            foreach (Series series in All.Where(s => s.Id == id))
            {
                series.isDeleted = true;
                Save();
            }
            // Force a re-load without any with isDeleted
            seriesDefinitions = null;
        }

    }


    public static void Initialise()
    {
        AllChannels = GetAllChannels();
        LoadOldRecordingInfo();
        LoadAllEpgProgrammes();
    }

    static void LoadOldRecordingInfo()
    {
        var doc = XDocument.Load(System.IO.Path.Combine(Config.OldRecordingsPath, "Recordings.xml"));
        var recordings = doc.Element("recordings").Elements("recording")
           .Select(r => new Recording(r, true, null))
           .Where(r => !r.InError);

        foreach (var r in recordings)
        {
            if (File.Exists(r.Filename))
            {
                var leaf = Path.GetFileNameWithoutExtension(r.Filename).ToLower();
                OldRecordings[leaf] = r;
            }
        }
    }

    static Dictionary<string,Channel> GetAllChannels()
    {
        var channels = new List<Channel>();
        var x = JRMC.GetXml(JRMC.Url + "Television/GetOrderedListOfTVChannels");

        if (x == null || !x.Root.HasElements)
        {
            return new Dictionary<string, Channel>();
        }

        IEnumerable<string> channelKeys = x.Root.Element("Item").Value.Split(',');
        var index = 0;

        foreach (var key in channelKeys)
        {
            var info = JRMC.GetXml(JRMC.Url + "File/GetInfo?File="+key);
            if (info != null)
            {
                var infoDict = MakeDict(info.Root);
                if (infoDict.ContainsKey("Keywords") && infoDict["Keywords"].Split(",").Contains("Hidden"))
                {
                    break;
                }
                var logoUrl = "";
                infoDict.TryGetValue("Image File", out logoUrl);
				logger.Info($"Found channel '{infoDict["Name"]}' with logo at '{logoUrl}'");

                var channel = new Channel (key, infoDict, index++);
                channels.Add(channel);
            }
        }

        return channels.ToDictionary(c => c.Key);
    }

    /// <summary>
    /// A named channel
    /// </summary>
    public static Channel NamedChannel(
        String channelName)
    {
        return AllChannels.Values.FirstOrDefault(ch => ch.Name.Equals(channelName, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// A numbered channel
    /// </summary>
    public static Channel NumberedChannel(
        int channelNumber)
    {
        return AllChannels.Values.FirstOrDefault(ch => ch.Number == channelNumber);
    }

    public static IEnumerable<Channel> AllTvChannels
    {
        get
        {
            return AllChannels.Values.Where(ch => !ch.IsRadio);
        }
    }

    /// <summary>
    /// A collection of all Radio channels
    /// </summary>
    public static IEnumerable<Channel> AllRadioChannels
    {
        get
        {
            return AllChannels.Values.Where(ch => ch.IsRadio);
        }
    }

    /// <summary>
    /// A collection of all TV channel names
    /// </summary>
    public static IEnumerable<string> AllTvChannelNames
    {
        get
        {
            return AllTvChannels.Select(c => c.Name);
        }
    }

    /// <summary>
    /// A collection of all Radio channel names
    /// </summary>
    public static IEnumerable<string> AllRadioChannelNames
    {
        get
        {
            return AllRadioChannels.Select(c => c.Name);
        }
    }

    /// <summary>
    /// The schedule of recordings
    /// </summary>
    public static Dictionary<string, Timer> Schedule
    {
        get
        {
            if (schedule == null)
            {
                LoadSchedule();
            }
            return schedule;
        }
    }
    static Dictionary<string, Timer> schedule = null;

    /// <summary>
    /// Load the schedule of recordings from the JRMC Recording Service
    /// </summary>
    public static void LoadSchedule()
    {
        var xml = JRMC.GetXml(JRMC.Url + "Television/GetRecordingScheduleXML");
        if (xml != null && xml.Root.Elements("Item").Count() > 0)
        {
            var scheduleXml = XDocument.Parse("<Item>\n" + xml.Root.Element("Item").Value + "\n</Item>");

            schedule = scheduleXml.Element("Item").Elements("Rec").Select(el => new Timer(el)).ToDictionary(t => t.Id);
        }
        else
        {
            schedule = new Dictionary<string, Timer>();
        }
    }

    /// <summary>
    /// Is the identified programme scheduled to record?
    /// </summary>
    /// <param name="programmeId"></param>
    /// <returns></returns>
    static bool IsScheduled(
        string programmeId)
    {
        return schedule != null && schedule.Values.Any(t => t.EventId == programmeId);
    }

    public static Dictionary<string, Recording> AllRecordings;

    public static void LoadRecordings()
    {
        AllRecordings = new Dictionary<string, Recording>(); ;

        var sidecarFiles = System.IO.Directory.GetFiles(Config.RecordingsPath, "*.xml");

        foreach (var sidecarPath in sidecarFiles)
        {
            var sidecar = XDocument.Load(sidecarPath);
            var r = new Recording(sidecar.Root, false, sidecarPath);
            if (File.Exists(r.Filename))
            {
                AllRecordings.Add(r.Id, r);
            }
        }

        var oldVideoFiles = System.IO.Directory.GetFiles(Config.OldRecordingsPath, "*.ts");

        foreach (var r in OldRecordings.Values)
        {
            if (File.Exists(r.Filename))
            {
                AllRecordings.Add(r.Id, r);
            }
        }
    }

    /// <summary>
    /// All recordings, most recent first
    /// </summary>
    static public IEnumerable<Recording> AllRecordingsInReverseTimeOrder
    {
        get { return AllRecordings.Values.OrderByDescending(r => r.StartTime); }
    }

    /// <summary>
    /// All recordings sharing the same title, most recent first
    /// </summary>
    /// <param name="title"></param>
    /// <returns></returns>
    static public IEnumerable<Recording> AllRecordingsForTitle(
        string title)
    {
        return AllRecordingsInReverseTimeOrder.Where(r => r.Title == title);
    }

    internal static void DeleteRecording(Recording recording)
    {
        File.Delete(recording.Filename);
        if (recording.SidecarPath != null && File.Exists(recording.SidecarPath))
        {
            File.Delete(recording.SidecarPath);
        }
        AllRecordings.Remove(recording.Id);
    }

    /// <summary>
    /// All recordings as a collection of Lists, grouped with those sharing a title in the same List
    /// </summary>
    static public IEnumerable<List<Recording>> AllRecordingsGroupedByTitle
    {
        get
        {
            Dictionary<string, List<Recording>> recordingsForTitle = new Dictionary<string, List<Recording>>();

            foreach (Recording r in AllRecordingsInReverseTimeOrder)
            {
                if (!recordingsForTitle.ContainsKey(r.Title))
                {
                    recordingsForTitle[r.Title] = new List<Recording>();
                }
                recordingsForTitle[r.Title].Add(r);
            }

            return recordingsForTitle.Values;
        }
    }

    static List<Programme> allProgrammes;

    static string RepairBrokenXml(string xmlText)
    {
        var ss = xmlText.Split('\n');
        var sb = new StringBuilder();
        foreach (var sl in ss)
        {
            if ( sl.Contains("="))
            {
                sb.Append(sl);
            }
            else 
            {
                bool isTag = false;
                foreach (char c in sl)
                {
                    var c1 = c;
                    switch (c)
                    {
                        case ' ':
                            if (isTag) c1 = '_';
                            break;
                        case '<':
                            isTag = true;
                            break;
                        case '>':
                            isTag = false;
                            break;
                        default: break;
                    }
                    sb.Append(c1);
                }
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    static void LoadAllEpgProgrammes()
    {
        allProgrammes = new List<Programme>();
        for (int i = 0; i < 14; i++)
        {
            for (var j = 0; j < 10; j++)
            {
                var date = DateTime.Today.AddDays(i).ToShortDateString();
				var channels = String.Join(",", AllChannels.Select(c => c.Key));
				var xml = JRMC.GetXml(JRMC.Url + $"Television/GetGuidePrograms?StartDate={date}&Channels={channels}");
                if (xml == null)
                {
                    Thread.Sleep(1000);
                } 
                else
                {
                    var programsItem = xml.Root.Element("Item");
                    var xmlText = RepairBrokenXml(programsItem.Value);
                    var programmesXml = XDocument.Parse("<Item>\n" + xmlText + "\n</Item>");
                    allProgrammes.AddRange(programmesXml.Root.Elements("Prog").Select(x => new Programme(x)));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Get the collection of programmes from the EPG scheduled to be broadcast on a specified date for a specified channel name
    /// </summary>
    /// <param name="day"></param>
    /// <param name="channelName"></param>
    /// <returns></returns>
    public static IEnumerable<Programme> GetEpgProgrammesForDay(
        DateTime day,
        Channel channel)
    {
        DateTime nextDay = day + new TimeSpan(1, 0, 0, 0);
        return GetEpgProgrammesInRange(day, nextDay, channel);
    }

    /// <summary>
    /// Get once and cache the collection of programmes from the EPG scheduled to be broadcast on a specified date for a specified channel name
    /// </summary>
    /// <param name="day"></param>
    /// <param name="channelName"></param>
    /// <returns></returns>
    public static IEnumerable<Programme> GetEpgProgrammesForChannel(
        Channel channel)
    {
        if (channel == null)
        {
            return null;
        }

        if (!epgProgrammesByChannel.ContainsKey(channel.Name))
        {
            epgProgrammesByChannel[channel.Name] =
                allProgrammes.Where(p => p.Channel==channel && !p.InError);

            foreach (var programme in epgProgrammesByChannel[channel.Name])
            {
                epgProgrammesByIdAndChannel[MakeIdAndChannelKey(programme.Id, channel.Name)] = programme;
            }
        }

        return epgProgrammesByChannel[channel.Name];
    }
    static Dictionary<String, IEnumerable<Programme>> epgProgrammesByChannel = new Dictionary<String, IEnumerable<Programme>>();
    static String MakeIdAndChannelKey(
        String Id,
        String channelName)
    {
        return Id + ";" + channelName;
    }

    static Dictionary<String, Programme> epgProgrammesByIdAndChannel = new Dictionary<String, Programme>();

    public static Programme EpgProgramme(
        String Id,
        String channelName)
    {
        return epgProgrammesByIdAndChannel[MakeIdAndChannelKey(Id, channelName)];
    }

    /// <summary>
    /// Get the (at most two) "Now and Next" programmes for a specified channel name
    /// </summary>
    /// <param name="channelName"></param>
    /// <returns></returns>
    public static IEnumerable<Programme> GetNowAndNext(
        Channel channel)
    {
        if (channel == null) return null;

        var epgProgrammes = GetEpgProgrammesForChannel(channel);
        return epgProgrammes == null ?
            new Programme[0] :
            epgProgrammes.SkipWhile(p => p.StopTime <= DateTime.UtcNow).Take(2);
    }

    /// <summary>
    /// Get the collection of programmes from the EPG within a specified date range for the specified channel
    /// </summary>
    /// <remarks>
    /// Only programmes that have not yet stopped will be returned
    /// </remarks>
    /// <param name="startTime"></param>
    /// <param name="endTime"></param>
    /// <param name="channelName"></param>
    /// <returns></returns>
    public static IEnumerable<Programme> GetEpgProgrammesInRange(
        DateTime startTime,
        DateTime endTime,
        Channel channel)
    {
        var epgProgrammes = GetEpgProgrammesForChannel(channel);
        return epgProgrammes == null ?
            new Programme[0] :
            epgProgrammes.SkipWhile(p => p.StartTime <= startTime).TakeWhile(p => p.StartTime <= endTime);
    }


    /// <summary>
    /// Schedule a recording for an identified programme from the EPG
    /// </summary>
    /// <param name="programmeId"></param>
    public static void AddTimer(
        String id,
        String channelName,
        bool isSeries = false)
    {
        string key = MakeIdAndChannelKey(id, channelName);
        if (epgProgrammesByIdAndChannel.ContainsKey(key))
        {
            AddTimer(epgProgrammesByIdAndChannel[key], isSeries);
        }
    }

    /// <summary>
    /// Schedule a recording for an identified programme from the EPG
    /// </summary>
    /// <param name="programmeId"></param>
    public static void AddTimer(
        Programme programme,
        bool isSeries = false)
    {
        if (programme != null)
        {
            JRMC.GetXml(JRMC.Url + $"Television/SetRecording?RuleName={programme.Title}&RecType=1&ProgKey={programme.Id}&Channels={programme.Channel.Key}&ExtBefore=5&ExtAfter=10");
            if (isSeries)
            {
                LoadSchedule();
                var timer = Schedule.Values.FirstOrDefault(t => t.EventId == programme.Id);

                if (timer != null)
                {
                    Series series = Series.Find(timer.Name, timer.Channel, timer.StartTime);
                    if (series == null)
                    {
                        Series.Add(timer.EventId, timer.Name, timer.Channel, timer.StartTime);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cancel the scheduled recording for an identified programme
    /// </summary>
    /// <param name="timerId"></param>
    public static void CancelTimer(
        String timerId)
    {
        if (schedule.ContainsKey(timerId))
        {
            var timer = schedule[timerId];
            JRMC.GetXml(JRMC.Url + $"Television/CancelRecording?RecRuleID={timer.Id}&CancelType=0&ProgKey={timer.EventId}");
            schedule.Remove(timerId);
        }
    }

    public static void Stop()
    {
        JRMC.ClearPlaylist();
        IsDvdMode = false; ;
    }

    /// <summary>
    /// The title of the video or DVD that is currently playing
    /// </summary>
    public static string Title { get; set; }

    /// <summary>
    /// Is a DVD currently playing?
    /// </summary>
    public static bool IsDvdMode { get; set; }

    public static void WatchLive()
    {
        JRMC.GetXml(JRMC.Url + "Control/MCC?Command=30002");
        JRMC.GoFullScreen();

	}

    public static void SelectChannel(
        Channel channel)
    {
        JRMC.GetXml(JRMC.Url + $"Playback/PlayByIndex?Index={channel.Index}");
    }

    public static string GetPlaybackInfo(string key)
    {
        var info = JRMC.GetXml(JRMC.Url + "Playback/Info");
        if (info != null)
        {
            var dict = MakeDict(info.Root);
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
        }
		return "";
	}

	public static bool IsWatchingTv
    {
        get
        {
            var infokey = GetPlaybackInfo("FileKey");
            return infokey!= null && AllChannels.ContainsKey(infokey);
        }
    }

    public static Programme CurrentlyWatching
    {
        get
        {
            var infokey = GetPlaybackInfo("FileKey");
            if (infokey != null && AllChannels.ContainsKey(infokey))
            {
                return GetNowAndNext(AllChannels[infokey]).First();

            }
            return null;
        }
    }

    public static void SendCommand(string command)
    {

    }

}