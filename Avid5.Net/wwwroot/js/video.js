var PositionMS = 0;
var DurationMS = 0;
var slidingTime = new Date(0);
var posSlider = null;
var lastDisplayUpdate = new Date();

function InitializePositionSlider() {
    $("#videoPosSlider").noUiSlider({
        range: [0, 200]
        , start: 0
        , step: 1
        , handles: 1
        , slide: function () {
            slidingTime = new Date();
            var pos = Math.floor($(this).val());
            PositionMS = pos * DurationMS / 200;
            UpdatePositionDisplay();
            $.ajax({
                url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=" + PositionMS),
                cache: false
            })
        }
    });
}

function updateSlider() {
    var now = new Date();
    if (now.getTime() - slidingTime.getTime() > 5 * 1000 && DurationMS > 0 && PositionMS <= DurationMS) {
        var sliderValue = Math.round((PositionMS * 200) / DurationMS);
        $("#videoPosSlider").val(sliderValue)
    }
}

function UpdatePositionDisplay() {
    var secs = Math.floor(PositionMS / 1000);
    var mins = Math.floor(secs / 60);
    secs = secs % 60;
    var posText = mins + ":" + (secs < 10 ? "0" : "") + secs;
    secs = Math.floor(DurationMS / 1000);
    mins = Math.floor(secs / 60);
    secs = secs % 60;
    var durText = mins + ":" + (secs < 10 ? "0" : "") + secs;
    var posDisplay = document.getElementById("PlaybackInfo.PositionDisplay");
    if (posDisplay != null) {
        $(posDisplay).text(posText + " / " + durText);
    }
}

function UpdateJrmcDisplayPlayingInformation() {
    var now = new Date();
    if (now.getTime() - lastDisplayUpdate.getTime() > 10 * 1000) {
        lastDisplayUpdate = now;
        return;
    }

    lastDisplayUpdate = now;
    if (overlayVisible || !navigator.onLine) {
        return;
    }

    $.ajax({
        type: "GET",
        url: "/Video/GetPlayingInfo",
        timeout: 700,
        cache: false,
        success: function (xml) {
            if (xml != null) {
                var root = xml.getElementsByTagName("Response");

                // see if response is valid
                if ((root != null) && (root.length == 1) && (xml.documentElement.getAttribute("Status") == "OK")) {

                    // get all items
                    var items = xml.getElementsByTagName("Item");
                    if (items != null) {

                        // loop items
                        for (var i = 0; i < items.length; i++) {

                            // parse values
                            var name = items[i].getAttribute("Name");
                            var value = items[i].childNodes.length == 0 ? "" : items[i].childNodes[0].nodeValue;

                            // get corresponding element
                            var element = document.getElementById("PlaybackInfo." + name);
                            if (element != null) {

                                // update element
                                if ((element.src != null) && (element.src != ""))
                                    element.src = JrmcHost + value; // image
                                else
                                    element.innerHTML = value; // text
                            }

                            if (name == "Status" && value == "Stopped") {
                                $("#videoRecordingPlayFromStart").text("Play recording")
                            }

                            if (name == "PositionMS") {
                                PositionMS = parseInt(value);
                                updateSlider();
                            }

                            if (name == "DurationMS") {
                                DurationMS = parseInt(value);
                                updateSlider();
                            }
                        }
                    }
                }
            }
        }
    });
}
function SetRecordingsHeight() {
    $(".videoRecordings").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log(".videoRecordings h=" + h + "; t=" + t + " => " + (h - t))
        $(this).height(h - t)
    });
    $(".videoVideos").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log(".videoDvds h=" + h + "; t=" + t + " => " + (h - t))
        $(this).height(h - t)
    });
    $(".videoDvds").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log(".videoDvds h=" + h + "; t=" + t + " => " + (h - t))
        $(this).height(h - t)
    });
}

function DisplayVideoRecordings() {
    var DvdsDisplay = document.getElementById("videoDvds");
    if (DvdsDisplay != null) {
        ClearStackedPanes()
        $(".videoRecordings").hide()
        $(".videoVideos").hide()
        $(".videoDvds").hide()
        $.ajax({
            url: "/Video/RecordingsPane",
            success: function (data) {
                $(".videoRecordings").html(data)
                $(".videoRecordings").show()
                SetRecordingsHeight();
            },
            cache: false
        });
    }
    else {
        LinkTo("/Video/Recordings");
    }
}

