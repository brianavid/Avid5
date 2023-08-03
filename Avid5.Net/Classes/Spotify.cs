using Avid.Spotify;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Http;
using SpotifyAPI.Web;
using NLog;
using System.Net;
using System.Net.Cache;
using Newtonsoft.Json;
using System.Xml.Linq;
using static Avid.Spotify.SpotifyData;
using System.Diagnostics;

/// <summary>
/// Class of static methods to access the Spotify player through its WebAPI interface
/// </summary>
public static class Spotify
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    static SpotifyClient webAppService = null;
    static DateTime webApiExpiry = DateTime.Now;
    static string webApiCurrentUserId = null;
    static object webAppServiceLock = new object();

    static string playbackDevice = null;

    static Dictionary<String, FullArtist> artistCache = new Dictionary<String, FullArtist>();
    static Dictionary<String, FullAlbum> albumCache = new Dictionary<String, FullAlbum>();
    static Dictionary<String, FullTrack> trackCache = new Dictionary<String, FullTrack>();

    static IEnumerable<SpotifyData.Album> AllSavedAlbumList = null;
    static SpotifyData.Album[] AllSavedAlbums;
    static SpotifyData.Artist[] AllSavedArtists;

    static string PreferredMarket = Config.SpotifyMarket ?? "GB";

    /// <summary>
    /// Initialize and memoize the we API service using the authentication token stored in the registry
    /// </summary>
    static SpotifyClient WebAppService
    {
        get
        {
            lock (logger)
            {
	            if (webAppService == null || webApiExpiry <= DateTime.Now)
	            {
                    logger.Info("Connecting and authenticating to Spotify Web API");
	                try
	                {
	                    string refreshUrl = Config.ReadValue("SpotifyRefreshUrl") as string;

	                    if (!string.IsNullOrEmpty(refreshUrl))
		                {
		                    HttpWebRequest request =
		                        (HttpWebRequest)HttpWebRequest.Create(refreshUrl);
		                    request.Method = WebRequestMethods.Http.Get;
		                    request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);

		                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
		                    var tokenJsonString = new StreamReader(response.GetResponseStream()).ReadToEnd();
		                    if (!string.IsNullOrEmpty(tokenJsonString))
		                    {
                                AuthorizationCodeTokenResponse token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(tokenJsonString);
		                        if (!string.IsNullOrEmpty(token.AccessToken) && !string.IsNullOrEmpty(token.TokenType))
		                        {
                                    if (!string.IsNullOrEmpty(token.RefreshToken))
                                    {
                                        var equalsPos = refreshUrl.LastIndexOf('=');
                                        if (equalsPos > 0)
                                        {
                                            var newRefreshUrl = refreshUrl.Substring(0, equalsPos + 1) + token.RefreshToken;
                                            if (newRefreshUrl != refreshUrl)
                                            {
                                                try
                                                {
	                                                Config.SaveValue("SpotifyRefreshUrl", newRefreshUrl);
	                                                logger.Info("Updated saved authentication data for Spotify Web API");
                                                }
                                                catch (System.Exception ex)
                                                {
                                                    logger.Info(ex, "Unable to update saved authentication data for Spotify Web API", ex.Message);
                                                }
                                            }
                                        }
                                    }
                                    webApiExpiry = DateTime.Now.AddSeconds(token.ExpiresIn * 4 / 5);    // Only use the token for 80% of its promised life
                                    webAppService = new SpotifyClient(token.AccessToken);

                                    webApiCurrentUserId = webAppService.UserProfile.Current().Id.ToString();
                                    logger.Info("Connected and authenticated {0} to Spotify Web API (expires at {1})",
                                        webApiCurrentUserId, webApiExpiry.ToShortTimeString());

                                }
                                else
	                            {
	                                logger.Error("Invalid response from authentication for Spotify Web API");
	                            }
	                        }
	                        else
	                        {
	                            logger.Error("No response from authentication for Spotify Web API");
	                        }
		                }
	                    else
	                    {
	                        logger.Error("No saved authentication data for Spotify Web API");
	                    }
	                }
	                catch (System.Exception ex)
	                {
                        logger.Error(ex, "Failed to connect to Spotify Web API: {0}", ex.Message);
                    }
	            }

                if (webAppService == null || webApiExpiry <= DateTime.Now)
                {
                    logger.Error("Failed to connect to Spotify Web API");
                }

                if (AllSavedAlbumList == null && webAppService != null)
                {
                    LoadAndIndexAllSavedTracks();
                }
            }

            return webAppService;
        }
    }

    public static bool Probe()
    {
        lock (logger)
        {
            if (webAppService == null || webApiExpiry <= DateTime.Now)
            {
                logger.Info("Probing Authentication API");
                try
                {
                    HttpWebRequest request =
                        (HttpWebRequest)HttpWebRequest.Create("http://brianavid.dnsalias.com/SpotifyAuth/Auth/Probe");
                    request.Method = WebRequestMethods.Http.Get;
                    request.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
                    request.Timeout = 10000;

                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    return response.StatusCode == HttpStatusCode.OK;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to probe Authentication API: {0}", ex.Message);
                    return false;
                }
            }
        }

        return true;
    }

    static FullTrack GetFullTrack(
        string id)
    {
        if (id == null) return null;

        if (!trackCache.ContainsKey(id))
        {
            lock (webAppServiceLock)
            {
                trackCache[id] = WebAppService.Tracks.Get(id).Result;
            }
        }
        return trackCache[id];
    }

    static FullAlbum GetFullAlbum(
        string id)
    {
        if (id == null) return null;

        if (!albumCache.ContainsKey(id))
        {
            lock (webAppServiceLock)
            {
                albumCache[id] = WebAppService.Albums.Get(id).Result;
            }
        }
        return albumCache[id];
    }

    static FullArtist GetFullArtist(
        string id)
    {
        if (id == null) return null;

        if (!artistCache.ContainsKey(id))
        {
            lock (webAppServiceLock)
            {
                artistCache[id] = WebAppService.Artists.Get(id).Result;
            }
        }
        return artistCache[id];
    }

    /// <summary>
    /// Helper function to turn an unbounded IEnumerable collection into a collection of collections
    /// where each inner collection is no larger than batchSize
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="batchSize"></param>
    /// <returns></returns>
    static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> collection, int batchSize)
    {
        List<T> nextbatch = new List<T>(batchSize);
        foreach (T item in collection)
        {
            nextbatch.Add(item);
            if (nextbatch.Count == batchSize)
            {
                yield return nextbatch;
                nextbatch = new List<T>();
            }
        }
        if (nextbatch.Count > 0)
            yield return nextbatch;
    }


    /// <summary>
    /// Helper comparator function to compare albums, first by artist name and then
    /// (for the same artist) by the album name
    /// </summary>
    /// <param name="a1"></param>
    /// <param name="a2"></param>
    /// <returns></returns>
    private static int CompareAlbumByArtist(
        SpotifyData.Album a1,
        SpotifyData.Album a2)
    {
        var result = a1.ArtistName.CompareTo(a2.ArtistName);
        return result != 0 ? result : a1.Name.CompareTo(a2.Name);
    }


    /// <summary>
    /// Load and index all saved track, to build arrays of saved albums and saved artists
    /// </summary>
    public static void LoadAndIndexAllSavedTracks()
    {
        if (webAppService != null)
        {
            logger.Info("LoadAndIndexAllSavedTracks start");

            AllSavedAlbumList = new List<SpotifyData.Album>(); // prevents reentrancy

            for (var retries = 0; retries < 20; retries++)
            {
                try
                {
                    IAsyncEnumerable<SavedAlbum> pagedAlbums;

                    lock (webAppServiceLock)
                    {
                        pagedAlbums = WebAppService.Paginate(WebAppService.Library.GetAlbums().Result);
                    }
                    AllSavedAlbums = MakeAlbums(pagedAlbums).ToArray();

                    logger.Info("LoadAndIndexAllSavedTracks {0} albums", AllSavedAlbums.Count());
                    break;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }

            HashSet<String> artistIds = new HashSet<String>();
            foreach (var album in AllSavedAlbums)
            {
                if (!artistIds.Contains(album.ArtistId))
                {
                    artistIds.Add(album.ArtistId);
                }
            }

            List<SpotifyData.Artist> savedArtistList = new List<SpotifyData.Artist>();
            foreach (var batch in artistIds.Batch(20))
            {
                try
                {
                    var batchOfIds = batch.Select(id => SimplifyId(id)).ToList();
                    IEnumerable<FullArtist> batchOfArtists;
                    lock (webAppServiceLock)
                    {
                        batchOfArtists = WebAppService.Artists.GetSeveral(new ArtistsRequest(batchOfIds)).Result.Artists;
                    }
                    if (batchOfArtists != null)
                    {
                        foreach (var artist in batchOfArtists)
                        {
                            savedArtistList.Add(MakeArtist(artist));
                        }
                    }
                    logger.Info("LoadAndIndexAllSavedTracks {0}/{1} artists", savedArtistList.Count, artistIds.Count);

                    if (savedArtistList.Count == artistIds.Count)
                        break;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }

            AllSavedArtists = savedArtistList.ToArray();

            Array.Sort(AllSavedAlbums, CompareAlbumByArtist);
            Array.Sort(AllSavedArtists, (a1, a2) => a1.Name.CompareTo(a2.Name));
        }
    }

    /// <summary>
    /// Format a track's duration for display
    /// </summary>
    /// <param name="rawDuration"></param>
    /// <returns></returns>
    public static string FormatDuration(int seconds)
    {
        return seconds < 0 ? "<0:00" : string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
    }

    public static void Initialize()
    {
        Probe();
    }

    #region Browsing and Searching
    /// <summary>
    /// Search Spotify for tracks matching the specified track name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Track> SearchTracks(
        string name)
    {
        if (WebAppService != null)
        {
            logger.Info("SearchTracks {0}", name);

            try
            {
                IAsyncEnumerable<FullTrack> tracks;
                lock (webAppServiceLock)
                {
                    SearchResponse searchResult = WebAppService.Search.Item(new SearchRequest(SearchRequest.Types.Track, HttpUtility.UrlEncode(name))).Result;
                    tracks = WebAppService.Paginate(searchResult.Tracks, (s) => s.Tracks);
                }
                return MakeTracks(tracks);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Track>();
    }

    /// <summary>
    /// Search Spotify for albums matching the specified album name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Album> SearchAlbums(
        string name)
    {
        if (WebAppService != null)
        {
            logger.Info("SearchAlbums {0}", name);

            try
            {
                IAsyncEnumerable<SimpleAlbum> albums;
                lock (webAppServiceLock)
                {
                    SearchResponse searchResult = WebAppService.Search.Item(new SearchRequest(SearchRequest.Types.Album, HttpUtility.UrlEncode(name))).Result;
                    albums = WebAppService.Paginate(searchResult.Albums, (s) => s.Albums);
                }
                return MakeAlbums(albums);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Album>();
    }

    /// <summary>
    /// Search Spotify for artists matching the specified artist name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Artist> SearchArtists(
        string name)
    {
        if (WebAppService != null)
        {
            logger.Info("SearchArtists {0}", name);

            try
            {
                IAsyncEnumerable<FullArtist> artists;
                lock (webAppServiceLock)
                {
                    SearchResponse searchResult = WebAppService.Search.Item(new SearchRequest(SearchRequest.Types.Artist, HttpUtility.UrlEncode(name))).Result;
                    artists = WebAppService.Paginate(searchResult.Artists, (s) => s.Artists);
                }
                return MakeArtists(artists);

            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Artist>();
    }

    /// <summary>
    /// Return track data for a track
    /// </summary>
    /// <param name="id">The Spotify Track URI</param>
    /// <returns></returns>
    public static SpotifyData.Track GetTrackById(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return MakeTrack(GetFullTrack(SimplifyId(id)));
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Return album data for an identified album
    /// </summary>
    /// <param name="id">The Spotify Album URI</param>
    /// <returns></returns>
    public static SpotifyData.Album GetAlbumById(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return MakeAlbum(GetFullAlbum(SimplifyId(id)));
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Return artist data for an identified artist
    /// </summary>
    /// <param name="id">The Spotify Artist URI</param>
    /// <returns></returns>
    public static SpotifyData.Artist GetArtistById(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return MakeArtist(WebAppService.Artists.Get(SimplifyId(id)).Result);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return null;
    }

    class ArtistHistory
    {
        static string XmlFilename = Config.FilePath("SpotifyArtists.xml");
        const int MaxHistory = 50;
        public string Name;
        public string Id;

        static List<ArtistHistory> artistHistory = null;

        ArtistHistory(
            string name,
            string id)
        {
            Id = id;
            Name = name;
        }

        ArtistHistory(
            XElement xSeries)
        {
            try
            {
                Id = xSeries.Attribute("Id").Value;
                Name = xSeries.Attribute("Name").Value;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex, "Error parsing ArtistHistory XML: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Load the ...
        /// </summary>
        static void Load()
        {
            if (artistHistory == null)
            {
                if (File.Exists(XmlFilename))
                {
                    XElement artistHistoryDoc = XDocument.Load(XmlFilename, LoadOptions.None).Root;
                    artistHistory = artistHistoryDoc.Elements("Artist")
                        .Select(s => new ArtistHistory(s))
                        .ToList();
                }
                else
                {
                    artistHistory = new List<ArtistHistory>();
                }
            }
        }

        static void Save()
        {
            XElement root = new XElement("Artists",
                artistHistory.Select(s => s.ToXml));
            root.Save(XmlFilename);
        }

        XElement ToXml
        {
            get
            {
                return new XElement("Artist",
                    new XAttribute("Id", Id),
                    new XAttribute("Name", Name));
            }
        }

        public static List<ArtistHistory> All
        {
            get {
                if (artistHistory == null)
                {
                    Load();
                }
                return artistHistory;
            }
        }

        public static void Add(
            string name,
            string id)
        {
            var newHistory = All.Where(h => h.Id != id).Take(MaxHistory - 1).ToList();
            newHistory.Insert(0, new ArtistHistory(name, id));
            artistHistory = newHistory;
            Save();
        }
    }

    /// <summary>
    /// Get the collection of albums for an identified artist
    /// </summary>
    /// <param name="id">The Spotify Artist URI</param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Album> GetAlbumsForArtist(
        string id)
    {
        if (WebAppService != null)
        {
            try
            {
                var artist = GetArtistById(id);
                ArtistHistory.Add(artist.Name, artist.Id);

                IAsyncEnumerable<SimpleAlbum> albums;
                lock (webAppServiceLock)
                {
                    albums = WebAppService.Paginate(WebAppService.Artists.GetAlbums(SimplifyId(id)).Result);
                }
                return MakeAlbums(albums);

            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Album>();
    }

    /// <summary>
    /// Get the collection of similar artists for an identified artist
    /// </summary>
    /// <param name="id">The Spotify Artist URI</param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Artist> GetSimilarArtistsForArtist(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Artists.GetRelatedArtists(SimplifyId(id)).Result.Artists.Select(a => MakeArtist(a));
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return new List<SpotifyData.Artist>();
    }

    /// <summary>
    /// Get the collection of recently searched artists
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Artist> GetHistoryArtists()
    {
        return ArtistHistory.All.Select(h => MakeArtist(h));
    }

    /// <summary>
    /// Get the collection of tracks for an identified album
    /// </summary>
    /// <param name="id">The Spotify Album URI</param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Track> GetTracksForAlbum(
        string id)
    {
        if (WebAppService != null)
        {
            try
            {
                IAsyncEnumerable<SimpleTrack> tracks;
                lock (webAppServiceLock)
                {
                    tracks = WebAppService.Paginate(WebAppService.Albums.GetTracks(SimplifyId(id)).Result);
                }
                return MakeTracks(
                        tracks,
                        GetFullAlbum(SimplifyId(id)));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Track>();
    }

    /// <summary>
    /// Get the cover image Url for an identified album
    /// </summary>
    /// <param name="id">The Spotify Album URI</param>
    /// <returns></returns>
    public static String GetAlbumImageUrl(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var a = GetFullAlbum(SimplifyId(id));
                    if (a != null && a.Images.Count != 0)
                    {
                        return a.Images[0].Url;
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return null;
    }

    #endregion

    #region Playlists and My Music
    public static Dictionary<String, SpotifyData.Playlist> CurrentPlaylists { get; private set; }

    /// <summary>
    /// Get the collection of named playlists, rebuilding from data on Spotify
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Playlist> GetPlayLists()
    {
        if (WebAppService != null)
        {
            try
            {
                IAsyncEnumerable<SimplePlaylist> pagingPlaylist;

                lock (webAppServiceLock)
                {
                    pagingPlaylist = WebAppService.Paginate(WebAppService.Playlists.GetUsers(webApiCurrentUserId).Result);
                }
                var playlists = MakePlaylists(pagingPlaylist);
                CurrentPlaylists = playlists.ToDictionary(p => p.Name);
                return playlists;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Playlist>();
    }

    /// <summary>
    /// Get the collection of tracks for an identified playlist
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Track> GetPlayListTracks(
        string id)
    {
        if (WebAppService != null)
        {
            try
            {
                IAsyncEnumerable<PlaylistTrack<IPlayableItem>> tracks;
                lock (webAppServiceLock)
                {
                    tracks = WebAppService.Paginate(WebAppService.Playlists.GetItems(SimplifyId(id)).Result);
                }
                return MakeTracks(tracks.Where(t=>t.Track.Type == ItemType.Track).Select(t=>t.Track as FullTrack), null);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Track>();
    }

    /// <summary>
    /// Get the collection of albums for an identified playlist
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Album> GetPlayListAlbums(
        string id)
    {
        if (WebAppService != null)
        {
            try
            {
                IEnumerable<SpotifyData.Track> tracks = GetPlayListTracks(id);
                HashSet<String> albumIds = new HashSet<String>();
                foreach (var track in tracks)
                {
                    if (!albumIds.Contains(track.AlbumId))
                    {
                        albumIds.Add(track.AlbumId);
                    }
                }

                return albumIds.Select(a => MakeAlbum(GetFullAlbum(SimplifyId(a))));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Album>();
    }

    /// <summary>
    /// Add a new (empty) named playlist
    /// </summary>
    /// <param name="name"></param>
    public static string AddPlayList(
        string name)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Playlists.Create(webApiCurrentUserId, new PlaylistCreateRequest(name)).Result.Uri;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Delete an identified playlist (just unfollows - does not actually delete)
    /// </summary>
    /// <param name="id"></param>
    public static void DeletePlayList(
        string id)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {

                    var ok = WebAppService.Follow.UnfollowPlaylist(SimplifyId(id)).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Rename an identified playlist
    /// </summary>
    /// <param name="id"></param>
    /// <param name="newName"></param>
    public static void RenamePlayList(
        string id,
        string newName)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new PlaylistChangeDetailsRequest();
                    request.Name = newName;
                    var ok = WebAppService.Playlists.ChangeDetails(SimplifyId(id), request).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Add an identified track to an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="trackId"></param>
    public static void AddTrackToPlayList(
        string playlistId,
        string trackId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new PlaylistAddItemsRequest(new List<string> { GetFullTrack(trackId).Uri });
                    var ok = WebAppService.Playlists.AddItems( SimplifyId(playlistId), request).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Add all the tracks of an identified album to an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="albumId"></param>
    public static void AddAlbumToPlayList(
        string playlistId,
        string albumId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var tracks = GetTracksForAlbum(SimplifyId(albumId));
                    var request = new PlaylistAddItemsRequest(tracks.Select(t => t.Id).ToList());
                    var ok = WebAppService.Playlists.AddItems(SimplifyId(playlistId), request).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Remove an identified track from an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="trackId"></param>
    public static void RemoveTrackFromPlayList(
        string playlistId,
        string trackId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new PlaylistRemoveItemsRequest();
                    request.Tracks = new PlaylistRemoveItemsRequest.Item[1];
                    request.Tracks[0].Uri = trackId;
                    var ok = WebAppService.Playlists.RemoveItems( SimplifyId(playlistId), request).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Remove all the tracks of an identified album from an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="albumId"></param>
    public static void RemoveAlbumFromPlayList(
        string playlistId,
        string albumId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var tracks = GetTracksForAlbum(SimplifyId(albumId));
                    var request = new PlaylistRemoveItemsRequest();
                    request.Tracks = tracks.Select(t => new PlaylistRemoveItemsRequest.Item { Uri = t.Id } ).ToList();;
                    var ok = WebAppService.Playlists.RemoveItems(SimplifyId(playlistId), request).Result;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Get the collection of albums saved by the current user
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Album> GetSavedAlbums()
    {
        logger.Info("Get Saved Albums");

        return AllSavedAlbums;
    }

    /// <summary>
    /// Get the collection of artists saved by the current user
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Artist> GetSavedArtists()
    {
        logger.Info("Get Saved Artists");

        return AllSavedArtists;
    }

    /// <summary>
    /// Save the  identified album as a saved album
    /// </summary>
    /// <param name="albumId"></param>
    public static void AddSavedAlbum(
        string albumId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new LibrarySaveAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var ok = WebAppService.Library.SaveAlbums(request).Result;
                    AllSavedAlbumList = null;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Remove the identified album as a saved album
    /// </summary>
    /// <param name="albumId"></param>
    public static void RemoveSavedAlbum(
        string albumId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new LibraryRemoveAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var ok = WebAppService.Library.RemoveAlbums(request).Result;
                    AllSavedAlbumList = null;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
    }

    /// <summary>
    /// Determine if all the identified album is saved
    /// </summary>
    /// <param name="albumId"></param>
    public static Boolean IsSavedAlbum(
        string albumId)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new LibraryCheckAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var saveIndications = WebAppService.Library.CheckAlbums(request).Result;
                    return saveIndications.First();
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }

        return false;
    }

    #endregion

    #region Player Queue Management

    static string GetPlaybackDevice()
    {
        if (playbackDevice == null)
        {
            var devices = WebAppService.Player.GetAvailableDevices().Result;
            if (devices != null && devices.Devices != null)
            {
                foreach (var dev in devices.Devices)
                {
                    logger.Info($"Spotify play device found: {dev.Name} [{dev.Type}]");
                    if (dev.Type == "avr")
                    {
                        playbackDevice = dev.Id;
                    }
                }
            }
        }

        return playbackDevice;
    }

    /// <summary>
    /// Play the identified track, either immediately or after the currently queued tracks
    /// </summary>
    /// <param name="id"></param>
    /// <param name="append"></param>
    /// <returns></returns>
    public static Boolean PlayTrack(
        string id,
        bool append = false)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    if (append)
                    {
                        var request = new PlayerAddToQueueRequest(id);
                        return WebAppService.Player.AddToQueue(request).Result;
                    }
                    else
                    {
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        request.Uris = new List<string> { id };
                        request.OffsetParam.Position = 0;
                        return WebAppService.Player.ResumePlayback(request).Result;
                    }
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Play all tracks of the identified album, either immediately or after the currently queued tracks
    /// </summary>
    /// <param name="id"></param>
    /// <param name="append"></param>
    /// <returns></returns>
    public static Boolean PlayAlbum(
        string id,
        bool append = false)
    {
        if (WebAppService != null)
        {
            try
            {
                if (append)
                {
                    var tracks = GetTracksForAlbum(SimplifyId(id));
                    lock (webAppServiceLock)
                    {
                        foreach (var t in tracks)
                        {
                            var request = new PlayerAddToQueueRequest(t.Id);
                            if (!WebAppService.Player.AddToQueue(request).Result)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
                else
                {
                    lock (webAppServiceLock)
                    {
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        request.ContextUri = id;
                        request.OffsetParam.Position = 0;
                        return WebAppService.Player.ResumePlayback(request).Result;
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return false;
    }

    /// <summary>
    /// Play all tracks of the identified playlist, either immediately or after the currently queued tracks
    /// </summary>
    /// <param name="id"></param>
    /// <param name="append"></param>
    /// <returns></returns>
    public static Boolean PlayPlaylist(
        string id,
        bool append = false)
    {
        if (WebAppService != null)
        {
            try
            {
                if (append)
                { 
                    var tracks = GetPlayListTracks(id);
                    lock (webAppServiceLock)
                    {
                        foreach (var t in tracks)
                        {
                            var request = new PlayerAddToQueueRequest(t.Id);
                            if (!WebAppService.Player.AddToQueue(request).Result)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
                else
                {
                    lock (webAppServiceLock)
                    {
                        lock (webAppServiceLock)
                        {
                            var request = new PlayerResumePlaybackRequest();
                            request.DeviceId = GetPlaybackDevice();
                            request.ContextUri = id;
                            request.OffsetParam.Position = 0;
                            return WebAppService.Player.ResumePlayback(request).Result;
                        }
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return false;
    }

    /// <summary>
    /// Get the currently playing track
    /// </summary>
    /// <returns></returns>
    public static SpotifyData.Track GetCurrentTrack()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new PlayerCurrentlyPlayingRequest();
                    var playingTrack = WebAppService.Player.GetCurrentlyPlaying(request).Result.Item as FullTrack;
                    return MakeTrack(playingTrack);
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Get the collection of all queued tracks
    /// </summary>
    /// <remarks>
    /// This queued list only contains the tracks from the original context (album or playlist) and takes no account of any 
    /// tracks subsequently added to the queue
    /// </remarks>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Track> GetQueuedTracks()
    {
        if (WebAppService != null)
        {
            try
            {
                var request = new PlayerCurrentPlaybackRequest();
                var playback = WebAppService.Player.GetCurrentPlayback(request).Result;
                if (playback.Context != null)
                {
                    if (playback.Context.Type == "album")
                    {
                        return GetTracksForAlbum(SimplifyId(playback.Context.Uri));
                    }
                    if (playback.Context.Type == "playlist")
                    {
                        return GetPlayListTracks(SimplifyId(playback.Context.Uri));
                    }
                }

                if (playback.Item != null)
                {
                    return new List<SpotifyData.Track> { MakeTrack(GetFullTrack(SimplifyId((playback.Item as FullTrack).Uri))) };
                }
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return new List<SpotifyData.Track>();
    }

    /// <summary>
    /// Skip to a specified queued track
    /// </summary>
    public static SpotifyData.Track SkipToQueuedTrack(
        string id)
    {
        var playback = WebAppService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
        var request = new PlayerResumePlaybackRequest();
        request.DeviceId = GetPlaybackDevice();
        request.ContextUri = playback.Context.Uri;
        request.OffsetParam.Uri = id;
        var ok = WebAppService.Player.ResumePlayback(request).Result;
        return GetCurrentTrack();
    }

    /// <summary>
    /// Remove the specified queued track from the queue
    /// </summary>
    public static SpotifyData.Track RemoveQueuedTrack(
        string id)
    {
        return GetCurrentTrack();
    }
    #endregion

    #region Player currently playing track operations
    /// <summary>
    /// Skip playing forwards to the next queued track
    /// </summary>
    /// <returns></returns>
    public static int Skip()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Player.SkipNext().Result ? 0 : -1;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Skip playing backwards to the previous queued track
    /// </summary>
    /// <returns></returns>
    public static int Back()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Player.SkipPrevious().Result ? 0 : -1;
                    return 0;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Start or continue playing the current track
    /// </summary>
    /// <returns></returns>
    public static int Play()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var playback = WebAppService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
                    if (!playback.IsPlaying && playback.Item != null)
                    {
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        request.ContextUri = playback.Context.Uri;
                        request.OffsetParam.Uri = (playback.Item as FullTrack).Id;
                        request.OffsetParam.Position = playback.ProgressMs;
                        return WebAppService.Player.ResumePlayback(request).Result ? 0 : -1;
                    }
                    return 0;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Pause playing the current track
    /// </summary>
    /// <returns></returns>
    public static int Pause()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Player.PausePlayback().Result ? 0 : -1;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Is the player playing a track?
    /// </summary>
    /// <returns>+ve: Playing; 0: Paused; -ve: Stolen by another session</returns>
    public static int GetPlaying()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var playback = WebAppService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
                    return playback == null ? -1 : playback.IsPlaying ? 1 : 0;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Stop playing the current track
    /// </summary>
    /// <returns></returns>
    public static int Stop()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    return WebAppService.Player.PausePlayback().Result ? 0 : -1;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Get the position at which the current track is playing
    /// </summary>
    /// <returns>Position in seconds</returns>
    public static int GetPosition()
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var playback = WebAppService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
                    return playback == null ? -1 : playback.ProgressMs/1000;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Seek to a particular position within the currently playing track
    /// </summary>
    /// <param name="pos">Position in seconds</param>
    /// <returns></returns>
    public static int SetPosition(
        int pos)
    {
        if (WebAppService != null)
        {
            lock (webAppServiceLock)
            {
                try
                {
                    var request = new PlayerSeekToRequest(pos);
                    return WebAppService.Player.SeekTo(request).Result ? pos : -1;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }
        }
        return -1;
    }

    public static void ExitPlayer()
    {
        Stop();
    }
    #endregion

    #region Constructors of SpotifyData from Web API model

    /// <summary>
    /// GIven a Spotify URI (an external ID), return the Spotify Id, which is its textually last component
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public static String SimplifyId(
        string id)
    {
        var pos = id.LastIndexOf(':');
        return (pos > 0) ? id.Substring(pos + 1) : id;

    }

    /// <summary>
    /// Make an external Artist structure from that returned by the Web API
    /// </summary>
    /// <param name="artist"></param>
    /// <returns></returns>
    static SpotifyData.Artist MakeArtist(SimpleArtist artist)
    {
        return artist == null ? null : new SpotifyData.Artist
        {
            Id = artist.Uri,
            Name = artist.Name
        };
    }

    /// <summary>
    /// Make an external Artist structure from that returned by the Web API
    /// </summary>
    /// <param name="artist"></param>
    /// <returns></returns>
    static SpotifyData.Artist MakeArtist(FullArtist artist)
    {
        return new SpotifyData.Artist
        {
            Id = artist.Uri,
            Name = artist.Name
        };
    }

    /// <summary>
    /// Make an external Artist structure from that int the Artists History
    /// </summary>
    /// <param name="artist"></param>
    /// <returns></returns>
    static SpotifyData.Artist MakeArtist(ArtistHistory artist)
    {
        return new SpotifyData.Artist
        {
            Id = artist.Id,
            Name = artist.Name
        };
    }

    /// <summary>
    /// Make a collection of external Artist structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Artist> MakeArtists(
        IAsyncEnumerable<FullArtist> col)
    {
        return col.Take(200).ToEnumerable().Select(MakeArtist);
    }

    /// <summary>
    /// Get the year of release for an album
    /// </summary>
    /// <param name="album"></param>
    /// <returns></returns>
    static string ReleaseYear(FullAlbum album)
    {
        if (DateTime.TryParse(album.ReleaseDate, out DateTime released))
        {
            return released.Year.ToString();
        }

        return album.ReleaseDate ?? "";
    }

    /// <summary>
    /// Make an external Album structure from that returned by the Web API
    /// </summary>
    /// <param name="album"></param>
    /// <returns></returns>
    static SpotifyData.Album MakeAlbum(FullAlbum album)
    {
        return new SpotifyData.Album
        {
            Id = album.Uri,
            Name = album.Name,
            ArtistId = album.Artists[0].Uri,
            ArtistName = album.Artists[0].Name,
            Year = ReleaseYear(album),
            TrackCount = album.TotalTracks
        };
    }

    /// <summary>
    /// Make a collection of external Album structures from Paging<SimpleAlbum> data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Album> MakeAlbums(
        IAsyncEnumerable<SimpleAlbum> col) => MakeAlbums(col, a => a.Id);

    /// <summary>
    /// Make a collection of external Album structures from Paging<SavedAlbum> data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Album> MakeAlbums(
        IAsyncEnumerable<SavedAlbum> col) => MakeAlbums(col, a => a.Album.Id);

    /// <summary>
    /// Make a collection of external Album structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Album> MakeAlbums<T>(
        IAsyncEnumerable<T> col,
        Func<T, string> GetAlbumId)
    {
        var albumIds = col.Take(200).Select(GetAlbumId).ToEnumerable();
        return WebAppService.Albums.GetSeveral(new AlbumsRequest(albumIds.ToList())).Result.Albums.Select(MakeAlbum);
    }

    /// Make an external Track structure from that returned by the Web API
    /// </summary>
    /// <param name="track"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    /// <summary>
    static SpotifyData.Track MakeTrack(FullTrack track, FullAlbum album = null)
    {
        if (album == null && track != null && track.Album != null)
        {
            album = GetFullAlbum(track.Album.Id);
        }
        try
        {
            var noFullAlbum = album == null || album.Artists == null || album.Artists.Count == 0;
	        return new SpotifyData.Track
	        {
	            Id = track.Uri,
	            Name = track.Name,
                AlbumId = noFullAlbum ? track.Album.Uri : album.Uri,
	            AlbumName = noFullAlbum ? track.Album.Name : album.Name,
	            ArtistId = noFullAlbum ? track.Artists[0].Uri : album.Artists[0].Uri,
	            AlbumArtistName = noFullAlbum ? track.Artists[0].Uri : album.Artists[0].Name,
	            TrackArtistNames = track.Artists.Aggregate("", ConstructTrackArtistNames),
                TrackFirstArtistId = track.Artists[0].Uri,
                Index = track.TrackNumber,
                Count = noFullAlbum ? 0 : album.TotalTracks,
                Duration = track.DurationMs / 1000
	        };
        }
        catch (System.Exception ex)
        {
            logger.Error(ex, "Can't make track {0}: {1}", track.Uri, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Track> MakeTracks(
        IAsyncEnumerable<FullTrack> col,
        FullAlbum album = null)
    {
        return col.Take(200).ToEnumerable().Select(t => MakeTrack(t, album));
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Track> MakeTracks(
        IAsyncEnumerable<FullTrack> col)
    {
        return MakeTracks(col.Select(t => GetFullTrack(t.Id)), null);
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Track> MakeTracks(
        IAsyncEnumerable<SimpleTrack> col,
        FullAlbum album)
    {
        return MakeTracks(col.Select(t => GetFullTrack(t.Id)), album);
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API as a Playlist
    /// </summary>
    /// <param name="col"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Track> MakeTracks(
        IAsyncEnumerable<PlaylistTrack<FullTrack>> col)
    {
        return MakeTracks(col.Select(p => p.Track));
    }

    /// <summary>
    /// Make an external Playlist structure from that returned by the Web API
    /// </summary>
    /// <param name="playlist"></param>
    /// <returns></returns>
    static SpotifyData.Playlist MakePlaylist(SimplePlaylist playlist)
    {
        return new SpotifyData.Playlist
        {
            Id = playlist.Uri,
            Name = playlist.Name,
        };
    }

    /// <summary>
    /// Make a collection of external Playlist structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static IEnumerable<SpotifyData.Playlist> MakePlaylists(
        IAsyncEnumerable<SimplePlaylist> col)
    {
        return col.Take(200).ToEnumerable().Select(MakePlaylist);
    }


    /// <summary>
    /// Construct a formatted string for a (possibly multiple) artist names for a track
    /// </summary>
    /// <param name="names"></param>
    /// <param name="artist"></param>
    /// <returns></returns>
    static string ConstructTrackArtistNames(
        string names,
        SimpleArtist artist)
    {
        const string ellipsis = ", ...";
        if (string.IsNullOrEmpty(names))
        {
            return artist.Name;
        }
        if (names.EndsWith(ellipsis))
        {
            return names;
        }
        if (names.Count(c => c == ',') >= 2)
        {
            return names + ellipsis;
        }

        return names + ", " + artist.Name;
    }
    #endregion

}
