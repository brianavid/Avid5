using SpotifyAPI.Web;
using static Button;

public class Button
{
    internal class Mapping
    {
        public string Glyph { get; private set; }
        public string Text { get; private set; }
        public string Style { get; private set; }
        public string Glyph2 { get; private set; }

        public Mapping(string glyph, string text, string style = "", string glyph2 = "")
        {
            Glyph = glyph;
            Text = text;
            Style = style;
            Glyph2 = glyph2;
        }

        public Mapping(string glyph)
        {
            Glyph = glyph;
            Text = null;
            Style = "";
            Glyph2 = "";
        }
    }

    //  The Mappings table maps the original(ish) names of PNG image files for buttons
    //  onto the text or glyph(s) that should appear inside CSS-styled buttons
    //  This avoids need to change all the places in views where buttons are used.
    private static Dictionary<string, Mapping> mappings = null;
    private static Dictionary<string, Mapping> Mappings
    {
        get
        {
            if (mappings == null)
            {
                mappings = new Dictionary<string, Mapping>();
                mappings["Back"] = new Mapping("Undo");
                mappings["Home"] = new Mapping("Home", "", "buttonGreen");
                mappings["Nav.Down"] = new Mapping("Arrow_Downward");
                mappings["Nav.Left"] = new Mapping("Arrow_Back");
                mappings["Nav.Right"] = new Mapping("Arrow_Forward");
                mappings["Nav.Up"] = new Mapping("Arrow_Upward");
                mappings["OK"] = new Mapping("", "OK");
                mappings["Plus"] = new Mapping("", "+");
                mappings["Minus"] = new Mapping("", "-");
                mappings["Star"] = new Mapping("Star");
                mappings["Enter"] = new Mapping("Keyboard_Return");
                mappings["Search"] = new Mapping("Search");
                mappings["Audio.Mute.On"] = new Mapping("volume_off");
                mappings["Audio.Volume.Down"] = new Mapping("volume_down");
                mappings["Audio.Volume.Up"] = new Mapping("volume_up");
                mappings["Exit"] = new Mapping("Cancel", "", "buttonRed");
                mappings["Power.Off"] = new Mapping("Bolt", "", "buttonRed");
                mappings["Rec"] = new Mapping("Fiber_Manual_Record", "", "buttonRed");
                mappings["RecSeries"] = new Mapping("Fiber_Smart_Record", "", "buttonRed");
                mappings["Spanner"] = new Mapping("Settings");
                mappings["ThumbsDown"] = new Mapping("Thumb_Down", "", "buttonRed");
                mappings["ThumbsUp"] = new Mapping("Thumb_Up", "", "buttonGreen");
                mappings["FastForward"] = new Mapping("Fast_Forward");
                mappings["Rewind"] = new Mapping("Fast_Rewind");
                mappings["Prev"] = new Mapping("Skip_Previous");
                mappings["Next"] = new Mapping("Skip_Next");
                mappings["Repeat"] = new Mapping("Replay");
                mappings["Stop"] = new Mapping("Stop");
                mappings["PlayPause"] = new Mapping("Play_Arrow", "", "", "Pause");
                mappings["Minus10"] = new Mapping("", "-10");
                mappings["Minus60"] = new Mapping("", "-60");
                mappings["Plus10"] = new Mapping("", "+10");
                mappings["Plus60"] = new Mapping("", "+60");
            }
            return mappings;
        }
    }

    public static string FontReference
    {
        get { return "<link rel='stylesheet' href='https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@24,400,1,0' />"; }
    }

    public static string SmallRound(string buttonName)
    {
        return StyledButton(buttonName, "buttonSmallRound", "buttonRegularIcon");
    }
    public static string BigRound(string buttonName)
    {
        return StyledButton(buttonName, "buttonBigRound", "buttonLargeIcon");
    }
    public static string MiniRound(string buttonName)
    {
        return StyledButton(buttonName, "buttonMiniRound", "buttonMiniIcon");
    }
    public static string Square(string buttonName)
    {
        return StyledButton(buttonName, "buttonSquare", "buttonLargeIcon");
    }

    public static string Rect(string buttonName)
    {
        return StyledButton(buttonName, "buttonRect", "buttonLargeIcon");
    }

    public static string LongRect(string buttonName)
    {
        return StyledButton(buttonName, "buttonLongRect", "buttonLargeIcon");
    }

    private static string StyledButton(string buttonName, string buttonStyle, string iconStyle)
    {
        var buttonContents = ButtonContents(buttonName, iconStyle);
        var additionalStyle = ButtonStyle(buttonName);
        return $"<span class='buttonCommon {buttonStyle} {additionalStyle}'>{buttonContents}</span>";
    }

    private static string ButtonContents(string buttonName, string iconStyle)
    {
        if (Mappings.ContainsKey(buttonName))
        {
            var mapping = Mappings[buttonName];
            if (!string.IsNullOrEmpty(mapping.Text))
            {
                return mapping.Text;
            }
            if (!string.IsNullOrEmpty(mapping.Glyph))
            {
                if (!string.IsNullOrEmpty(mapping.Glyph2))
                {
                    return $"<span class='material-symbols-rounded {iconStyle}'>{mapping.Glyph}</span>" +
                           $"<span class='material-symbols-rounded {iconStyle}'>{mapping.Glyph2}</span>";
                }
                return $"<span class='material-symbols-rounded {iconStyle}'>{mapping.Glyph}</span>";
            }
        }
        return buttonName;
    }

    private static string ButtonStyle(string buttonName)
    {
        if (Mappings.ContainsKey(buttonName))
        {
            var mapping = Mappings[buttonName];
            if (!string.IsNullOrEmpty(mapping.Style))
            {
                return mapping.Style;
            }
        }
        return "";
    }

    public static string DoubleTap(string iconStyle = "")  
    { 
        return $"<span class='material-symbols-rounded {iconStyle}'>touch_app</span>&nbsp;"; 
    } 
}