function DisplayVideos() {
    var recordingsDisplay = document.getElementById("videoRecordings");
    if (recordingsDisplay != null) {
        ClearStackedPanes()
        $(".videoRecordings").hide()
        $(".videoVideos").hide()
        $(".videoDvds").hide()
        $.ajax({
            url: "/Video/VideosPane",
            success: function (data) {
                $(".videoVideos").html(data)
                $(".videoVideos").show()
                SetRecordingsHeight()
            },
            cache: false
        });
    }
    else {
        LinkTo("/Video/DVDs");
    }
}

function DisplayDvds() {
    var recordingsDisplay = document.getElementById("videoRecordings");
    if (recordingsDisplay != null) {
        ClearStackedPanes()
        $(".videoRecordings").hide()
        $(".videoVideos").hide()
        $(".videoDvds").hide()
        $.ajax({
            url: "/Video/DVDsPane",
            success: function (data) {
                $(".videoDvds").html(data)
                $(".videoDvds").show()
                SetRecordingsHeight()
            },
            cache: false
        });
    }
    else {
        LinkTo("/Video/DVDs");
    }
}

function DisplayRunningOnWatchPane(jump) {
    var watchDisplay = document.getElementById("videoWatchPane");
    if (watchDisplay != null) {
        ReplacePane("videoWatchPane", "/Video/WatchPane", "none", InitializePositionSlider);
    }
    else if (jump) {
        LinkTo("/Video/Watch");
    }
}

var videoControlHammer = null;

function AddVideoControlsHammerActions() {

    if (!videoControlHammer) {
        videoControlHammer = $(".videoWatchPane").hammer({ prevent_default: true });
    }

    videoControlHammer.on("touch", "#videoBack60", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=60000&Relative=-1"),
            success: function (data) {
                PositionMS -= 60000;
                UpdatePositionDisplay();
            },
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoBack10", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=10000&Relative=-1"),
            success: function (data) {
                PositionMS -= 10000;
                UpdatePositionDisplay();
            },
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoForward10", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=10000&Relative=1"),
            success: function (data) {
                PositionMS += 10000;
                UpdatePositionDisplay();
            },
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoForward60", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=60000&Relative=1"),
            success: function (data) {
                PositionMS += 60000;
                UpdatePositionDisplay();
            },
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoPlayPause", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/PlayPause"),
            cache: false
        })
    });

    videoControlHammer.on("hold", "#videoPlayPause", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Stop"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoStop", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Stop"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoDvdMenu", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/MCC?Command=28016"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoMenuUp", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Key=Up&Focus=1"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoMenuLeft", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Key=Left&Focus=1"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoMenuSelect", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Key=Enter&Focus=1"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoMenuRight", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Key=Right&Focus=1"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoMenuDown", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Key=Down&Focus=1"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoAudioBack", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/MCC?Command=28036&Parameter=-50"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoAudioReset", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/MCC?Command=28036&Parameter=0"),
            cache: false
        })
    });

    videoControlHammer.on("touch", "#videoAudioAdvance", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/MCC?Command=28036&Parameter=50"),
            cache: false
        })
    });

}

var videoRecordingsListHammer = null;

