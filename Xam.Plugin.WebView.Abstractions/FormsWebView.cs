using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xam.Plugin.WebView.Abstractions.Delegates;
using Xam.Plugin.WebView.Abstractions.Enumerations;
using Xam.Plugin.WebView.Abstractions.Models;
using Xamarin.Forms;

[assembly: InternalsVisibleTo("Xam.Plugin.WebView.UWP")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.Droid")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.iOS")]
[assembly: InternalsVisibleTo("Xam.Plugin.WebView.MacOS")]
namespace Xam.Plugin.WebView.Abstractions
{
    public partial class FormsWebView : View, IFormsWebView, IDisposable
    {
        public bool ShouldSynchronizeAllCallsToInjectJavascriptAsync { get; set; }

        /// <summary>
        /// A delegate which takes valid javascript and returns the response from it, if the response is a string.
        /// </summary>
        /// <param name="js">The valid JS to inject</param>
        /// <returns>Any string response from the DOM or string.Empty</returns>
        public delegate Task<string> JavascriptInjectionRequestDelegate(string js);


        /// <summary>
        /// Delegate to await clearing cookies. Will remove all temporary data on UWP
        /// </summary>
        public delegate Task ClearCookiesRequestDelegate();

        /// <summary>
        /// Fired when navigation begins, for example when the source is set.
        /// </summary>
        public event EventHandler<DecisionHandlerDelegate> OnNavigationStarted;

        /// <summary>
        /// Fires when navigation is completed. This can be either as the result of a valid navigation, or on an error.
        /// Returns the URL of the page navigated to.
        /// </summary>
        public event EventHandler<string> OnNavigationCompleted;

        /// <summary>
        /// Fires when navigation fires an error. By default this uses the native systems error codes.
        /// </summary>
        public event EventHandler<int> OnNavigationError;

        /// <summary>
        /// Fires when the content on the DOM is ready. All your calls to Javascript using C# should be performed after this is fired.
        /// </summary>
        public event EventHandler OnContentLoaded;

        internal event EventHandler OnBackRequested;

        internal event EventHandler OnForwardRequested;

        internal event EventHandler OnRefreshRequested;

        internal event JavascriptInjectionRequestDelegate OnJavascriptInjectionRequest;

        internal event ClearCookiesRequestDelegate OnClearCookiesRequested;

        internal readonly Dictionary<string, Action<string>> LocalRegisteredCallbacks = new Dictionary<string, Action<string>>();

        /// <summary>
        /// A dictionary containing all headers to be injected into the request. Local headers take precedence over global ones.
        /// </summary>
        public readonly Dictionary<string, string> LocalRegisteredHeaders = new Dictionary<string, string>();

        /// <summary>
        /// The content type to attempt to load. By default this is Internet.
        /// </summary>
        public WebViewContentType ContentType
        {
            get => (WebViewContentType)GetValue(ContentTypeProperty);
            set => SetValue(ContentTypeProperty, value);
        }

