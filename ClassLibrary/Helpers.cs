using System;

namespace ClassLibrary
{
    public class Helpers
    {
        public static string UriToString(Uri uri)
        {
            return (uri != null) ? uri.ToString() : "";
        }
    }
}
