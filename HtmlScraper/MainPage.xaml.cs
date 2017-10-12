﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ClassLibrary;
using HtmlAgilityPack;
using System.Collections.ObjectModel;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

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
        }

        HtmlWeb htmlWeb = new HtmlWeb();
        HtmlDocument htmlDoc;
        ObservableCollection<HtmlElement> elementList = new ObservableCollection<HtmlElement>();

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
                Uri targetUri = new Uri(url);
                htmlDoc = htmlWeb.Load(url);
                WebViewControl.Navigate(targetUri);
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

        private void ListItemPathBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var nodes = htmlDoc.DocumentNode.SelectNodes(ListItemPathBox.Text);
            ListItemPathBlock.Text = $"Found {nodes.Count} nodes on this page.";

            childrenListView.ItemsSource = nodes.First().ChildNodes.Where(m => m.Name != "#text");
        }
    }
}
