using System;

namespace ClassLibrary
{
    public static class Helpers
    {
        public static string UriToString(Uri uri)
        {
            return (uri != null) ? uri.ToString() : "";
        }
    }
    public class HtmlElement
    {
        public string Name { get; set; }
        public string Attribute { get; set; }
    }
}
