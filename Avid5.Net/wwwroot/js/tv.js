var PositionMS = 0;
var DurationMS = 0;
var slidingTime = new Date(0);
var posSlider = null;
var lastDisplayUpdate = new Date();

function updateSlider() {
    var now = new Date();
    if (now.getTime() - slidingTime.getTime() > 5 * 1000 && DurationMS > 0 && PositionMS <= DurationMS) {
        var sliderValue = Math.round((PositionMS * 200) / DurationMS);
        $("#liveTvPosSlider").val(sliderValue)
    }
}

function UpdateTvDisplayPlayingInformation() {
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
        url: "/Tv/GetLiveTVPlayingPositionInfo",
        timeout: 700,
        cache: false,
        success: function (xml) {
            if (xml != null) {
                var pos = xml.documentElement;
                $("#tvCurrentChannelName").text(pos.getAttribute("channel"));
                $("#tvCurrentProgramme").text(pos.getAttribute("now"));
                $("#tvNextPrograme").text(pos.getAttribute("next"));
                $("#tvPlaybackStartTime").text(pos.getAttribute("startDisplay"));
                $("#tvPlaybackPositionTime").text(pos.getAttribute("positionDisplay"));
                $("#tvPlaybackEndTime").text(pos.getAttribute("endDisplay"));
                $("#tvPlaybackCurrentStatus").text(pos.getAttribute("state"));
                PositionMS = parseInt(pos.getAttribute("positionMS"))
                DurationMS = parseInt(pos.getAttribute("durationMS"))
                updateSlider()
            }
        }
    });
}

$("#liveTvPosSlider").noUiSlider({
    range: [0, 200]
    , start: 0
    , step: 1
    , handles: 1
    , slide: function () {
        slidingTime = new Date();
        var pos = Math.floor($(this).val());
        PositionMS = pos * DurationMS / 200;
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=" + PositionMS),
            success: function (data) {
                UpdateTvDisplayPlayingInformation()
            },
           cache: false
        })
    }
});


var controlHammer = null;

function AddControlHammerActions(controlUnderButtons) {
    if (!controlUnderButtons)
    {
        $(".tvControlPane").each(function () {
            $(this).height($(window).height() - getTop(this))
        });
    }

    if (!controlHammer) {
        controlHammer = $(".tvControlPane").hammer({ prevent_default: true });
    }

    controlHammer.on("touch", ".tvAction", function (e) {
        $.ajax({
            url: "/Tv/Action?command=" + this.id,
            success: function (data) {
                UpdateTvDisplayPlayingInformation()
            },
            cache: false
        });
    });

    controlHammer.on("touch", "#tvBack60", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=60000&Relative=-1"),
            success: function (data) {
                PositionMS -= 60000;
                UpdateTvDisplayPlayingInformation();
            },
            cache: false
        })
    });

    controlHammer.on("touch", "#tvBack10", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=10000&Relative=-1"),
            success: function (data) {
                PositionMS -= 10000;
                UpdateTvDisplayPlayingInformation();
            },
            cache: false
        })
    });

    controlHammer.on("touch", "#tvForward10", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=10000&Relative=1"),
            success: function (data) {
                PositionMS += 10000;
                UpdateTvDisplayPlayingInformation();
            },
            cache: false
        })
    });

    controlHammer.on("touch", "#tvForward60", function (e) {
        e.preventDefault();
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Position?Position=60000&Relative=1"),
            success: function (data) {
                PositionMS += 60000;
                UpdateTvDisplayPlayingInformation();
            },
            cache: false
        })
    });

    controlHammer.on("touch", "#tvPlayPause", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/PlayPause"),
            success: function (data) {
                UpdateTvDisplayPlayingInformation();
            },
            cache: false
        })
    });

    controlHammer.on("hold", "#tvPlayPause", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Stop"),
            success: function (data) {
                UpdateTvDisplayPlayingInformation();
            },
           cache: false
        })
    });

    controlHammer.on("touch", "#tvStop", function (e) {
        $.ajax({
            url: "/Video/SendMCWS?url=" + encodeURIComponent("Playback/Stop"),
            success: function (data) {
                UpdateTvDisplayPlayingInformation();
            },
          cache: false
        })
    });


    controlHammer.on("tap", ".tvProgrammeRecord", function (e) {
        $.ajax({
            url: "/Tv/RecordNow",
            success: function (data) {
                UpdateTvDisplayPlayingInformation()
            },
            cache: false
        });
    });
}

var buttonsHammer = null;

