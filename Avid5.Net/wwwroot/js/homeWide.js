
$(function () {
    if (window.innerWidth < 1080) {
        LinkTo("/Home/Home");
    }

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

    $("#selectRoku").mousedown(function () {
        StopSwitching();
        $.ajax({
            url: "/Action/GoRoku",
            success: function () {
                LinkTo("/Streaming/All");
            },
            cache: false
        });
    });

    $("#selectPhotos").mousedown(function () {
        StopSwitching();
        LaunchProgram("Photo", "/Photos/All");
    });

    $("#selectEpg").mousedown(function () {
        StopSwitching();
        LinkTo("/Guide/BrowserWide?mode=GuideRoot");
    });

});