using System.Net;

namespace Hkpropel;

public class CookieJson
{
    public string Domain { get; set; }
    public string Name { get; set; }
    public string Value { get; set; }

    public Cookie ToCookie()
    {
        var cookie = new Cookie { Domain = Domain, Value = Value };

        if (!string.IsNullOrEmpty(Name))
        {
            cookie.Name = Name;
        }

        return cookie;
    }
}
