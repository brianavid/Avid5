using SpotifyAPI.Web;

public class Button
{
    static string Image(string buttonName)
    {
        return $"<img src='/Content/Buttons/{buttonName}.png'/>";
    }
    public static string SmallRound(string buttonName)
    {
        return Image("SmallRound/" + buttonName);
    }
    public static string BigRound(string buttonName)
    {
        return Image("BigRound/" + buttonName);
    }
    public static string MiniRound(string buttonName)
    {
        return Image("MiniRound/" + buttonName);
    }
    public static string Square(string buttonName)
    {
        return Image("Square/" + buttonName);
    }
    public static string Rect(string buttonName)
    {
        return Image(buttonName);
    }
}
