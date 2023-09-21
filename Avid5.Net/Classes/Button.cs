using SpotifyAPI.Web;
using System;
using static Button;

public class Button
{
    public static string FontReference
    {
        get { return "<link rel='stylesheet' href='https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@24,400,1,0' />"; }
    }

    public static string SmallRound(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonSmallRound", "buttonRegularIcon", additionalStyle);
    }
    public static string BigRound(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonBigRound", "buttonLargeIcon", additionalStyle);
    }
    public static string MiniRound(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonMiniRound", "buttonMiniIcon", additionalStyle);
    }
    public static string Square(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonSquare", "buttonLargeIcon", additionalStyle);
    }

    public static string Rect(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonRect", "buttonLargeIcon", additionalStyle);
    }

    public static string LongRect(string buttonName, string additionalStyle = "")
    {
        return StyledButton(buttonName, "buttonLongRect", "buttonLargeIcon", additionalStyle);
    }

    private static string StyledButton(string buttonName, string buttonStyle, string iconStyle, string additionalStyle)
    {
        var buttonContents = ButtonContents(buttonName, iconStyle);
        return $"<span class='buttonCommon {buttonStyle} {additionalStyle}'>{buttonContents}</span>";
    }

    private static string ButtonContents(string buttonName, string iconStyle)
    {
        
        return buttonName.Replace("[[", $"<span class='material-symbols-rounded {iconStyle}'>").Replace("]]", "</span>");
    }

    public static string DoubleTap(string iconStyle = "")  
    { 
        return $"<span class='material-symbols-rounded {iconStyle}'>touch_app</span>&nbsp;"; 
    } 
}
