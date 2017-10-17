using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using HtmlAgilityPack;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;

namespace HtmlScraper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        HtmlWeb htmlWeb = new HtmlWeb();
        HtmlDocument htmlDoc;
        ObservableCollection<SelectedNode> selectedNodes = new ObservableCollection<SelectedNode>();
        HtmlNode parentNode;
        Uri baseUrl;
        string basePath = "";

        /// <summary>
        /// This is the click handler for the "Go" button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void GoButton_Click()
        {
            if (!pageIsLoading)
            {
                NavigateWebviewAsync(AddressBox.Text);
            }
            else
            {
                WebViewControl.Stop();
                pageIsLoading = false;
            }
        }

        /// <summary>
        /// Property to control the "Go" button text, forward/backward buttons and progress ring.
        /// </summary>
        private bool _pageIsLoading;
        bool pageIsLoading
        {
            get { return _pageIsLoading; }
            set
            {
                _pageIsLoading = value;
                GoButton.Content = (value ? "Stop" : "Go");
                ProgressControl.Opacity = (value ? 1 : 0);
            }
        }

        /// <summary>
        /// This handles the enter key in the url address box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Address_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                NavigateWebviewAsync(AddressBox.Text);
            }
        }

        /// <summary>
        /// Helper to perform the navigation in webview
        /// </summary>
        /// <param name="url"></param>
        private void NavigateWebviewAsync(string url)
        {
            try
            {
                baseUrl = new Uri(url);
                htmlDoc = htmlWeb.Load(baseUrl);
                WebViewControl.Navigate(baseUrl);
            }
            catch (UriFormatException ex)
            {
                // Bad address
                AppendLog($"Address is invalid, try again. Error: {ex.Message}.");
            }
        }

        /// <summary>
        /// Handle the event that indicates that WebView is starting a navigation.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void WebViewControl_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            string url = Helpers.UriToString(args.Uri);
            AddressBox.Text = url;
            AppendLog($"Starting navigation to: \"{url}\".");
            pageIsLoading = true;
        }

        /// <summary>
        /// Handle the event that indicates that the WebView content is not a web page.
        /// For example, it may be a file download.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void WebViewControl_UnviewableContentIdentified(WebView sender, WebViewUnviewableContentIdentifiedEventArgs args)
        {
            AppendLog($"Content for \"{Helpers.UriToString(args.Uri)}\" cannot be loaded into webview.");
            // We throw away the request. See the "Unviewable content" scenario for other
            // ways of handling the event.
            pageIsLoading = false;
        }

        /// <summary>
        /// Handle the event that indicates that WebView has resolved the URI, and that it is loading HTML content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void WebViewControl_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            AppendLog($"Loading content for \"{Helpers.UriToString(args.Uri)}\".");
        }


        /// <summary>
        /// Handle the event that indicates that the WebView content is fully loaded.
        /// If you need to invoke script, it is best to wait for this event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void WebViewControl_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            AppendLog($"Content for \"{Helpers.UriToString(args.Uri)}\" has finished loading.");
        }

        /// <summary>
        /// Event to indicate webview has completed the navigation, either with success or failure.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void WebViewControl_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            pageIsLoading = false;
            if (args.IsSuccess)
            {
                AppendLog($"Navigation to \"{Helpers.UriToString(args.Uri)}\" completed successfully.");
            }
            else
            {
                AppendLog($"Navigation to: \"{Helpers.UriToString(args.Uri)}\" failed with error {args.WebErrorStatus}.");
            }
        }

        /// <summary>
        /// Helper for logging
        /// </summary>
        /// <param name="logEntry"></param>
        void AppendLog(string logEntry)
        {
            logResults.Inlines.Add(new Run { Text = logEntry + "\n" });
            logScroller.ChangeView(0, logScroller.ScrollableHeight, null);
        }

        private async void ListItemPathGoButton_Click(object sender, RoutedEventArgs e)
        {
            if (htmlDoc == null)
            {
                await Helpers.DisplayErrorDialogAsync("Page not loaded", "Please load the page first by clicking the Go button next to the URL box!");
                return;
            }

            var nodes = htmlDoc.DocumentNode.SelectNodes(ListItemPathBox.Text);
            basePath = nodes.First().XPath;

            ListItemPathBlock.Text = $"Found {nodes.Count} nodes on this page.";
            ChildrenListView.ItemsSource = nodes.First().ChildNodes.Where(m => m.Name != "#text");
        }

        private void ChildrenListViewItem_Click(object sender, ItemClickEventArgs e)
        {
            var clickedNode = (HtmlNode)e.ClickedItem;
            parentNode = clickedNode;

            ChildrenListView.ItemsSource = (clickedNode).ChildNodes.Where(m => m.Name != "#text");
        }

        private async void ChildrenListViewItem_AddButtonClick(object sender, RoutedEventArgs e)
        {
            ListItemPathBox.IsEnabled = false;

            var path = (String)((Button)sender).Tag;
            var clickedNode = htmlDoc.DocumentNode.SelectSingleNode(path);
            var name = await Helpers.InputTextDialogAsync("Give a name for this element!", (clickedNode.Attributes["class"]?.Value ?? ""));
            var newSelectedNode = new SelectedNode
            {
                Name = name,
                HtmlTag = clickedNode.Name,
                RelativePath = Helpers.TrimStart(clickedNode.XPath, basePath)
            };

            if (!selectedNodes.Any(m => m.RelativePath == newSelectedNode.RelativePath)) selectedNodes.Add(newSelectedNode);
            else await Helpers.DisplayErrorDialogAsync("Item already selected", "You have already selected this item.");

            SelectedNodesListView.ItemsSource = selectedNodes;
        }

        private void SelectedNodesListViewItem_RemoveButtonClick(object sender, RoutedEventArgs e)
        {
            var path = Helpers.TrimStart((String)((Button)sender).Tag, basePath);
            selectedNodes.Remove(selectedNodes.SingleOrDefault(m => m.RelativePath == path));

            SelectedNodesListView.ItemsSource = selectedNodes;

            if (selectedNodes.Count == 0) ListItemPathBox.IsEnabled = true;
        }

        private void ChildrenListViewItem_UpButtonClick(object sender, RoutedEventArgs e)
        {
            var path = (String)((Button)sender).Tag;
            var clickedNode = htmlDoc.DocumentNode.SelectSingleNode(path);

            ChildrenListView.ItemsSource = clickedNode.ParentNode.ParentNode.ChildNodes.Where(m => m.Name != "#text");
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var childrenList = (IEnumerable<HtmlNode>)ChildrenListView.ItemsSource;
                if (childrenList != null) if (childrenList.Count() != 0) ChildrenListView.ItemsSource = childrenList.First().ParentNode.ParentNode.ChildNodes.Where(m => m.Name != "#text");
                    else if (parentNode != null) if (parentNode.ParentNode != null) ChildrenListView.ItemsSource = parentNode.ParentNode.ChildNodes.Where(m => m.Name != "#text");
            }
            catch (Exception) { }
        }

        private async void ExcelExportButton_Click(object sender, RoutedEventArgs e)
        {
            // Disable the UI
            WebViewControl.IsTapEnabled = false;
            GoButton.IsEnabled = false;
            AddressBox.IsEnabled = false;
            PaginationBox.IsEnabled = false;
            ListItemPathBox.IsEnabled = false;
            ListItemPathGoButton.IsEnabled = false;
            UpButton.IsEnabled = false;
            ChildrenScroller.IsEnabled = false;
            SelectedNodesScroller.IsEnabled = false;

            // Diplay ProgressRing
            ExportProgressRing.IsActive = true;
            ExportProgressRing.Visibility = Visibility.Visible;

            await Helpers.ExcelExportAsync(
                selectedNodes.ToList(),
                baseUrl,
                PaginationBox.Text,
                ListItemPathBox.Text,
                ExportCallback);

            // Hide ProgressRing
            ExportProgressRing.IsActive = false;
            ExportProgressRing.Visibility = Visibility.Collapsed;

            // Re-enable UI
            WebViewControl.IsTapEnabled = true;
            GoButton.IsEnabled = true;
            AddressBox.IsEnabled = true;
            PaginationBox.IsEnabled = true;
            ListItemPathGoButton.IsEnabled = true;
            UpButton.IsEnabled = true;
            ChildrenScroller.IsEnabled = true;
            SelectedNodesScroller.IsEnabled = true;
        }

        private void ExportCallback(int pageNumber)
        {
            AppendLog($"Loaded items from page {pageNumber}.");
        }
    }
}
