using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace HtmlScraper
{
    class UwpHelpers
    {
        public static async Task<ContentDialogResult> DisplayErrorDialogAsync(string title, string content)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };

            return await dialog.ShowAsync();
        }
    }
}
