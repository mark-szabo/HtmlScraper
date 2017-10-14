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
        public static async Task<string> InputTextDialogAsync(string title) => await InputTextDialogAsync(title, "");
        public static async Task<string> InputTextDialogAsync(string title, string defaultText)
        {
            TextBox inputTextBox = new TextBox
            {
                Text = defaultText,
                AcceptsReturn = false,
                Height = 32
            };
            ContentDialog dialog = new ContentDialog
            {
                Content = inputTextBox,
                Title = title,
                IsSecondaryButtonEnabled = false,
                PrimaryButtonText = "Ok"
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary) return inputTextBox.Text;
            else return "";
        }
    }
}
