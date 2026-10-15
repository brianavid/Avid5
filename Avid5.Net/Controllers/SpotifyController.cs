
using System.Xml.Linq;
using Avid.Spotify;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Avid5.Net.Controllers
{
    public class SpotifyController : Controller
    {
        // GET: /Spotify/All
        public async Task<ActionResult> All()
        {
            ViewBag.Mode = "Library";

            return View();
        }

        // GET: /Spotify/Playing
        public async Task<ActionResult> Playing()
        {
            return View();
        }

        // GET: /Spotify/Queue
        public async Task<ActionResult> Queue()
        {
            return View();
        }

        // GET: /Spotify/QueuePane
        public async Task<ActionResult> QueuePane()
        {
            return PartialView();
        }

        // GET: /Spotify/Browser
        public async Task<ActionResult> Browser(
            string mode,
            string id,
            string name,
            string query)
        {
            ViewBag.Mode = mode;
            if (id != null)
            {
                ViewBag.Id = id;
            }
            if (name != null)
            {
                ViewBag.Name = name;
            }
            if (query != null)
            {
                ViewBag.Query = query;
            }

            return View();
        }

        // GET: /Spotify/BrowserPane
        public async Task<ActionResult> BrowserPane(
            string mode,
            string id,
            string playlistId,
            string playlistName,
            string query,
            string trackInfoId,
            string albumInfoId,
            string artistInfoId,
            string append)
        {
            ViewBag.Mode = mode;
            if (id != null)
            {
                ViewBag.Id = id;
            }
            if (playlistId != null)
            {
                ViewBag.PlaylistId = playlistId;
            }
            if (playlistName != null)
            {
                ViewBag.PlaylistName = playlistName;
            }
            if (query != null)
            {
                ViewBag.Query = query;
            }
            if (trackInfoId != null)
            {
                ViewBag.TrackId = trackInfoId;
            }
            if (albumInfoId != null)
            {
                ViewBag.AlbumId = albumInfoId;
            }
            if (artistInfoId != null)
            {
                ViewBag.ArtistId = artistInfoId;
            }
            if (append != null)
            {
                ViewBag.Append = append;
            }

            return PartialView();
        }

        // GET: /Spotify/GetPlayingInfo
        public async Task<ContentResult> GetPlayingInfo()
        {
            SpotifyData.Track currentTrack = await Spotify.GetCurrentTrackAsync();

            if (currentTrack == null)
            {
                XElement stoppedInfo = new XElement("Track",
                    new XAttribute("id", ""),
                    new XAttribute("name", ""),
                    new XAttribute("album", ""),
                    new XAttribute("albumid", ""),
                    new XAttribute("albumArtist", ""),
                    new XAttribute("trackArtists", ""),
                    new XAttribute("duration", 0),
                    new XAttribute("position", 0),
                    new XAttribute("status", "Stopped"),
                    new XAttribute("postionDisplay", ""),
                    new XAttribute("indexDisplay", ""));

                return this.Content(stoppedInfo.ToString(), @"text/xml", Encoding.UTF8);
            }

            int pos = await Spotify.GetPositionAsync();
            int playStatus = await Spotify.GetPlayingAsync();

            var queuedTracks = await Spotify.GetQueuedTracksAsync();
            var queuedTracksArray = queuedTracks == null ? new SpotifyData.Track[0] : queuedTracks.ToArray();
            int foundIndexInQueue = Array.FindIndex(queuedTracksArray, t => t.Id == currentTrack.Id);
            string indexDisplay = (foundIndexInQueue >= 0) ?
                foundIndexInQueue + 1 + " / " + queuedTracksArray.Length :
                "[ " + (currentTrack.Index + 1) + " / " + currentTrack.Count + " ]";

            XElement info = new XElement("Track",
                new XAttribute("id", currentTrack.Id),
                new XAttribute("name", currentTrack.Name),
                new XAttribute("album", currentTrack.AlbumName),
                new XAttribute("albumid", currentTrack.AlbumId),
                new XAttribute("albumArtist", currentTrack.AlbumArtistName),
                new XAttribute("trackArtists", currentTrack.TrackArtistNames),
                new XAttribute("duration", currentTrack.Duration),
                new XAttribute("position", pos),
                new XAttribute("status", playStatus == -1 ? "Stolen" : playStatus == 0 ? "Paused" : "Playing"),
                new XAttribute("postionDisplay", Spotify.FormatDuration(pos) + "/" + Spotify.FormatDuration(currentTrack.Duration)),
                new XAttribute("indexDisplay", indexDisplay));

            return this.Content(info.ToString(), @"text/xml", Encoding.UTF8);
        }

        // GET: /Spotify/PlayAlbum
        public async Task<ContentResult> PlayAlbum(
            string id,
            bool append = false)
        {
            await Spotify.PlayAlbumAsync(id, append);
            return this.Content("");
        }

        // GET: /Spotify/PlayTrack
        public async Task<ContentResult> PlayTrack(
            string id,
            bool append = false)
        {
            await Spotify.PlayTrackAsync(id, append);
            return this.Content("");
        }

        // GET: /Spotify/PlayPlaylist
        public async Task<ContentResult> PlayPlaylist(
            string id,
            bool append = false)
        {
            await Spotify.PlayPlaylistAsync(id, append);
            return this.Content("");
        }

        // GET: /Spotify/SkipToQueuedTrack
        public async Task<ContentResult> SkipToQueuedTrack(
            string id)
        {
            await Spotify.SkipToQueuedTrackAsync(id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveQueuedTrack
        public async Task<ContentResult> RemoveQueuedTrack(
            string id)
        {
            await Spotify.RemoveQueuedTrack(id);
            return this.Content("");
        }

        // GET: /Spotify/PlayPause
        public async Task<ContentResult> PlayPause()
        {
            if (await Spotify.GetPlayingAsync() <= 0)
            {
                await Spotify.PlayAsync();
            }
            else
            {
                await Spotify.PauseAsync();
            }
            return this.Content("");
        }

        // GET: /Spotify/Skip
        public async Task<ContentResult> Skip()
        {
            await Spotify.SkipAsync();
            return this.Content("");
        }

        // GET: /Spotify/Back
        public async Task<ContentResult> Back()
        {
            await Spotify.BackAsync();
            return this.Content("");
        }

        // GET: /Spotify/Plus10
        public async Task<ContentResult> Plus10()
        {
            await Spotify.SetPositionAsync(await Spotify.GetPositionAsync() + 10);
            return this.Content("");
        }

        // GET: /Spotify/Minus10
        public async Task<ContentResult> Minus10()
        {
            int pos = await Spotify.GetPositionAsync();
            await Spotify.SetPositionAsync(pos < 10 ? 0 : pos - 10);
            return this.Content("");
        }

        // GET: /Spotify/SetPosition
        public async Task<ContentResult> SetPosition(
            int pos)
        {
            await Spotify.SetPositionAsync(pos);
            return this.Content("");
        }

        // GET: /Spotify/GetAlbumImage
        public async Task<ActionResult> GetAlbumImage(
            string id)
        {
            try
            {
                var requestUri = await Spotify.GetAlbumImageUrlAsync(id);
                //make the sync GET request
                using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
                {
                    var response = JRMC.httpClient.Send(request);
                    response.EnsureSuccessStatusCode();
                    byte[] bytes = response.Content.ReadAsByteArrayAsync().Result;
                    return base.File(bytes, response.Content.Headers.ContentType.MediaType);
                }
            }
            catch
            {
            }

            return this.Content("");
        }

        // GET: /Spotify/AddTrackToPlayList
        public async Task<ContentResult> AddTrackToPlaylist(
            string id,
            string playlistId,
            string playlistName)
        {
            if (playlistId == null && Spotify.CurrentPlaylistsByName.ContainsKey(playlistName))
            {
                playlistId = Spotify.CurrentPlaylistsByName[playlistName].Id;
            }
            if (playlistId == null)
            {
                playlistId = await Spotify.AddPlayListAsync(playlistName);
            }
            await Spotify.AddTrackToPlayListAsync(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/AddAlbumToPlayList
        public async Task<ContentResult> AddAlbumToPlayList(
            string id,
            string playlistId,
            string playlistName)
        {
            if (playlistId == null && !string.IsNullOrEmpty(playlistName) && Spotify.CurrentPlaylistsByName.ContainsKey(playlistName))
            {
                playlistId = Spotify.CurrentPlaylistsByName[playlistName].Id;
            }
            if (playlistId == null && !string.IsNullOrEmpty(playlistName))
            {
                playlistId = await Spotify.AddPlayListAsync(playlistName);
            }
            if (playlistId != null)
            {
                await Spotify.AddAlbumToPlayListAsync(playlistId, id);
            }
            return this.Content("");
        }


        // GET: /Spotify/RemoveTrackFromPlayList
        public async Task<ContentResult> RemoveTrackFromPlayList(
            string id,
            string playlistId)
        {
            await Spotify.RemoveTrackFromPlayListAsync(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveAlbumFromPlayList
        public async Task<ContentResult> RemoveAlbumFromPlayList(
            string id,
            string playlistId)
        {
            await Spotify.RemoveAlbumFromPlayListAsync(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/DeletePlaylist
        public async Task<ContentResult> DeletePlaylist(
            string playlistId)
        {
            await Spotify.DeletePlayListAsync(playlistId);
            return this.Content("");
        }

        // GET: /Spotify/RenamePlaylist
        public async Task<ContentResult> RenamePlaylist(
            string playlistId,
            string name)
        {
            await Spotify.RenamePlayListAsync(playlistId, name);
            return this.Content("");
        }

        // GET: /Spotify/SaveAlbum
        public async Task<ContentResult> AddSavedAlbum(
            string id)
        {
            await Spotify.AddSavedAlbumAsync(id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveSavedAlbum
        public async Task<ContentResult> RemoveSavedAlbum(
            string id)
        {
            await Spotify.RemoveSavedAlbumAsync(id);
            return this.Content("");
        }

        // GET: /Spotify/GetAuthenticationUrl
        public async Task<ContentResult> GetAuthenticationUrl()
        {
            return this.Content(Spotify.GetAuthenticationUrl());
        }

        // GET: /Spotify/WaitForAuthentication
        public async Task<ContentResult> WaitForAuthentication()
        {
            await Spotify.WaitForAuthenticationAsync();
            return this.Content("");
        }
    }
}
