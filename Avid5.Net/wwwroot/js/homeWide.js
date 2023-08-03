﻿
$(function () {

    $("#selectMusic").mousedown(function () {
        StopSwitching();
        LaunchProgram("Music", "/Music/All");
    });

    $("#selectVideo").mousedown(function () {
        StopSwitching();
        LaunchProgram("Video", "/Video/All");
    });

    $("#selectTV").mousedown(function () {
        StopSwitching();
        LaunchProgram("TV", "/TV/All")
    });

    $("#selectSpotify").mousedown(function () {
        StopSwitching();
        LaunchProgram("Spotify", "/Spotify/All");
    });

    $("#selectStream").mousedown(function () {
        StopSwitching();
        $.ajax({
            url: "/Action/StartStream",
            success: function () {
                LinkTo("/Streaming/All");
            },
            cache: false
        });
    });

    $("#selectPhotos").mousedown(function () {
        StopSwitching();
        LaunchNewProgram("Photo", "", "/Photos/All");
    });

    $("#selectEpg").mousedown(function () {
        StopSwitching();
        LinkTo("/Guide/BrowserWide?mode=GuideRoot");
    });

});