function AddVideoRecordingsHammerActions() {
    SetRecordingsHeight()

    if (!videoRecordingsListHammer) {
        videoRecordingsListHammer = $(".videoRecordings").hammer({ prevent_default: true });
    }

    EnableDragScroll(videoRecordingsListHammer)

    videoRecordingsListHammer.on("swiperight swipeleft", function (e) {
        PopStackedPane("videoRecordings", DisplayVideoRecordings)
        return false;
    })

    videoRecordingsListHammer.on("tap", ".videoRecording", function (e) {
        e.gesture.preventDefault()
        ReplacePane("videoRecordings", "/Video/Recording?id=" + this.id, "push")
    });

    videoRecordingsListHammer.on("tap", ".videoRecordingGroup", function (e) {
        e.gesture.preventDefault()
        ReplacePane("videoRecordings", "/Video/RecordingsPane?series=" + encodeURIComponent(this.id), "push")
    });

    videoRecordingsListHammer.on("tap", "#videoRecordingPlayFromStart", function () {
        $("#videoRecordingPlayFromStart").text("Playing ...")
        $("#videoRecordingConfirmDelete").hide();
       var playUrl = "/Video/PlayRecording?id=" + $("#recordingId").text();
        $.ajax({
            url: playUrl,
            success: function (data) {
                DisplayRunningOnWatchPane(true)
            },
            cache: false
        });
    });

    videoRecordingsListHammer.on("doubletap", "#videoRecordingDelete", function () {
        $("#videoRecordingConfirmDelete").show();
    });


    videoRecordingsListHammer.on("tap", "#videoRecordingConfirmDelete", function () {
        $("#videoRecordingDelete").text("Deleting ...")
        $.ajax({
            url: "/Video/DeleteRecording?id=" + $("#recordingId").text(),
            success: function (data) {
                DisplayVideoRecordings()
            },
            cache: false
        });
    });

}

var videoVideosListHammer = null;

function AddVideoVideosHammerActions() {
    if (!videoVideosListHammer) {
        videoVideosListHammer = $(".videoVideos").hammer({ prevent_default: true });
    }

    EnableDragScroll(videoVideosListHammer)

    videoVideosListHammer.on("doubletap", ".videoVideo", function () {
        OverlayScreenForLaunch()
        var playUrl = "/Video/PlayVideoFile?path=" + encodeURIComponent(this.id) + "&title=" + encodeURIComponent($(".videoRecordingTitle", this).text());
        $.ajax({
            url: playUrl,
            success: function (data) {
                RemoveScreenOverlay();
                DisplayRunningOnWatchPane(true)
            },
            error: RemoveScreenOverlay,
            cache: false
        });
    });

}

var videoDvdsListHammer = null;

function AddVideoDvdsHammerActions() {
    if (!videoDvdsListHammer) {
        videoDvdsListHammer = $(".videoDvds").hammer({ prevent_default: true });
    }

    EnableDragScroll(videoDvdsListHammer)

    videoDvdsListHammer.on("doubletap", ".videoDvdDirectory", function () {
        OverlayScreenForLaunch()
        var playUrl = "/Video/PlayDvdDirectory?path=" + encodeURIComponent(this.id) + "&title=" + encodeURIComponent($(".videoRecordingTitle", this).text());
        $.ajax({
            url: playUrl,
            success: function (data) {
                RemoveScreenOverlay();
                DisplayRunningOnWatchPane(true)
            },
            error: RemoveScreenOverlay,
            cache: false
        });
    });

}

var PositionMS = 0;
var DurationMS = 0;
var slidingTime = new Date(0);
var posSlider = null;
var lastDisplayUpdate = new Date();

function updateSlider() {
    var now = new Date();
    if (now.getTime() - slidingTime.getTime() > 5 * 1000 && DurationMS > 0 && PositionMS <= DurationMS) {
        var sliderValue = Math.round((PositionMS * 200) / DurationMS);
        $("#videoPosSlider").val(sliderValue)
    }
}

$(function () {
    InitializePositionSlider()

    SetRecordingsHeight()

    AddVideoControlsHammerActions();
    AddVideoRecordingsHammerActions();
    AddVideoVideosHammerActions();
    AddVideoDvdsHammerActions();

    $("#goVideoWatch").click(function () {
        LinkTo("/Video/Watch")
    });

    $("#goVideoRecordings").click(function () {
        LinkTo("/Video/Recordings")
    });

    $("#goVideoVideos").click(function () {
        LinkTo("/Video/Videos")
    });

    $("#goVideoDVDs").click(function () {
        LinkTo("/Video/DVDs")
    });

    $("#displayVideoRecordings").click(DisplayVideoRecordings);

    $("#displayVideos").click(DisplayVideos);

    $("#displayDVDs").click(DisplayDvds);

    // update information once now
    UpdateJrmcDisplayPlayingInformation();

    // update again every little bit
    jrmcRepeater = setInterval("UpdateJrmcDisplayPlayingInformation()", 2000);
})