function AddButtonsHammerActions(controlHeight) {
    $("#tvButtonsPane").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log("#tvButtonsPane h=" + h + "; t=" + t + "; c=" + controlHeight + " => " + (h - t - controlHeight))
        $(this).height(h - t - controlHeight)
    });

    if (!buttonsHammer) {
        buttonsHammer = $(".tvButtons").hammer({ prevent_default: true });
    }

    EnableDragScroll(buttonsHammer)

    buttonsHammer.on("touch", ".tvAction", function (e) {
        $.ajax({
            url: "/Tv/Action?command=" + this.id,
            success: function (data) {
                UpdateTvDisplayPlayingInformation()
            },
            cache: false
        });
    });
}

function SetChannelsHeight()
{
    $(".tvChannels").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log(".tvChannels h=" + h + "; t=" + t + " => " + (h - t))
        $(this).height(h - t)
    });
}

var channelsHammer = null;

function AddChannelsHammerActions() {
    SetChannelsHeight()

    if (!channelsHammer) {
        channelsHammer = $(".tvChannels").hammer({ prevent_default: true, holdThreshold: 100 });
    }

    EnableDragScroll(channelsHammer)

    channelsHammer.on("hold", ".tvChannel", function (e) {
        e.gesture.stopDetect()
        e.gesture.preventDefault()
        var which = this;
        $.ajax({
            url: "/Tv/NowAndNext?channelKey=" + encodeURIComponent(this.id),
            success: function (data) {
                $(".tvChannelNowNext", which).html(data)
            },
            cache: false
        });
    });

    channelsHammer.on("tap", ".tvChannel", function (e) {
        e.gesture.preventDefault()
        var which = this;
        $.ajax({
            url: "/Tv/ChangeChannel?channelKey=" + encodeURIComponent(this.id),
            success: function (data) {
                if (document.getElementById("tvControlPane") == null) {
                    LinkTo("/Tv/Watch");
                }
            },
            cache: false
        });
    });
}

function ResizeButtons()
{
    var controlHeight = 0;

    $("#tvControlPane").each(function () {
        controlHeight = $(this).height();
    })

    $("#tvButtonsPane").each(function () {
        var h = $(window).height() - 24
        var t = $(this).offset().top
        console.log("#tvButtonsPane h=" + h + "; t=" + t + "; c=" + controlHeight + " => " + (h - t - controlHeight))
        $(this).height(h - t - controlHeight)
    });
}

function DisplayRunningOnControlPad(jump) {
    var controlDisplay = document.getElementById("tvControlPane");

    if (controlDisplay != null) {
        ReplacePane("tvControlPane", "/Tv/ControlPane", "none", ResizeButtons);
    }
    else if (jump) {
        LinkTo("/Tv/Watch");
    }
}

function DisplayTvChannels() {
    var channelsDisplay = document.getElementById("tvChannels");
    if (channelsDisplay != null) {
        $(".tvChannels").hide()
        $.ajax({
            url: "/Tv/ChannelsPane",
            success: function (data) {
                $(".tvChannels").html(data)
                $(".tvChannels").show()
                SetChannelsHeight();
            },
            cache: false
        });
    }
}

function DisplayTvRadio() {
    var channelsDisplay = document.getElementById("tvChannels");
    if (channelsDisplay != null) {
        $(".tvChannels").hide()
        $.ajax({
            url: "/Tv/RadioPane",
            success: function (data) {
                $(".tvChannels").html(data)
                $(".tvChannels").show()
                SetChannelsHeight();
            },
            cache: false
        });
    }
}

$(function () {
    var controlHeight = 0;
    var controlUnderButtons = false;

    $("#tvControlPane").each(function () {
        controlHeight = $(this).height();
    })

    $("#tvButtonsPane").each(function () {
        controlUnderButtons = true;
    })

    AddControlHammerActions(controlUnderButtons)
    AddButtonsHammerActions(controlHeight)
    AddChannelsHammerActions()

    $("#goTvWatch").click(function () {
        LinkTo("/Tv/Watch");
    });

    $("#goTvTv").click(function () {
        LinkTo("/Tv/Channels")
    });

    $("#goTvRadio").click(function () {
        LinkTo("/Tv/Radio")
    });

    $("#goTvControls").click(function () {
        LinkTo("/Tv/Buttons")
    });

    $("#displayTvTv").click(DisplayTvChannels);

    $("#displayTvRadio").click(DisplayTvRadio);

    // update again every few seconds
    setInterval("UpdateTvDisplayPlayingInformation()", 2000);
})