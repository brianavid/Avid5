﻿
using System.Xml.Linq;
using Avid.Spotify;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Avid5.Net.Controllers
{
    public class SpotifyController : Controller
    {
        // GET: /Spotify/All
        public ActionResult All()
        {
            ViewBag.Mode = "Library";

            return View();
        }

        // GET: /Spotify/Playing
        public ActionResult Playing()
        {
            return View();
        }

        // GET: /Spotify/Queue
        public ActionResult Queue()
        {
            return View();
        }

        // GET: /Spotify/QueuePane
        public ActionResult QueuePane()
        {
            return PartialView();
        }

        // GET: /Spotify/Browser
        public ActionResult Browser(
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
        public ActionResult BrowserPane(
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
        public ContentResult GetPlayingInfo()
        {
            SpotifyData.Track currentTrack = Spotify.GetCurrentTrack();

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

            int pos = Spotify.GetPosition();
            int playStatus = Spotify.GetPlaying();

            var queuedTracks = Spotify.GetQueuedTracks();
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
        public ContentResult PlayAlbum(
            string id,
            bool append = false)
        {
            Spotify.PlayAlbum(id, append);
            return this.Content("");
        }

        // GET: /Spotify/PlayTrack
        public ContentResult PlayTrack(
            string id,
            bool append = false)
        {
            Spotify.PlayTrack(id, append);
            return this.Content("");
        }

        // GET: /Spotify/PlayPlaylist
        public ContentResult PlayPlaylist(
            string id,
            bool append = false)
        {
            Spotify.PlayPlaylist(id, append);
            return this.Content("");
        }

        // GET: /Spotify/SkipToQueuedTrack
        public ContentResult SkipToQueuedTrack(
            string id)
        {
            Spotify.SkipToQueuedTrack(id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveQueuedTrack
        public ContentResult RemoveQueuedTrack(
            string id)
        {
            Spotify.RemoveQueuedTrack(id);
            return this.Content("");
        }

        // GET: /Spotify/PlayPause
        public ContentResult PlayPause()
        {
            if (Spotify.GetPlaying() <= 0)
            {
                Spotify.Play();
            }
            else
            {
                Spotify.Pause();
            }
            return this.Content("");
        }

        // GET: /Spotify/Skip
        public ContentResult Skip()
        {
            Spotify.Skip();
            return this.Content("");
        }

        // GET: /Spotify/Back
        public ContentResult Back()
        {
            Spotify.Back();
            return this.Content("");
        }

        // GET: /Spotify/Plus10
        public ContentResult Plus10()
        {
            Spotify.SetPosition(Spotify.GetPosition() + 10);
            return this.Content("");
        }

        // GET: /Spotify/Minus10
        public ContentResult Minus10()
        {
            int pos = Spotify.GetPosition();
            Spotify.SetPosition(pos < 10 ? 0 : pos - 10);
            return this.Content("");
        }

        // GET: /Spotify/SetPosition
        public ContentResult SetPosition(
            int pos)
        {
            Spotify.SetPosition(pos);
            return this.Content("");
        }

        // GET: /Spotify/GetAlbumImage
        public ActionResult GetAlbumImage(
            string id)
        {
            try
            {
                var requestUri = Spotify.GetAlbumImageUrl(id);
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
        public ContentResult AddTrackToPlaylist(
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
                playlistId = Spotify.AddPlayList(playlistName);
            }
            Spotify.AddTrackToPlayList(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/AddAlbumToPlayList
        public ContentResult AddAlbumToPlayList(
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
                playlistId = Spotify.AddPlayList(playlistName);
            }
            if (playlistId != null)
            {
                Spotify.AddAlbumToPlayList(playlistId, id);
            }
            return this.Content("");
        }


        // GET: /Spotify/RemoveTrackFromPlayList
        public ContentResult RemoveTrackFromPlayList(
            string id,
            string playlistId)
        {
            Spotify.RemoveTrackFromPlayList(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveAlbumFromPlayList
        public ContentResult RemoveAlbumFromPlayList(
            string id,
            string playlistId)
        {
            Spotify.RemoveAlbumFromPlayList(playlistId, id);
            return this.Content("");
        }

        // GET: /Spotify/DeletePlaylist
        public ContentResult DeletePlaylist(
            string playlistId)
        {
            Spotify.DeletePlayList(playlistId);
            return this.Content("");
        }

        // GET: /Spotify/RenamePlaylist
        public ContentResult RenamePlaylist(
            string playlistId,
            string name)
        {
            Spotify.RenamePlayList(playlistId, name);
            return this.Content("");
        }

        // GET: /Spotify/SaveAlbum
        public ContentResult AddSavedAlbum(
            string id)
        {
            Spotify.AddSavedAlbum(id);
            return this.Content("");
        }

        // GET: /Spotify/RemoveSavedAlbum
        public ContentResult RemoveSavedAlbum(
            string id)
        {
            Spotify.RemoveSavedAlbum(id);
            return this.Content("");
        }

        // GET: /Spotify/GetAuthenticationUrl
        public ContentResult GetAuthenticationUrl()
        {
            return this.Content(Spotify.GetAuthenticationUrl());
        }

        // GET: /Spotify/WaitForAuthentication
        public ContentResult WaitForAuthentication()
        {
            Spotify.WaitForAuthentication();
            return this.Content("");
        }
    }
}