        /// <summary>
        /// The source data. This can either be a valid URL, a path to a local file, or a HTML string.
        /// </summary>
        public string Source
        {
            get => (string) GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Override the BaseURL in the renderer with this property.
        /// By default, the BaseUrls are the following:
        /// Android) Assets folder with AndroidAsset build property
        /// iOS and MacOS) Resources folder with BundleResource build property
        /// UWP) Project folder with content build property
        /// </summary>
        public string BaseUrl
        {
            get { return (string)GetValue(BaseUrlProperty); }
            set { SetValue(BaseUrlProperty, value); }
        }

        /// <summary>
        /// Opt in and out of global callbacks
        /// </summary>
        public bool EnableGlobalCallbacks
        {
            get => (bool) GetValue(EnableGlobalCallbacksProperty);
            set => SetValue(EnableGlobalCallbacksProperty, value);
        }

        /// <summary>
        /// Opt in and out of global headers
        /// </summary>
        public bool EnableGlobalHeaders
        {
            get => (bool) GetValue(EnableGlobalHeadersProperty);
            set => SetValue(EnableGlobalHeadersProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the page is currently navigating.
        /// </summary>
        public bool Navigating
        {
            get => (bool)GetValue(NavigatingProperty);
            internal set => SetValue(NavigatingProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the webview can go back a page.
        /// </summary>
        public bool CanGoBack
        {
            get => (bool) GetValue(CanGoBackProperty);
            internal set => SetValue(CanGoBackProperty, value);
        }

        /// <summary>
        /// Bindable property which is true when the webview can go forward a page.
        /// </summary>
        public bool CanGoForward
        {
            get => (bool) GetValue(CanGoForwardProperty);
            internal set => SetValue(CanGoForwardProperty, value);
        }

        public bool UseWideViewPort
        {
            get => (bool) GetValue(UseWideViewPortProperty);
            set => SetValue(UseWideViewPortProperty, value);
        }

        public Rectangle? SelectionClientBoundingRectangle
        {
            get => (Rectangle?)GetValue(SelectionClientBoundingRectangleProperty);
            set => SetValue(SelectionClientBoundingRectangleProperty, value);
        }

        private static SemaphoreSlim InjectJavascriptAsyncSynchronizationSemaphoreSlim { get; }

        static FormsWebView()
        {
            InjectJavascriptAsyncSynchronizationSemaphoreSlim = new SemaphoreSlim(1, 1);
        }

        public FormsWebView()
        {
            HorizontalOptions = VerticalOptions = LayoutOptions.FillAndExpand;

            AddLocalCallback("formsWebViewSelectionChanged", FormsWebViewSelectionChanged);
        }

        private void FormsWebViewSelectionChanged(string selectionRangeBoundingClientDomRectJson)
        {
            var selectionClientBoundingRectangle = GetRectangleFromDomRectJson(selectionRangeBoundingClientDomRectJson);

            SelectionClientBoundingRectangle = selectionClientBoundingRectangle;
        }

        private static Rectangle? GetRectangleFromDomRectJson(string selectionRangeBoundingClientDOMRectJson)
        {
            if (selectionRangeBoundingClientDOMRectJson == null)
            {
                return null;
            }

            var selectionRangeBoundingClientDomRect =
                JsonConvert.DeserializeObject<JObject>(selectionRangeBoundingClientDOMRectJson);

            if (selectionRangeBoundingClientDomRect == null)
            {
                return null;
            }

            var selectionClientBoundingRectangle = new Rectangle(
                (double)selectionRangeBoundingClientDomRect["left"],
                (double)selectionRangeBoundingClientDomRect["top"],
                (double)selectionRangeBoundingClientDomRect["width"],
                (double)selectionRangeBoundingClientDomRect["height"]);
            return selectionClientBoundingRectangle;
        }

        /// <summary>
        /// Navigate back a page if capable of doing so.
        /// </summary>
        public void GoBack()
        {
            if (!CanGoBack) return;
            OnBackRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Navigate forward a page if capable of doing so.
        /// </summary>
        public void GoForward()
        {
            if (!CanGoForward) return;
            OnForwardRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refresh the current page if capable of doing so.
        /// </summary>
        public void Refresh()
        {
            OnRefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clearing all cookies.
        /// For UWP, all temporary browser data will be cleared.
        /// </summary>
        public async Task ClearCookiesAsync() {
            if (OnClearCookiesRequested != null)
                await OnClearCookiesRequested.Invoke();
        }

        /// <summary>
        /// Inject some javascript, returning a string result if the resulting Javascript resolves to a string on the DOM.
        /// For example 'document.body.style.backgroundColor = \"red\";' will return 'red'.
        /// </summary>
        /// <param name="js">The javascript to inject</param>
        /// <returns>A valid string response or string.Empty</returns>
        public async Task<string> InjectJavascriptAsync(string js)
        {
            var didWaitOnInjectJavascriptAsyncSynchronizationSemaphoreSlim = false;
            try
            {
                if (ShouldSynchronizeAllCallsToInjectJavascriptAsync)
                {
                    await InjectJavascriptAsyncSynchronizationSemaphoreSlim.WaitAsync();
                    didWaitOnInjectJavascriptAsyncSynchronizationSemaphoreSlim = true;
                }

                if (string.IsNullOrWhiteSpace(js)) return string.Empty;

                if (OnJavascriptInjectionRequest != null)
                    return await OnJavascriptInjectionRequest.Invoke(js);

                return string.Empty;
            }
            finally
            {
                if (didWaitOnInjectJavascriptAsyncSynchronizationSemaphoreSlim)
                {
                    InjectJavascriptAsyncSynchronizationSemaphoreSlim.Release();
                }
            }
        }

        /// <summary>
        /// Adds a callback to the DOM, this callback when passed a string by the Javascript, will fire an action with that string as the parameter.
        /// </summary>
        /// <param name="functionName">The name of the function</param>
        /// <param name="action">The action to call back to</param>
        public void AddLocalCallback(string functionName, Action<string> action)
        {
            if (string.IsNullOrWhiteSpace(functionName)) return;

            if (LocalRegisteredCallbacks.ContainsKey(functionName))
                LocalRegisteredCallbacks.Remove(functionName);

            LocalRegisteredCallbacks.Add(functionName, action);
            CallbackAdded?.Invoke(this, functionName);
        }

        /// <summary>
        /// Removes a callback by the function name.
        /// Note: this does not remove it from the DOM, rather it removes the action, resulting in your view never getting the response.
        /// </summary>
        /// <param name="functionName"></param>
        public void RemoveLocalCallback(string functionName)
        {
            if (LocalRegisteredCallbacks.ContainsKey(functionName))
                LocalRegisteredCallbacks.Remove(functionName);
        }

        /// <summary>
        /// Removes all local callbacks from the DOM.
        /// Note: this does not remove it from the DOM, rather it removes the action, resulting in your view never getting the response.
        /// </summary>
        public void RemoveAllLocalCallbacks()
        {
            LocalRegisteredCallbacks.Clear();
        }

        /// <summary>
        /// Dispose of the WebView
        /// </summary>
        public void Dispose()
        {
            LocalRegisteredCallbacks.Clear();
            LocalRegisteredHeaders.Clear();
        }

        // All code which should be hidden from the end user goes here
        #region Internals

        internal DecisionHandlerDelegate HandleNavigationStartRequest(string uri)
        {
            // By default, we only attempt to offload valid Uris with none http/s schemes
            bool validUri = Uri.TryCreate(uri, UriKind.Absolute, out Uri uriResult);
            bool validScheme = false;
            
            if (validUri)
                validScheme = uriResult.Scheme.StartsWith("http") || uriResult.Scheme.StartsWith("file");

            var handler = new DecisionHandlerDelegate()
            {
                Uri = uri,
                OffloadOntoDevice = validUri && !validScheme
            };

            OnNavigationStarted?.Invoke(this, handler);
            return handler;
        }

        private const string AddOnSelectionChangeEventHandlerJavascript = @"

if (!window.hasRegisteredFormsWebViewOnSelectionChangeEventHandler) {

window.getSelectionBoundingClientRectJson = function () {
	return JSON.stringify(window.getSelectionBoundingClientRect());
};

window.getSelectionBoundingClientRect = function () {

	var selection = window.getSelection();
	var selectionRange = selection.getRangeAt(0);

	var anchorNode = selection.anchorNode;
	var anchorParentElement = anchorNode.getBoundingClientRect
		? anchorNode
		: anchorNode.parentElement;

	if (selectionRange.collapsed) {
		return window.getBoundingClientRectForCollapsedRange(selectionRange);
	}

	var selectionRangeBoundingClientRect = selectionRange.getBoundingClientRect();

	var startHandleSelectionRangeBoundingClientRect = 
		window.getBoundingClientRectForContainerAndOffset(
			selectionRange.startContainer,
			selectionRange.startOffset
		);
	var endHandleSelectionRangeBoundingClientRect = 
		window.getBoundingClientRectForContainerAndOffset(
			selectionRange.endContainer,
			selectionRange.endOffset
		);

	// this accounts for the case where the selection ends up much larger than it should be (mostly seen with tables)
	// where selection includes all of table bounds when we are selecting a character in the middle of the table
	var selectionStartHandleTop = Math.min(
		startHandleSelectionRangeBoundingClientRect.top,
		endHandleSelectionRangeBoundingClientRect.top
	);
	if (selectionRangeBoundingClientRect.top != selectionStartHandleTop) {
		selectionRangeBoundingClientRect.y = selectionStartHandleTop;
	}
	var selectionEndHandleBottom = Math.max(
		endHandleSelectionRangeBoundingClientRect.bottom,
		startHandleSelectionRangeBoundingClientRect.bottom
	);
	if (selectionRangeBoundingClientRect.bottom != selectionEndHandleBottom) {
		selectionRangeBoundingClientRect.height = selectionEndHandleBottom - selectionStartHandleTop;
	}

	return selectionRangeBoundingClientRect;
};

window.getBoundingClientRectForContainerAndOffset = function(container, offset) {
	var range = document.createRange();
	range.setStart(container, offset);
	range.setEnd(container, offset);

	return window.getBoundingClientRectForCollapsedRange(range);
}

window.getBoundingClientRectForCollapsedRange = function(range) {
	var rangeBoundingClientRect;

	var container = range.startContainer;
	var offset = range.startOffset;

	// on iOS, getBoundingClientRect() on a range that is collapsed returns 0s for all values (Android does return the position, but we are reusing this code for both platforms for consistency)
	// this code approximates the bounding client rect when the selection range is collapsed (start and end position are the same)
	// under certain edge cases, it will return a rect that includes the previous and/or next lines
	var clonedRange = range.cloneRange();

	try {
		clonedRange.setStart(container, offset - 1);
	} catch (e) { }
	try {
		clonedRange.setEnd(container, offset + 1);
	} catch (e) { }

	if (clonedRange.collapsed === false) {
		
		rangeBoundingClientRect = clonedRange.getBoundingClientRect();

		if (!(rangeBoundingClientRect.x==0 &&
			  rangeBoundingClientRect.y==0 &&
			  rangeBoundingClientRect.width==0 &&
			  rangeBoundingClientRect.height==0)) {
			return rangeBoundingClientRect;
		}
	}

	clonedRange = range.cloneRange();
	var newSpan = document.createElement('span');
	newSpan.appendChild(document.createTextNode('.'));
	clonedRange.insertNode(newSpan);
	var boundingClientRect5 = clonedRange.getBoundingClientRect();
	newSpan.remove();
	return boundingClientRect5;
}

window.contentOnselectionchangeHandler = document.onselectionchange;

document.onselectionchange = function () {
	try {
		var windowFormsWebViewSelectionChanged = window.formsWebViewSelectionChanged;
		if (windowFormsWebViewSelectionChanged == null) {
			return;
		}

		windowFormsWebViewSelectionChanged(window.getSelectionBoundingClientRectJson());
	}
	finally {
		var windowContentOnselectionchangeHandler = window.contentOnselectionchangeHandler;
		if (windowContentOnselectionchangeHandler == null) {
			return;
		}

		windowContentOnselectionchangeHandler();
	}
};

document.onkeyup = function() {
	if (document.onselectionchange) {
		document.onselectionchange();
	}
};

    window.hasRegisteredFormsWebViewOnSelectionChangeEventHandler = true;
}
";

        internal void HandleNavigationCompleted(string uri)
        {
#pragma warning disable 4014
            InjectJavascriptAsync(AddOnSelectionChangeEventHandlerJavascript);
#pragma warning restore 4014

            OnNavigationCompleted?.Invoke(this, uri);
        }

        internal void HandleNavigationError(int errorCode)
        {
            OnNavigationError?.Invoke(this, errorCode);
        }

        internal void HandleContentLoaded()
        {
            OnContentLoaded?.Invoke(this, EventArgs.Empty);
        }

        internal void HandleScriptReceived(string data)
        {
            if (string.IsNullOrWhiteSpace(data)) return;
            
            var action = JsonConvert.DeserializeObject<ActionEvent>(data);

            // Decode
            byte[] dBytes = Convert.FromBase64String(action.Data);
            action.Data = Encoding.UTF8.GetString(dBytes, 0, dBytes.Length);

            // Local takes priority
            if (LocalRegisteredCallbacks.ContainsKey(action.Action))
                LocalRegisteredCallbacks[action.Action]?.Invoke(action.Data);

            // Global is checked if local fails
            else if (GlobalRegisteredCallbacks.ContainsKey(action.Action))
                GlobalRegisteredCallbacks[action.Action]?.Invoke(action.Data);
        }

        #endregion
    }
}
