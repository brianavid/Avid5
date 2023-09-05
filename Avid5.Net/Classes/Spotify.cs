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
using System.Net.Http.Headers;

/// <summary>
/// Class of static methods to access the Spotify player through its WebAPI interface
/// </summary>
public static class Spotify
{
    static Logger logger = LogManager.GetCurrentClassLogger();

    //  A publicly available URI running thew OAUTH authentication with the necessary Client Secret
    private static string AuthenticatorUri = Config.SpotifyClientUrl;

    static HttpClient httpClient = new HttpClient();

	static SpotifyClient spotifyService = null;
    static DateTime spotifyApiExpiry = DateTime.Now;
    static string spotifyCurrentUserId = null;
    static object spotifyServiceLock = new object();
    static SpotifyClientConfig spotifyClientConfig;

    static string playbackDevice = null;

    static Dictionary<String, FullArtist> artistCache = new Dictionary<String, FullArtist>();
    static Dictionary<String, FullAlbum> albumCache = new Dictionary<String, FullAlbum>();
    static Dictionary<String, FullTrack> trackCache = new Dictionary<String, FullTrack>();

    static IEnumerable<SpotifyData.Album> AllSavedAlbumList = null;
    static SpotifyData.Album[] AllSavedAlbums;
    static SpotifyData.Artist[] AllSavedArtists;

    static string PreferredMarket = Config.SpotifyMarket ?? "GB";

    public static bool HasAuthenticated
    {
        get { return !string.IsNullOrEmpty(Config.ReadValue("SpotifyRefreshUrl")); }
    }

