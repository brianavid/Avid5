
$(function () {

    if (window.innerWidth >= 1080) {
        LinkTo("/Home/Wide");
    }

    $("#selectMusic").mousedown(function () {
        StopSwitching();
        LaunchProgram("Music", "/Music/Playing");
    });

    $("#selectVideo").mousedown(function () {
        StopSwitching();
        var lastRunningProgram = $("#homeTitle").text();
        LaunchProgram( "Video", lastRunningProgram == "Video" ? "/Video/Watch" : "/Video/Recordings");
    });

    var timeoutId = 0;

    $('#selectTV').on('mousedown touchstart', function (event) {
        event.stopPropagation();
        StopSwitching();
        timeoutId = setTimeout(function () {
            timeoutId = 0;
            $.get("/Action/GoSmart", null, function () {
                LinkTo(document.getElementById("isWide") != null ? "/Streaming/All" : "/Streaming/Controls")
            })
        }, 1000);
    }).on('mouseup mouseleave touchend', function () {
        if (timeoutId != 0) {
            clearTimeout(timeoutId);
            timeoutId = 0;
            LaunchProgram("TV", "/TV/Channels")
        }
    });

    $("#selectRadio").mousedown(function () {
        StopSwitching();
        LaunchProgram("TV", "/TV/Radio", "Radio")
    });

    $("#selectSpotify").mousedown(function () {
        StopSwitching();
        LaunchProgram("Spotify", "/Spotify/Playing");
    });

    $("#selectRoku").mousedown(function () {
        StopSwitching();
        if (document.getElementById("isWide") == null &&
            $("#homeTitle").text() == "Roku") {
            LinkTo("/Streaming/Controls")
        } else {
            $.get("/Action/GoRoku", null, function () {
                LinkTo(document.getElementById("isWide") != null ? "/Streaming/All" : "/Streaming/Browser")
            })
        }
    });

    $("#selectCast").mousedown(function () {
        StopSwitching();
        $.get("/Action/GoChromecast", null, function () {
            LinkTo(document.getElementById("isWide") != null ? "/Streaming/All" : "/Streaming/Browser")
        })
    });

    $("#selectBluetooth").mousedown(function () {
        StopSwitching();
        $.get("/Action/GoBluetooth", null, function () {
            LinkTo(document.getElementById("isWide") != null ? "/Streaming/All" : "/Streaming/Browser")
        })
    });

    $("#selectPhotos").mousedown(function () {
        StopSwitching();
        var lastRunningProgram = $("#homeTitle").text();
        if (lastRunningProgram == "Photo")
        {
            LinkTo("/Photos/Display");
        }
        else
        {
            LaunchProgram("Photo", "/Photos/Browse");
        }
    });

    $("#selectPC").click(function () {
        StopSwitching();
        $.get("/Action/GoPC", null, function () {
            LinkTo("/Home/Home")
        })
    });

    $("#selectEpg").mousedown(function () {
        StopSwitching();
        LinkTo("/Guide/Browser?mode=GuideRoot");
    });

    $("#selectSecurity").mousedown(function () {
        StopSwitching();
        LinkTo("/Security/GetProfiles");
    });

    $(".playerActionButton").mousedown(function () {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Control/Key?Focus=1&Key=") + this.id,
            cache: false
        });
    });

});