using System;

namespace ClassLibrary
{
    public static class Helpers
    {
        public static string UriToString(Uri uri)
        {
            return (uri != null) ? uri.ToString() : "";
        }

        public static string TrimStart(string target, string trimString)
        {
            string result = target;
            while (result.StartsWith(trimString))
            {
                result = result.Substring(trimString.Length);
            }

            return result;
        }
    }
    public class SelectedNode
    {
        public string Name { get; set; }
        public string Attribute { get; set; }
        public string HtmlTag { get; set; }
        public string RelativePath { get; set; }
    }
}