    /// <summary>
    /// Initialize and memoize the we API service using the authentication token stored in the registry
    /// </summary>
    static SpotifyClient SpotifyService
    {
        get
        {
            lock (logger)
            {
	            if (spotifyService == null || spotifyApiExpiry <= DateTime.Now)
	            {
                    logger.Info("Connecting and authenticating to Spotify Web API");
	                try
	                {
	                    string refreshUrl = Config.ReadValue("SpotifyRefreshUrl");

	                    if (!string.IsNullOrEmpty(refreshUrl))
		                {
                            string tokenJsonString = null;

                            //make the sync GET request to the refresh URL to get an up-to-date access token and a new refresh token
                            using (var request = new HttpRequestMessage(HttpMethod.Get, refreshUrl))
                            {
                                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
								var response = httpClient.Send(request);
                                response.EnsureSuccessStatusCode();
                                tokenJsonString = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                            }

		                    if (!string.IsNullOrEmpty(tokenJsonString))
		                    {
                                //  The token returned (passed through from Spotify) has different JSON representations than in the 
                                //  AuthorizationCodeTokenResponse structure.
                                tokenJsonString = tokenJsonString.
                                    Replace("access_token", "AccessToken").
                                    Replace("expires_in", "ExpiresIn").
                                    Replace("token_type", "TokenType");
                                AuthorizationCodeTokenResponse token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(tokenJsonString);

		                        if (!string.IsNullOrEmpty(token.AccessToken) && !string.IsNullOrEmpty(token.TokenType))
		                        {
                                    //  Replace the parameter value in the existing refresh URL with the newly updated RefreshToken
                                    if (!string.IsNullOrEmpty(token.RefreshToken))
                                    {
                                        var equalsPos = refreshUrl.LastIndexOf('=');
                                        if (equalsPos > 0)
                                        {
                                            var newRefreshUrl = refreshUrl.Substring(0, equalsPos + 1) + token.RefreshToken;
                                            if (newRefreshUrl != refreshUrl)
                                            {
                                                //  Save the new refresh URL back into persistent storage
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

                                    // Only use the token for 80% of its promised life
                                    spotifyApiExpiry = DateTime.Now.AddSeconds(token.ExpiresIn * 4 / 5);

                                    //  Now create a SpotifyClient with the authenticated access token
                                    spotifyClientConfig = SpotifyClientConfig.
                                        CreateDefault(token.AccessToken).
                                        WithRetryHandler(new SimpleRetryHandler() { RetryAfter = TimeSpan.FromSeconds(1) });
                                    spotifyService = new SpotifyClient(spotifyClientConfig);

                                    spotifyCurrentUserId = spotifyService.UserProfile.Current().Result.Id.ToString();
                                    logger.Info("Connected and authenticated {0} to Spotify Web API (expires at {1})",
                                        spotifyCurrentUserId, spotifyApiExpiry.ToShortTimeString());
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

                if (spotifyService == null || spotifyApiExpiry <= DateTime.Now)
                {
                    logger.Error("Failed to connect to Spotify Web API");
                }

                if (AllSavedAlbumList == null && spotifyService != null)
                {
                    LoadAndIndexAllSavedTracks();
                }
            }

            return spotifyService;
        }
    }

    public static bool Probe()
    {
        lock (logger)
        {
            if (spotifyService == null || spotifyApiExpiry <= DateTime.Now)
            {
                logger.Info("Probing Authentication API");
                try
                {
                    string requestUri = AuthenticatorUri + "/Probe";
                    using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                    {
                        var response = httpClient.Send(request);
                        response.EnsureSuccessStatusCode();
                        return true;
                    }
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
            lock (spotifyServiceLock)
            {
                trackCache[id] = SpotifyService.Tracks.Get(id).Result;
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
            lock (spotifyServiceLock)
            {
                albumCache[id] = SpotifyService.Albums.Get(id).Result;
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
            lock (spotifyServiceLock)
            {
                artistCache[id] = SpotifyService.Artists.Get(id).Result;
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
        if (spotifyService != null)
        {
            logger.Info("LoadAndIndexAllSavedTracks start");

            AllSavedAlbumList = new List<SpotifyData.Album>(); // prevents reentrancy

            for (var retries = 0; retries < 20; retries++)
            {
                try
                {
                    IAsyncEnumerable<SavedAlbum> pagedAlbums;

                    lock (spotifyServiceLock)
                    {
                        pagedAlbums = SpotifyService.Paginate(SpotifyService.Library.GetAlbums().Result);
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
                    lock (spotifyServiceLock)
                    {
                        batchOfArtists = SpotifyService.Artists.GetSeveral(new ArtistsRequest(batchOfIds)).Result.Artists;
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
        if (SpotifyService != null)
        {
            logger.Info("SearchTracks {0}", name);

            try
            {
                IAsyncEnumerable<FullTrack> tracks;
                lock (spotifyServiceLock)
                {
                    SearchResponse searchResult = SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Track, HttpUtility.UrlEncode(name))).Result;
                    tracks = SpotifyService.Paginate(searchResult.Tracks, (s) => s.Tracks);
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
        if (SpotifyService != null)
        {
            logger.Info("SearchAlbums {0}", name);

            try
            {
                IAsyncEnumerable<SimpleAlbum> albums;
                lock (spotifyServiceLock)
                {
                    SearchResponse searchResult = SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Album, HttpUtility.UrlEncode(name))).Result;
                    albums = SpotifyService.Paginate(searchResult.Albums, (s) => s.Albums);
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
        if (SpotifyService != null)
        {
            logger.Info("SearchArtists {0}", name);

            try
            {
                IAsyncEnumerable<FullArtist> artists;
                lock (spotifyServiceLock)
                {
                    SearchResponse searchResult = SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Artist, HttpUtility.UrlEncode(name))).Result;
                    artists = SpotifyService.Paginate(searchResult.Artists, (s) => s.Artists);
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return MakeArtist(SpotifyService.Artists.Get(SimplifyId(id)).Result);
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
        if (SpotifyService != null)
        {
            try
            {
                var artist = GetArtistById(id);
                ArtistHistory.Add(artist.Name, artist.Id);

                IAsyncEnumerable<SimpleAlbum> albums;
                lock (spotifyServiceLock)
                {
                    albums = SpotifyService.Paginate(SpotifyService.Artists.GetAlbums(SimplifyId(id)).Result);
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Artists.GetRelatedArtists(SimplifyId(id)).Result.Artists.Select(a => MakeArtist(a));
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
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<SimpleTrack> tracks;
                lock (spotifyServiceLock)
                {
                    tracks = SpotifyService.Paginate(SpotifyService.Albums.GetTracks(SimplifyId(id)).Result);
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
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
    public static Dictionary<String, SpotifyData.Playlist> CurrentPlaylistsByName { get; private set; }
    public static Dictionary<String, SpotifyData.Playlist> CurrentPlaylistsById { get; private set; }

    /// <summary>
    /// Get the collection of named playlists, rebuilding from data on Spotify
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Playlist> GetPlayLists()
    {
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<SimplePlaylist> pagingPlaylist;

                lock (spotifyServiceLock)
                {
                    pagingPlaylist = SpotifyService.Paginate(SpotifyService.Playlists.GetUsers(spotifyCurrentUserId).Result);
                }
                var playlists = MakePlaylists(pagingPlaylist);
                CurrentPlaylistsByName = playlists.ToDictionary(p => p.Name);
                CurrentPlaylistsById = playlists.ToDictionary(p => p.Id);
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
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<PlaylistTrack<IPlayableItem>> tracks;
                lock (spotifyServiceLock)
                {
                    tracks = SpotifyService.Paginate(SpotifyService.Playlists.GetItems(SimplifyId(id)).Result);
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
        if (SpotifyService != null)
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Playlists.Create(spotifyCurrentUserId, new PlaylistCreateRequest(name)).Result.Uri;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {

                    var ok = SpotifyService.Follow.UnfollowPlaylist(SimplifyId(id)).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new PlaylistChangeDetailsRequest();
                    request.Name = newName;
                    var ok = SpotifyService.Playlists.ChangeDetails(SimplifyId(id), request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new PlaylistAddItemsRequest(new List<string> { GetFullTrack(trackId).Uri });
                    var ok = SpotifyService.Playlists.AddItems( SimplifyId(playlistId), request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var tracks = GetTracksForAlbum(SimplifyId(albumId));
                    var request = new PlaylistAddItemsRequest(tracks.Select(t => t.Id).ToList());
                    var ok = SpotifyService.Playlists.AddItems(SimplifyId(playlistId), request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new PlaylistRemoveItemsRequest();
                    request.Tracks = new PlaylistRemoveItemsRequest.Item[1];
                    request.Tracks[0].Uri = trackId;
                    var ok = SpotifyService.Playlists.RemoveItems( SimplifyId(playlistId), request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var tracks = GetTracksForAlbum(SimplifyId(albumId));
                    var request = new PlaylistRemoveItemsRequest();
                    request.Tracks = tracks.Select(t => new PlaylistRemoveItemsRequest.Item { Uri = t.Id } ).ToList();;
                    var ok = SpotifyService.Playlists.RemoveItems(SimplifyId(playlistId), request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new LibrarySaveAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var ok = SpotifyService.Library.SaveAlbums(request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new LibraryRemoveAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var ok = SpotifyService.Library.RemoveAlbums(request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new LibraryCheckAlbumsRequest(new List<string> { SimplifyId(albumId) });
                    var saveIndications = SpotifyService.Library.CheckAlbums(request).Result;
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
            var devices = SpotifyService.Player.GetAvailableDevices().Result;
            if (devices != null && devices.Devices != null)
            {
                foreach (var dev in devices.Devices)
                {
                    logger.Info($"Spotify play device found: {dev.Name} [{dev.Type}]");
					if (dev.Type.ToLower() == "avr")
					{
						playbackDevice = dev.Id;
					}
                }

                if (playbackDevice == null)
                {
                    foreach (var dev in devices.Devices)
                    {
						if (dev.Type.ToLower() == "computer" && dev.Name.ToLower() == Environment.MachineName.ToLower())
						{
							logger.Info($"Environment.MachineName: {Environment.MachineName}");
							playbackDevice = dev.Id;
						}
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
        var track = GetTrackById(id);
        logger.Info($"Play track: '{track.Name}' [{track.AlbumArtistName}] (append={append})");

        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    if (append)
                    {
                        var request = new PlayerAddToQueueRequest(id);
                        return SpotifyService.Player.AddToQueue(request).Result;
                    }
                    else
                    {
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        request.Uris = new List<string> { id };
                        request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                        return SpotifyService.Player.ResumePlayback(request).Result;
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
        if (SpotifyService != null)
        {
            try
            {
                if (append)
                {
                    var tracks = GetTracksForAlbum(SimplifyId(id));
                    if (tracks.Any())
                    {
                        logger.Info($"Append album '{tracks.First().AlbumName}' [{tracks.First().AlbumArtistName}]");
                    }
                    lock (spotifyServiceLock)
                    {
                        foreach (var t in tracks)
                        {
                            var request = new PlayerAddToQueueRequest(t.Id);
                            if (!SpotifyService.Player.AddToQueue(request).Result)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
                else
                {
                    lock (spotifyServiceLock)
                    {
                        var album = MakeAlbum(GetFullAlbum(SimplifyId(id)));
                        logger.Info($"Play album '{album.Name}' [{album.ArtistName}]");
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        request.ContextUri = id;
                        request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                        return SpotifyService.Player.ResumePlayback(request).Result;
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
        logger.Info($"Play playlist '{CurrentPlaylistsById[id].Name}'");
        if (SpotifyService != null)
        {
            try
            {
                if (append)
                { 
                    var tracks = GetPlayListTracks(id);
                    lock (spotifyServiceLock)
                    {
                        foreach (var t in tracks)
                        {
                            var request = new PlayerAddToQueueRequest(t.Id);
                            if (!SpotifyService.Player.AddToQueue(request).Result)
                            {
                                return false;
                            }
                            return true;
                        }
                    }
                }
                else
                {
                    lock (spotifyServiceLock)
                    {
                        lock (spotifyServiceLock)
                        {
                            var request = new PlayerResumePlaybackRequest();
                            request.DeviceId = GetPlaybackDevice();
                            request.ContextUri = id;
                            request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                            return SpotifyService.Player.ResumePlayback(request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new PlayerCurrentlyPlayingRequest();
                    var playingTrack = SpotifyService.Player.GetCurrentlyPlaying(request).Result?.Item as FullTrack;
                    return playingTrack == null ? null : MakeTrack(playingTrack);
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
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlayerCurrentPlaybackRequest();
                var playback = SpotifyService.Player.GetCurrentPlayback(request).Result;
                if (playback?.Context != null)
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

                if (playback?.Item != null)
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
        var playback = SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
        var request = new PlayerResumePlaybackRequest();
        request.DeviceId = GetPlaybackDevice();
        request.ContextUri = playback.Context.Uri;
        request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Uri = id };
        var ok = SpotifyService.Player.ResumePlayback(request).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Player.SkipNext().Result ? 0 : -1;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Player.SkipPrevious().Result ? 0 : -1;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var playback = SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
                    if (!playback.IsPlaying && playback.Item != null)
                    {
                        var request = new PlayerResumePlaybackRequest();
                        request.DeviceId = GetPlaybackDevice();
                        return SpotifyService.Player.ResumePlayback(request).Result ? 0 : -1;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Player.PausePlayback().Result ? 0 : -1;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var playback = SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
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
        logger.Info($"Stop");
        if (spotifyService != null)  //  Note the use of webAppService to avoid unnecessarily starting Spotify
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    return SpotifyService.Player.PausePlayback().Result ? 0 : -1;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var playback = SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest()).Result;
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
        if (SpotifyService != null)
        {
            lock (spotifyServiceLock)
            {
                try
                {
                    var request = new PlayerSeekToRequest(pos*1000);
                    return SpotifyService.Player.SeekTo(request).Result ? pos : -1;
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
        var count = albumIds.Count();
        foreach (var batch in albumIds.Batch(20))
        {
            var albumBatch = SpotifyService.Albums.GetSeveral(new AlbumsRequest(batch.ToList())).Result.Albums.Select(MakeAlbum);
            foreach (var album in albumBatch)
            {
                yield return album;
            }
        }
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

    //  This URL (hopefully permanently running) will be used to authenticate Avid5 instances
    public static void Authenticate()
    {
        try
        {
            //  My own web server has an authenticator with the client secret for my developer client ID
            string ClientId = Config.SpotifyClientId;

            var auth = new LoginRequest(new Uri(AuthenticatorUri + "/Authenticate"), ClientId, SpotifyAPI.Web.LoginRequest.ResponseType.Code)
            {
                //How many permissions we need? Ask for the lot!
                Scope = new[] {
                    Scopes.UgcImageUpload,
                    Scopes.UserReadPlaybackState,
                    Scopes.UserModifyPlaybackState,
                    Scopes.UserReadCurrentlyPlaying,
                    Scopes.Streaming,
                    Scopes.AppRemoteControl,
                    Scopes.UserReadEmail,
                    Scopes.UserReadPrivate,
                    Scopes.PlaylistReadCollaborative,
                    Scopes.PlaylistModifyPublic,
                    Scopes.PlaylistReadPrivate,
                    Scopes.PlaylistModifyPrivate,
                    Scopes.UserLibraryModify,
                    Scopes.UserLibraryRead,
                    Scopes.UserTopRead,
                    Scopes.UserReadPlaybackPosition,
                    Scopes.UserReadRecentlyPlayed,
                    Scopes.UserFollowRead,
                    Scopes.UserFollowModify
                }
            };

            //  Start a browser to authenticate
            var url =  auth.ToUri().ToString();
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = url
            };
            System.Diagnostics.Process.Start(psi);

            //  Try for two minutes to get the RefreshToken constructed as part of the OAUTH exchange with the browser
            for (int i = 0; i < 120; i++)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, AuthenticatorUri + "/GetLastRefreshToken"))
                {
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                    using (HttpResponseMessage response = httpClient.Send(request))
                    {
                        response.EnsureSuccessStatusCode();
                        var lastRefreshToken = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                        if (!string.IsNullOrEmpty(lastRefreshToken))
                        {
                            //  Save the required authentication refresh URL so that the 
                            //  Avid5 web app can authenticate using the same credentials
                            Config.SaveValue("SpotifyRefreshUrl", AuthenticatorUri + "/Refresh?refresh_token=" + lastRefreshToken);
                            logger.Info("Authenticated to Spotify Web API");
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
    }
}
