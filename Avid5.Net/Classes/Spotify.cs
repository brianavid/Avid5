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
using System.Runtime.InteropServices.Marshalling;

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
    static SpotifyClientConfig spotifyClientConfig;

    static string playbackDevice = null;

    static System.Collections.Concurrent.ConcurrentDictionary<String, FullArtist> artistCache = new System.Collections.Concurrent.ConcurrentDictionary<String, FullArtist>();
    static System.Collections.Concurrent.ConcurrentDictionary<String, FullAlbum> albumCache = new System.Collections.Concurrent.ConcurrentDictionary<String, FullAlbum>();
    static System.Collections.Concurrent.ConcurrentDictionary<String, FullTrack> trackCache = new System.Collections.Concurrent.ConcurrentDictionary<String, FullTrack>();

    static SpotifyData.Album[] AllSavedAlbums;

    static string PreferredMarket = Config.SpotifyMarket ?? "GB";

    public static bool HasAuthenticated
    {
        get { return !string.IsNullOrEmpty(Config.ReadValue("SpotifyRefreshUrl")); }
    }

    /// <summary>
    /// Initialize and memoize the web API service using the authentication token stored in the registry
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
            }

            if (spotifyCurrentUserId == null && spotifyService != null)
            {
                var user = spotifyService.UserProfile.Current().Result;
                spotifyCurrentUserId = user.Id;
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

    static async Task<FullTrack> GetFullTrackAsync(
        string id)
    {
        if (id == null) return null;

        if (!trackCache.ContainsKey(id))
        {
            trackCache[id] = await SpotifyService.Tracks.Get(id);
        }
        return trackCache[id];
    }

    static async Task<FullAlbum> GetFullAlbumAsync(
        string id)
    {
        if (id == null) return null;

        if (!albumCache.ContainsKey(id))
        {
            albumCache[id] = await SpotifyService.Albums.Get(id);
        }
        return albumCache[id];
    }

    static async Task<FullArtist> GetFullArtistAsync(
        string id)
    {
        if (id == null) return null;

        if (!artistCache.ContainsKey(id))
        {
            artistCache[id] = await SpotifyService.Artists.Get(id);
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
    /// Format a track's duration for display
    /// </summary>
    /// <param name="rawDuration"></param>
    /// <returns></returns>
    public static string FormatDuration(int seconds) => seconds < 0 ? "<0:00" : string.Format("{0}:{1:00}", seconds / 60, seconds % 60);

    public static void Initialize() => Probe();

    #region Browsing and Searching
    /// <summary>
    /// Search Spotify for tracks matching the specified track name
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static async Task<IEnumerable<SpotifyData.Track>> SearchTracksAsync(
        string name)
    {
        if (SpotifyService != null)
        {
            logger.Info("SearchTracksAsync {0}", name);

            try
            {
                IAsyncEnumerable<FullTrack> tracks;
                SearchResponse searchResult = await SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Track, HttpUtility.UrlEncode(name)));
                tracks = SpotifyService.Paginate(searchResult.Tracks, (s) => s.Tracks);
                return await MakeTracksAsync(tracks);
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
    public static async Task<IEnumerable<SpotifyData.Album>> SearchAlbumsAsync(
        string name)
    {
        if (SpotifyService != null)
        {
            logger.Info("SearchAlbumsAsync {0}", name);

            try
            {
                IAsyncEnumerable<SimpleAlbum> albums;
                SearchResponse searchResult = await SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Album, HttpUtility.UrlEncode(name)));
                albums = SpotifyService.Paginate(searchResult.Albums, (s) => s.Albums);
                return await MakeAlbumsAsync(albums);
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
    public static async Task<IEnumerable<SpotifyData.Artist>> SearchArtistsAsync(
        string name)
    {
        if (SpotifyService != null)
        {
            logger.Info("SearchArtistsAsync {0}", name);

            try
            {
                IAsyncEnumerable<FullArtist> artists;
                SearchResponse searchResult = await SpotifyService.Search.Item(new SearchRequest(SearchRequest.Types.Artist, HttpUtility.UrlEncode(name)));
                artists = SpotifyService.Paginate(searchResult.Artists, (s) => s.Artists);
                return await MakeArtistsAsync(artists);

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
    public static async Task<SpotifyData.Track> GetTrackByIdAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                return await MakeTrackAsync(await GetFullTrackAsync(SimplifyId(id)));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Return album data for an identified album
    /// </summary>
    /// <param name="id">The Spotify Album URI</param>
    /// <returns></returns>
    public static async Task<SpotifyData.Album> GetAlbumByIdAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                return MakeAlbum(await GetFullAlbumAsync(SimplifyId(id)));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Return artist data for an identified artist
    /// </summary>
    /// <param name="id">The Spotify Artist URI</param>
    /// <returns></returns>
    public static async Task<SpotifyData.Artist> GetArtistByIdAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                return MakeArtist(await SpotifyService.Artists.Get(SimplifyId(id)));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
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
    public static async Task<IEnumerable<SpotifyData.Album>> GetAlbumsForArtistAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                var artist = await GetArtistByIdAsync(id);
                ArtistHistory.Add(artist.Name, artist.Id);

                // Await the first page response without locking
                var pagedAlbums = await SpotifyService.Artists.GetAlbums(SimplifyId(id));

                // Paginate() creates the IAsyncEnumerable stream
                var albumsStream = SpotifyService.Paginate(pagedAlbums);

                // Await the materialization of the list
                return await MakeAlbumsAsync(albumsStream);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return Enumerable.Empty<SpotifyData.Album>();
    }

    /// <summary>
    /// Get the collection of similar artists for an identified artist
    /// </summary>
    /// <param name="id">The Spotify Artist URI</param>
    /// <returns></returns>
    public static async Task<IEnumerable<SpotifyData.Artist>> GetSimilarArtistsForArtistAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                return (await SpotifyService.Artists.GetRelatedArtists(SimplifyId(id))).Artists.Select(a => MakeArtist(a));
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return new List<SpotifyData.Artist>();
    }

    /// <summary>
    /// Get the collection of recently searched artists
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<SpotifyData.Artist> GetHistoryArtists() => ArtistHistory.All.Select(h => MakeArtist(h));

    /// <summary>
    /// Get the collection of tracks for an identified album
    /// </summary>
    /// <param name="id">The Spotify Album URI</param>
    /// <returns></returns>
    public static async Task<IEnumerable<SpotifyData.Track>> GetTracksForAlbumAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<SimpleTrack> tracks;
                tracks = SpotifyService.Paginate(await SpotifyService.Albums.GetTracks(SimplifyId(id)));
                return await MakeTracksAsync(
                        tracks,
                        await GetFullAlbumAsync(SimplifyId(id)));
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
    public static async Task<String> GetAlbumImageUrlAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                var a = await GetFullAlbumAsync(SimplifyId(id));
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
    public static async Task<IEnumerable<SpotifyData.Playlist>> GetPlayListsAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<FullPlaylist> pagingPlaylist;

                pagingPlaylist = SpotifyService.Paginate(await SpotifyService.Playlists.CurrentUsers());
                var playlists = await MakePlaylistsAsync(pagingPlaylist);
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
    public static async Task<IEnumerable<SpotifyData.Track>> GetPlayListTracksAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                IAsyncEnumerable<PlaylistTrack<IPlayableItem>> tracks;
                tracks = SpotifyService.Paginate(await SpotifyService.Playlists.GetPlaylistItems(SimplifyId(id)));
                return await MakeTracksAsync(tracks.Where(t => t.Track.Type == ItemType.Track).Select(t => t.Track as FullTrack), null);
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
    public static async Task<IEnumerable<SpotifyData.Album>> GetPlayListAlbumsAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {
                IEnumerable<SpotifyData.Track> tracks = await GetPlayListTracksAsync(id);
                HashSet<String> albumIds = new HashSet<String>();
                foreach (var track in tracks)
                {
                    if (!albumIds.Contains(track.AlbumId))
                    {
                        albumIds.Add(track.AlbumId);
                    }
                }

                var albums = new List<SpotifyData.Album>();
                foreach (var a in albumIds)
                {
                    albums.Add(MakeAlbum(await GetFullAlbumAsync(SimplifyId(a))));
                }
                return albums;
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
    public static async Task<string> AddPlayListAsync(
        string name)
    {
        if (SpotifyService != null)
        {
            try
            {
                return (await SpotifyService.Playlists.Create(new PlaylistCreateRequest(name))).Uri;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return null;
    }

    /// <summary>
    /// Delete an identified playlist (just unfollows - does not actually delete)
    /// </summary>
    /// <param name="id"></param>
    public static async Task DeletePlayListAsync(
        string id)
    {
        if (SpotifyService != null)
        {
            try
            {

                var request = new LibraryRemoveItemsRequest(new List<string> { SimplifyId(id) });
                var ok = await SpotifyService.Library.RemoveItems(request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Rename an identified playlist
    /// </summary>
    /// <param name="id"></param>
    /// <param name="newName"></param>
    public static async Task RenamePlayListAsync(
        string id,
        string newName)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlaylistChangeDetailsRequest();
                request.Name = newName;
                var ok = await SpotifyService.Playlists.ChangeDetails(SimplifyId(id), request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Add an identified track to an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="trackId"></param>
    public static async Task AddTrackToPlayListAsync(
        string playlistId,
        string trackId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlaylistAddItemsRequest(new List<string> { (await GetFullTrackAsync(trackId)).Uri });
                var ok = await SpotifyService.Playlists.AddPlaylistItems( SimplifyId(playlistId), request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Add all the tracks of an identified album to an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="albumId"></param>
    public static async Task AddAlbumToPlayListAsync(
        string playlistId,
        string albumId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var tracks = await GetTracksForAlbumAsync(SimplifyId(albumId));
                var request = new PlaylistAddItemsRequest(tracks.Select(t => t.Id).ToList());
                var ok = await SpotifyService.Playlists.AddPlaylistItems(SimplifyId(playlistId), request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Remove an identified track from an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="trackId"></param>
    public static async Task RemoveTrackFromPlayListAsync(
        string playlistId,
        string trackId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlaylistRemoveItemsRequestV2();
                request.Items = new PlaylistRemoveItemsRequestV2.Item[1];
                request.Items[0].Uri = trackId;
                var ok = await SpotifyService.Playlists.RemovePlaylistItems( SimplifyId(playlistId), request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Remove all the tracks of an identified album from an identified playlist
    /// </summary>
    /// <param name="playlistId"></param>
    /// <param name="albumId"></param>
    public static async Task RemoveAlbumFromPlayListAsync(
        string playlistId,
        string albumId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var tracks = await GetTracksForAlbumAsync(SimplifyId(albumId));
                var request = new PlaylistRemoveItemsRequestV2();
                request.Items = tracks.Select(t => new PlaylistRemoveItemsRequestV2.Item { Uri = t.Id } ).ToList();;
                var ok = await SpotifyService.Playlists.RemovePlaylistItems(SimplifyId(playlistId), request);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Get the collection of albums saved by the current user
    /// </summary>
    /// <returns></returns>
    public static async Task<IEnumerable<SpotifyData.Album>> GetSavedAlbumsAsync()
    {
        logger.Info("Get Saved Albums");

        if (spotifyService != null && AllSavedAlbums == null)
        {
            for (var retries = 0; retries < 20; retries++)
            {
                try
                {
                    IAsyncEnumerable<SavedAlbum> pagedAlbums;

                    pagedAlbums = SpotifyService.Paginate(await SpotifyService.Library.GetAlbums());
                    AllSavedAlbums = (await MakeAlbumsAsync(pagedAlbums)).ToArray();
                    break;
                }
                catch (System.Exception ex)
                {
                    logger.Error(ex);
                }
            }

            Array.Sort(AllSavedAlbums, CompareAlbumByArtist);
        }

        return AllSavedAlbums;
    }

    /// <summary>
    /// Force re-loading of saved album array
    /// </summary>
    public static void ClearSavedAlbums()
    {
        AllSavedAlbums = null;
    }

    /// <summary>
    /// Save the  identified album as a saved album
    /// </summary>
    /// <param name="albumId"></param>
    public static async Task AddSavedAlbumAsync(
        string albumId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new LibrarySaveItemsRequest(new List<string> { SimplifyId(albumId) });
                var ok = await SpotifyService.Library.SaveItems(request);
                AllSavedAlbums = null;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Remove the identified album as a saved album
    /// </summary>
    /// <param name="albumId"></param>
    public static async Task RemoveSavedAlbumAsync(
        string albumId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new LibraryRemoveItemsRequest(new List<string> { SimplifyId(albumId) });
                var ok = await SpotifyService.Library.RemoveItems(request);
                AllSavedAlbums = null;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
    }

    /// <summary>
    /// Determine if all the identified album is saved
    /// </summary>
    /// <param name="albumId"></param>
    public static async Task<Boolean> IsSavedAlbumAsync(
        string albumId)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new LibraryCheckItemsRequest(new List<string> { SimplifyId(albumId) });
                var saveIndications = await SpotifyService.Library.CheckItems(request);
                return saveIndications.First();
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }

        return false;
    }

    #endregion

    #region Player Queue Management

    static async Task<string> GetPlaybackDeviceAsync()
    {
        if (playbackDevice == null)
        {
            var devices = await SpotifyService.Player.GetAvailableDevices();
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
    public static async Task<Boolean> PlayTrackAsync(
        string id,
        bool append = false)
    {
        var track = await GetTrackByIdAsync(id);
        logger.Info($"PlayAsync track: '{track.Name}' [{track.AlbumArtistName}] (append={append})");

        if (SpotifyService != null)
        {
            try
            {
                if (append)
                {
                    var request = new PlayerAddToQueueRequest(id);
                    return await SpotifyService.Player.AddToQueue(request);
                }
                else
                {
                    var request = new PlayerResumePlaybackRequest();
                    request.DeviceId = await GetPlaybackDeviceAsync();
                    request.Uris = new List<string> { id };
                    request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                    return await SpotifyService.Player.ResumePlayback(request);
                }
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
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
    public static async Task<Boolean> PlayAlbumAsync(
        string id,
        bool append = false)
    {
        if (SpotifyService != null)
        {
            try
            {
                if (append)
                {
                    var tracks = await GetTracksForAlbumAsync(SimplifyId(id));
                    if (tracks.Any())
                    {
                        logger.Info($"Append album '{tracks.First().AlbumName}' [{tracks.First().AlbumArtistName}]");
                    }
                    foreach (var t in tracks)
                    {
                        var request = new PlayerAddToQueueRequest(t.Id);
                        await SpotifyService.Player.AddToQueue(request);
                    }
                    return true;
                }
                else
                {
                    var album = MakeAlbum(await GetFullAlbumAsync(SimplifyId(id)));
                    logger.Info($"PlayAsync album '{album.Name}' [{album.ArtistName}]");
                    var request = new PlayerResumePlaybackRequest();
                    request.DeviceId = await GetPlaybackDeviceAsync();
                    request.ContextUri = id;
                    request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                    return await SpotifyService.Player.ResumePlayback(request);
                }
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
    public static async Task<Boolean> PlayPlaylistAsync(
        string id,
        bool append = false)
    {
        logger.Info($"PlayAsync playlist '{CurrentPlaylistsById[id].Name}'");
        if (SpotifyService != null)
        {
            try
            {
                if (append)
                { 
                    var tracks = await GetPlayListTracksAsync(id);
                    foreach (var t in tracks)
                    {
                        var request = new PlayerAddToQueueRequest(t.Id);
                        await SpotifyService.Player.AddToQueue(request);
                    }
                }
                else
                {
                    var request = new PlayerResumePlaybackRequest();
                    request.DeviceId = await GetPlaybackDeviceAsync();
                    request.ContextUri = id;
                    request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Position = 0 };
                    return await SpotifyService.Player.ResumePlayback(request);
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
    public static async Task<SpotifyData.Track> GetCurrentTrackAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlayerCurrentlyPlayingRequest();
                var playingTrack = (await SpotifyService.Player.GetCurrentlyPlaying(request))?.Item as FullTrack;
                return playingTrack == null ? null : await MakeTrackAsync(playingTrack);
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
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
    public static async Task<IEnumerable<SpotifyData.Track>> GetQueuedTracksAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlayerCurrentPlaybackRequest();
                var playback = await SpotifyService.Player.GetCurrentPlayback(request);
                if (playback?.Context != null)
                {
                    if (playback.Context.Type == "album")
                    {
                        return await GetTracksForAlbumAsync(SimplifyId(playback.Context.Uri));
                    }
                    if (playback.Context.Type == "playlist")
                    {
                        return await GetPlayListTracksAsync(SimplifyId(playback.Context.Uri));
                    }
                }

                if (playback?.Item != null)
                {
                    return new List<SpotifyData.Track> { await MakeTrackAsync(await GetFullTrackAsync(SimplifyId((playback.Item as FullTrack).Uri))) };
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
    public static async Task<SpotifyData.Track> SkipToQueuedTrackAsync(
        string id)
    {
        var playback = await SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest());
        var request = new PlayerResumePlaybackRequest();
        request.DeviceId = await GetPlaybackDeviceAsync();
        request.ContextUri = playback.Context.Uri;
        request.OffsetParam = new PlayerResumePlaybackRequest.Offset { Uri = id };
        var ok = await SpotifyService.Player.ResumePlayback(request);
        return await GetCurrentTrackAsync();
    }

    /// <summary>
    /// Remove the specified queued track from the queue
    /// </summary>
    public static Task<SpotifyData.Track> RemoveQueuedTrack(
        string id) => GetCurrentTrackAsync();
    #endregion

    #region Player currently playing track operations
    /// <summary>
    /// Skip playing forwards to the next queued track
    /// </summary>
    /// <returns></returns>
    public static async Task<int> SkipAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                return await SpotifyService.Player.SkipNext() ? 0 : -1;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Skip playing backwards to the previous queued track
    /// </summary>
    /// <returns></returns>
    public static async Task<int> BackAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                return await SpotifyService.Player.SkipPrevious() ? 0 : -1;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Start or continue playing the current track
    /// </summary>
    /// <returns></returns>
    public static async Task<int> PlayAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                var playback = await SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest());
                if (!playback.IsPlaying && playback.Item != null)
                {
                    var request = new PlayerResumePlaybackRequest();
                    request.DeviceId = await GetPlaybackDeviceAsync();
                    return await SpotifyService.Player.ResumePlayback(request) ? 0 : -1;
                }
                return 0;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Pause playing the current track
    /// </summary>
    /// <returns></returns>
    public static async Task<int> PauseAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                return await SpotifyService.Player.PausePlayback() ? 0 : -1;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Is the player playing a track?
    /// </summary>
    /// <returns>+ve: Playing; 0: Paused; -ve: Stolen by another session</returns>
    public static async Task<int> GetPlayingAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                var playback = await SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest());
                return playback == null ? -1 : playback.IsPlaying ? 1 : 0;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Stop playing the current track
    /// </summary>
    /// <returns></returns>
    public static async Task<int> StopAsync()
    {
        logger.Info($"StopAsync");
        if (spotifyService != null)  //  Note the use of webAppService to avoid unnecessarily starting Spotify
        {
            try
            {
                if (await GetPlayingAsync() > 0)
                {
                    return await SpotifyService.Player.PausePlayback() ? 0 : -1;
                }
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Get the position at which the current track is playing
    /// </summary>
    /// <returns>Position in seconds</returns>
    public static async Task<int> GetPositionAsync()
    {
        if (SpotifyService != null)
        {
            try
            {
                var playback = await SpotifyService.Player.GetCurrentPlayback(new PlayerCurrentPlaybackRequest());
                return playback == null ? -1 : playback.ProgressMs/1000;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    /// <summary>
    /// Seek to a particular position within the currently playing track
    /// </summary>
    /// <param name="pos">Position in seconds</param>
    /// <returns></returns>
    public static async Task<int> SetPositionAsync(
        int pos)
    {
        if (SpotifyService != null)
        {
            try
            {
                var request = new PlayerSeekToRequest(pos*1000);
                return await SpotifyService.Player.SeekTo(request) ? pos : -1;
            }
            catch (System.Exception ex)
            {
                logger.Error(ex);
            }
        }
        return -1;
    }

    public static async Task  ExitPlayer() => await StopAsync();
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
    static SpotifyData.Artist MakeArtist(SimpleArtist artist) => artist == null ? null : new SpotifyData.Artist
    {
        Id = artist.Uri,
        Name = artist.Name
    };

    /// <summary>
    /// Make an external Artist structure from that returned by the Web API
    /// </summary>
    /// <param name="artist"></param>
    /// <returns></returns>
    static SpotifyData.Artist MakeArtist(FullArtist artist) => new SpotifyData.Artist
    {
        Id = artist.Uri,
        Name = artist.Name
    };

    /// <summary>
    /// Make an external Artist structure from that int the Artists History
    /// </summary>
    /// <param name="artist"></param>
    /// <returns></returns>
    static SpotifyData.Artist MakeArtist(ArtistHistory artist) => new SpotifyData.Artist
    {
        Id = artist.Id,
        Name = artist.Name
    };

    /// <summary>
    /// Make a collection of external Artist structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static async Task<IEnumerable<SpotifyData.Artist>> MakeArtistsAsync(
    IAsyncEnumerable<FullArtist> col)
    {
        var artists = new List<SpotifyData.Artist>();

        // Consume the Spotify stream asynchronously
        await foreach (var fullArtist in col.Take(200))
        {
            artists.Add(MakeArtist(fullArtist));
        }
        return artists;
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
    /// Get the year of release for an album
    /// </summary>
    /// <param name="album"></param>
    /// <returns></returns>
    static string ReleaseYear(SimpleAlbum album)
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
    static SpotifyData.Album MakeAlbum(FullAlbum album) => new SpotifyData.Album
    {
        Id = album.Uri,
        Name = album.Name,
        ArtistId = album.Artists[0].Uri,
        ArtistName = album.Artists[0].Name,
        Year = ReleaseYear(album),
        TrackCount = album.TotalTracks
    };

    /// <summary>
    /// Make an external Album structure from that returned by the Web API
    /// </summary>
    /// <param name="album"></param>
    /// <returns></returns>
    static SpotifyData.Album MakeAlbum(SimpleAlbum album) => new SpotifyData.Album
    {
        Id = album.Uri,
        Name = album.Name,
        ArtistId = album.Artists[0].Uri,
        ArtistName = album.Artists[0].Name,
        Year = ReleaseYear(album),
        TrackCount = album.TotalTracks
    };

    /// <summary>
    /// Make a collection of external Album structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static async Task<IEnumerable<SpotifyData.Album>> MakeAlbumsAsync<T>(
        IAsyncEnumerable<T> col,
        Func<T, string> GetAlbumId)
    {
        var albums = new List<SpotifyData.Album>();

        // Consume the Spotify stream asynchronously
        await foreach (var item in col.Take(200))
        {
            string albumId = GetAlbumId(item);

            // Await the fetch for each individual full album
            var fullAlbum = await SpotifyService.Albums.Get(SimplifyId(albumId));
            albums.Add(MakeAlbum(fullAlbum));
        }
        return albums;
    }

    // Update the parameter-less overload to match
    static async Task<IEnumerable<SpotifyData.Album>> MakeAlbumsAsync(
        IAsyncEnumerable<SimpleAlbum> col) => await MakeAlbumsAsync(col, a => a.Id);

    // Update the parameter-less overload to match
    static async Task<IEnumerable<SpotifyData.Album>> MakeAlbumsAsync(
        IAsyncEnumerable<SavedAlbum> col) => await MakeAlbumsAsync(col, a => a.Album.Id);

    /// Make an external Track structure from that returned by the Web API
    /// </summary>
    /// <param name="track"></param>
    /// <param name="album"></param>
    /// <returns></returns>
    /// <summary>
    static async Task<SpotifyData.Track> MakeTrackAsync(FullTrack track, FullAlbum album = null)
    {
        if (album == null && track != null && track.Album != null)
        {
            album = await GetFullAlbumAsync(track.Album.Id);
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
    static async Task<IEnumerable<SpotifyData.Track>> MakeTracksAsync(
        IAsyncEnumerable<FullTrack> col,
        FullAlbum album = null)
    {
        var tracks = new List<SpotifyData.Track>();

        await foreach (var t in col.Take(200))
        {
            // Assuming MakeTrackAsync is now an async method
            var track = await MakeTrackAsync(t, album);
            if (track != null)
            {
                tracks.Add(track);
            }
        }
        return tracks;
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API
    /// </summary>
    static async Task<IEnumerable<SpotifyData.Track>> MakeTracksAsync(
        IAsyncEnumerable<FullTrack> col) => await MakeTracksAsync(col, null);

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API
    /// </summary>
    static async Task<IEnumerable<SpotifyData.Track>> MakeTracksAsync(
        IAsyncEnumerable<SimpleTrack> col,
        FullAlbum album)
    {
        var tracks = new List<SpotifyData.Track>();

        // Apply Take(200) here to prevent fetching unnecessary FullTracks
        await foreach (var t in col.Take(200))
        {
            var fullTrack = await GetFullTrackAsync(t.Id);
            var track = await MakeTrackAsync(fullTrack, album);
            if (track != null)
            {
                tracks.Add(track);
            }
        }
        return tracks;
    }

    /// <summary>
    /// Make a collection of external Track structures from Paging data returned by the Web API as a Playlist
    /// </summary>
    static async Task<IEnumerable<SpotifyData.Track>> MakeTracksAsync(
        IAsyncEnumerable<PlaylistTrack<FullTrack>> col)
    {
        var tracks = new List<SpotifyData.Track>();

        await foreach (var p in col.Take(200))
        {
            var track = await MakeTrackAsync(p.Track, null);
            if (track != null)
            {
                tracks.Add(track);
            }
        }
        return tracks;
    }    /// <summary>
         /// Make an external Playlist structure from that returned by the Web API
         /// </summary>
         /// <param name="playlist"></param>
         /// <returns></returns>
    static SpotifyData.Playlist MakePlaylist(FullPlaylist playlist) => new SpotifyData.Playlist
    {
        Id = playlist.Uri,
        Name = playlist.Name,
    };

    /// <summary>
    /// Make a collection of external Playlist structures from Paging data returned by the Web API
    /// </summary>
    /// <param name="col"></param>
    /// <param name="ReadNext"></param>
    /// <returns></returns>
    static async Task<IEnumerable<SpotifyData.Playlist>> MakePlaylistsAsync(IAsyncEnumerable<FullPlaylist> col)
    {
        var playlists = new List<SpotifyData.Playlist>();
        await foreach (var p in col.Take(200))
        {
            playlists.Add(MakePlaylist(p));
        }
        return playlists;
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

    public static string GetAuthenticationUrl()
    {
        logger.Info("Get Authentication URL");

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

        //  Get a URL to authenticate
        return auth.ToUri().ToString();
    }

    public static async Task WaitForAuthenticationAsync()
    {
        logger.Info("Wait for Authentication");

        try
        { 
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
                            return;
                        }
                    }
                }
                await Task.Delay(1000);
            }
            logger.Info("Failed to authenticate to Spotify Web API");
        }
        catch (Exception ex)
        {
            logger.Error(ex);
        }
    }
}
