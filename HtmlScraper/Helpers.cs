using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using HtmlAgilityPack;
using OfficeOpenXml;

namespace HtmlScraper
{
    internal static class Helpers
    {
        private static object _incrementLock = new object();
        public static string UriToString(Uri uri) { return uri != null ? uri.ToString() : ""; }

        public static string TrimStart(string target, string trimString)
        {
            var result = target;
            while (result.StartsWith(trimString)) result = result.Substring(trimString.Length);

            return result;
        }

        /// <summary>
        ///     Returns app version in "HtmlScraper 2.0.3" format.
        /// </summary>
        /// <returns></returns>
        public static string GetVersionString()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return string.Format("HtmlScraper {0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        public static async Task<ContentDialogResult> DisplayErrorDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog { Title = title, Content = content, CloseButtonText = "OK" };

            return await dialog.ShowAsync();
        }

        public static async Task<string> InputTextDialogAsync(string title) { return await InputTextDialogAsync(title, ""); }

        public static async Task<string> InputTextDialogAsync(string title, string defaultText)
        {
            var inputTextBox = new TextBox { Text = defaultText, AcceptsReturn = false, Height = 32 };
            var dialog = new ContentDialog { Content = inputTextBox, Title = title, IsSecondaryButtonEnabled = false, PrimaryButtonText = "Ok" };

            return await dialog.ShowAsync() == ContentDialogResult.Primary ? inputTextBox.Text : "";
        }

        private static IEnumerable<bool> IterateUntilTrue(bool condition)
        {
            while (!condition)
            {
                yield return true;
            }
        }

        public static async Task ExcelExportAsync(List<SelectedNode> nodes, Uri baseUrl, string paginationGetParam, string basePath, Action<int> callback)
        {
            try
            {
                // Register encoding provider
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // Perform Excel export
                using (var excelPackage = new ExcelPackage())
                {
                    excelPackage.Workbook.Properties.Author = GetVersionString();
                    var worksheet = excelPackage.Workbook.Worksheets.Add("HtmlScraper");

                    int excelRowIndex = 1, excelColumnIndex = 1, pageNumber = 0;
                    var htmlWeb = new HtmlWeb();

                    // Write header
                    foreach (var node in nodes)
                    {
                        worksheet.Cells[excelRowIndex, excelColumnIndex].Value = node.Name;
                        excelColumnIndex++;

                        if (node.HtmlTag != "a") continue;
                        worksheet.Cells[excelRowIndex, excelColumnIndex].Value = node.Name + "Url";
                        excelColumnIndex++;
                    }
                    excelRowIndex++;

                    await Task.Factory.StartNew(
                        () =>
                        {
                            var noNewItemFoundCancellationTokenSource = new CancellationTokenSource();
                            Parallel.ForEach(
                                IterateUntilTrue(noNewItemFoundCancellationTokenSource.IsCancellationRequested),
                                async ignored =>
                                {
                                    int currentPageNumber;
                                    lock (_incrementLock)
                                    {
                                        pageNumber++;
                                        currentPageNumber = pageNumber;
                                    }
                                    var uriBuilder = new UriBuilder(baseUrl);
                                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                                    if (!string.IsNullOrEmpty(paginationGetParam)) query[paginationGetParam] = currentPageNumber.ToString();
                                    else noNewItemFoundCancellationTokenSource.Cancel();
                                    uriBuilder.Query = query.ToString();

                                    var htmlDoc = htmlWeb.Load(uriBuilder.Uri);
                                    var newItems = htmlDoc.DocumentNode.SelectNodes(basePath);

                                    if (newItems == null) noNewItemFoundCancellationTokenSource.Cancel();
                                    else
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
                                                        var hrefUriBuilder = new UriBuilder(baseUrl) {Path = nodeElement?.Attributes["href"]?.Value};
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

                                    Debug.WriteLine($"Now on page {currentPageNumber}");
                                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => { callback(currentPageNumber); });

                                });
                        });

                    var savePicker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
                    // Dropdown of file types the user can save the file as
                    savePicker.FileTypeChoices.Add("Excel (.xlsx)", new List<string> { ".xlsx" });
                    // Default file name if the user does not type one in or select a file to replace
                    savePicker.SuggestedFileName = "HtmlScraperExport";

                    var file = await savePicker.PickSaveFileAsync();
                    if (file != null)
                    {
                        // Prevent updates to the remote version of the file until
                        // we finish making changes and call CompleteUpdatesAsync.
                        CachedFileManager.DeferUpdates(file);

                        // write to file
                        var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
                        using (var s = stream.GetOutputStreamAt(0).AsStreamForWrite()) { excelPackage.SaveAs(s); }
                        stream.Dispose();

                        // Let Windows know that we're finished changing the file so
                        // the other app can update the remote version of the file.
                        // Completing updates may require Windows to ask for user input.
                        var status = await CachedFileManager.CompleteUpdatesAsync(file);
                        if (status == FileUpdateStatus.Complete) await DisplayErrorDialogAsync("Export successful", "Successfully exported into the Excel file: " + file.Name);
                        else await DisplayErrorDialogAsync("Export error", "File " + file.Name + " couldn't be saved.");
                    }
                    else { await DisplayErrorDialogAsync("Export error", "Operation cancelled."); }
                }
            }
            catch (Exception ex) { await DisplayErrorDialogAsync("Export error", "Error while exporting: " + ex); }
        }
    }

    public class SelectedNode
    {
        public string Name { get; set; }
        public string HtmlTag { get; set; }
        public string RelativePath { get; set; }
    }
}