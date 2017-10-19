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

namespace HtmlScraper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private readonly HtmlWeb _htmlWeb = new HtmlWeb();
        private HtmlDocument _htmlDoc;
        private readonly ObservableCollection<SelectedNode> _selectedNodes = new ObservableCollection<SelectedNode>();
        private HtmlNode _parentNode;
        private Uri _baseUrl;
        private string _basePath = "";

        /// <summary>
        /// This is the click handler for the "Go" button.
        /// </summary>
        private void GoButton_Click()
        {
            if (!PageIsLoading)
            {
                NavigateWebviewAsync(AddressBox.Text);
            }
            else
            {
                WebViewControl.Stop();
                PageIsLoading = false;
            }
        }

        /// <summary>
        /// Property to control the "Go" button text, forward/backward buttons and progress ring.
        /// </summary>
        private bool _pageIsLoading;

        private bool PageIsLoading
        {
            get => _pageIsLoading;
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
        private void Address_KeyUp(object sender, KeyRoutedEventArgs e)
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
                _baseUrl = new Uri(url);
                _htmlDoc = _htmlWeb.Load(_baseUrl);
                WebViewControl.Navigate(_baseUrl);
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
        private void WebViewControl_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            var url = Helpers.UriToString(args.Uri);
            AddressBox.Text = url;
            AppendLog($"Starting navigation to: \"{url}\".");
            PageIsLoading = true;
        }

        /// <summary>
        /// Handle the event that indicates that the WebView content is not a web page.
        /// For example, it may be a file download.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebViewControl_UnviewableContentIdentified(WebView sender, WebViewUnviewableContentIdentifiedEventArgs args)
        {
            AppendLog($"Content for \"{Helpers.UriToString(args.Uri)}\" cannot be loaded into webview.");
            // We throw away the request. See the "Unviewable content" scenario for other
            // ways of handling the event.
            PageIsLoading = false;
        }

        /// <summary>
        /// Handle the event that indicates that WebView has resolved the URI, and that it is loading HTML content.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebViewControl_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            var url = Helpers.UriToString(args.Uri);
            AddressBox.Text = url;
            _baseUrl = new Uri(url);
            _htmlDoc = _htmlWeb.Load(_baseUrl);
            AppendLog($"Loading content for \"{url}\".");
        }
        
        /// <summary>
        /// Handle the event that indicates that the WebView content is fully loaded.
        /// If you need to invoke script, it is best to wait for this event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebViewControl_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            AppendLog($"Content for \"{Helpers.UriToString(args.Uri)}\" has finished loading.");
        }

        /// <summary>
        /// Event to indicate webview has completed the navigation, either with success or failure.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void WebViewControl_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            PageIsLoading = false;
            AppendLog(args.IsSuccess
                ? $"Navigation to \"{Helpers.UriToString(args.Uri)}\" completed successfully."
                : $"Navigation to: \"{Helpers.UriToString(args.Uri)}\" failed with error {args.WebErrorStatus}.");
        }

        /// <summary>
        /// Helper for logging
        /// </summary>
        /// <param name="logEntry"></param>
        private void AppendLog(string logEntry)
        {
            LogResults.Inlines.Add(new Run { Text = logEntry + "\n" });
            LogScroller.ChangeView(0, LogScroller.ScrollableHeight, null);
        }

        private async void ListItemPathGoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_htmlDoc == null)
            {
                await Helpers.DisplayErrorDialogAsync("Page not loaded", "Please load the page first by clicking the Go button next to the URL box!");
                return;
            }

            var nodes = _htmlDoc.DocumentNode.SelectNodes(ListItemPathBox.Text);
            _basePath = nodes.First().XPath;

            ListItemPathBlock.Text = $"Found {nodes.Count} nodes on this page.";
            ChildrenListView.ItemsSource = nodes.First().ChildNodes.Where(m => m.Name != "#text");
        }

        private void ChildrenListViewItem_Click(object sender, ItemClickEventArgs e)
        {
            var clickedNode = (HtmlNode)e.ClickedItem;
            _parentNode = clickedNode;

            ChildrenListView.ItemsSource = clickedNode.ChildNodes.Where(m => m.Name != "#text");
        }

        private async void ChildrenListViewItem_AddButtonClick(object sender, RoutedEventArgs e)
        {
            ListItemPathBox.IsEnabled = false;

            var path = (string)((Button)sender).Tag;
            var clickedNode = _htmlDoc.DocumentNode.SelectSingleNode(path);
            var name = await Helpers.InputTextDialogAsync("Give a name for this element!", clickedNode.Attributes["class"]?.Value ?? "");
            var newSelectedNode = new SelectedNode
            {
                Name = name,
                HtmlTag = clickedNode.Name,
                RelativePath = Helpers.TrimStart(clickedNode.XPath, _basePath)
            };

            if (_selectedNodes.All(m => m.RelativePath != newSelectedNode.RelativePath)) _selectedNodes.Add(newSelectedNode);
            else await Helpers.DisplayErrorDialogAsync("Item already selected", "You have already selected this item.");

            SelectedNodesListView.ItemsSource = _selectedNodes;
        }

        private void SelectedNodesListViewItem_RemoveButtonClick(object sender, RoutedEventArgs e)
        {
            var path = Helpers.TrimStart((string)((Button)sender).Tag, _basePath);
            _selectedNodes.Remove(_selectedNodes.SingleOrDefault(m => m.RelativePath == path));

            SelectedNodesListView.ItemsSource = _selectedNodes;

            if (_selectedNodes.Count == 0) ListItemPathBox.IsEnabled = true;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var childrenList = (IEnumerable<HtmlNode>)ChildrenListView.ItemsSource;
                if (childrenList == null) return;
                var htmlNodes = childrenList as IList<HtmlNode> ?? childrenList.ToList();
                if (htmlNodes.Count() != 0) ChildrenListView.ItemsSource = htmlNodes.First().ParentNode.ParentNode.ChildNodes.Where(m => m.Name != "#text");
                else if (_parentNode?.ParentNode != null) ChildrenListView.ItemsSource = _parentNode.ParentNode.ChildNodes.Where(m => m.Name != "#text");
            }
            catch (Exception)
            {
                // ignored
            }
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
                _selectedNodes.ToList(),
                _baseUrl,
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
