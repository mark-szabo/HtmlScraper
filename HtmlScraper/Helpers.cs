using HtmlAgilityPack;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace HtmlScraper
{
    class Helpers
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

        /// <summary>
        /// Returns app version in "HtmlScraper 2.0.3" format.
        /// </summary>
        /// <returns></returns>
        public static string GetVersionString()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return string.Format("HtmlScraper {0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

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

        private static IEnumerable<bool> IterateUntilFalse(bool condition)
        {
            while (condition) yield return true;
        }

        public static async Task ExcelExportAsync(List<SelectedNode> nodes, Uri baseUrl, string paginationGetParam, string basePath, Action<int> callback)
        {
            try
            {
                // Register encoding provider
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Perform Excel export
                using (ExcelPackage excelPackage = new ExcelPackage())
                {
                    excelPackage.Workbook.Properties.Author = GetVersionString();
                    var worksheet = excelPackage.Workbook.Worksheets.Add("HtmlScraper");

                    int excelRowIndex = 1, excelColumnIndex = 1, pageNumber = 0;
                    HtmlWeb htmlWeb = new HtmlWeb();

                    // Write header
                    foreach (var node in nodes)
                    {
                        worksheet.Cells[excelRowIndex, excelColumnIndex].Value = node.Name;
                        excelColumnIndex++;

                        if (node.HtmlTag == "a")
                        {
                            worksheet.Cells[excelRowIndex, excelColumnIndex].Value = node.Name + "Url";
                            excelColumnIndex++;
                        }
                    }
                    excelRowIndex++;

                    bool newItemsFound = true;
                    //Parallel.ForEach(IterateUntilFalse(newItemsFound), ignored => { });
                    while (newItemsFound)
                    {
                        pageNumber++;
                        var uriBuilder = new UriBuilder(baseUrl);
                        if (paginationGetParam != null && paginationGetParam != "") uriBuilder.Query = $"{paginationGetParam}={pageNumber}";
                        else newItemsFound = false;

                        var htmlDoc = htmlWeb.Load(uriBuilder.Uri);
                        var newItems = htmlDoc.DocumentNode.SelectNodes(basePath);

                        if (newItems == null) newItemsFound = false;
                        else
                        {
                            foreach (var item in newItems)
                            {
                                excelColumnIndex = 1;
                                foreach (var node in nodes)
                                {
                                    var nodeElement = htmlDoc.DocumentNode.SelectSingleNode(item.XPath + node.RelativePath);
                                    switch (node.HtmlTag)
                                    {
                                        case "a":
                                            if (node.Name == "Rating") worksheet.Cells[excelRowIndex, excelColumnIndex].Value = Convert.ToDouble(TrimStart(nodeElement?.Attributes["class"]?.Value, "rating-")) / 10;
                                            else worksheet.Cells[excelRowIndex, excelColumnIndex].Value = nodeElement?.InnerText;
                                            excelColumnIndex++;
                                            var hrefUriBuilder = new UriBuilder(baseUrl)
                                            {
                                                Path = nodeElement?.Attributes["href"]?.Value
                                            };
                                            worksheet.Cells[excelRowIndex, excelColumnIndex].Value = HttpUtility.UrlDecode(hrefUriBuilder.Uri.AbsoluteUri);
                                            break;
                                        case "img":
                                            worksheet.Cells[excelRowIndex, excelColumnIndex].Value = nodeElement?.Attributes["src"]?.Value;
                                            break;
                                        default:
                                            worksheet.Cells[excelRowIndex, excelColumnIndex].Value = nodeElement?.InnerText;
                                            break;
                                    }
                                    excelColumnIndex++;
                                }
                                excelRowIndex++;
                            }
                        }

                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { callback(pageNumber); });

                        //System.Windows.Threading.Dispatcher.Invoke(() =>
                        //{
                        //    callback(pageNumber);
                        //});
                    }

                    var savePicker = new FileSavePicker
                    {
                        SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                    };
                    // Dropdown of file types the user can save the file as
                    savePicker.FileTypeChoices.Add("Excel (.xlsx)", new List<string>() { ".xlsx" });
                    // Default file name if the user does not type one in or select a file to replace
                    savePicker.SuggestedFileName = "HtmlScraperExport";

                    StorageFile file = await savePicker.PickSaveFileAsync();
                    if (file != null)
                    {
                        // Prevent updates to the remote version of the file until
                        // we finish making changes and call CompleteUpdatesAsync.
                        CachedFileManager.DeferUpdates(file);

                        // write to file
                        var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                        using (Stream s = stream.GetOutputStreamAt(0).AsStreamForWrite())
                        {
                            excelPackage.SaveAs(s);
                        }
                        stream.Dispose();

                        // Let Windows know that we're finished changing the file so
                        // the other app can update the remote version of the file.
                        // Completing updates may require Windows to ask for user input.
                        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                        if (status == FileUpdateStatus.Complete)
                        {
                            await DisplayErrorDialogAsync("Export successful", "Successfully exported into the Excel file: " + file.Name);
                        }
                        else
                        {
                            await DisplayErrorDialogAsync("Export error", "File " + file.Name + " couldn't be saved.");
                        }
                    }
                    else
                    {
                        await DisplayErrorDialogAsync("Export error", "Operation cancelled.");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayErrorDialogAsync("Export error", "Error while exporting: " + ex.ToString());
            }
        }
    }

    public class SelectedNode
    {
        public string Name { get; set; }
        public string HtmlTag { get; set; }
        public string RelativePath { get; set; }
    }
}

