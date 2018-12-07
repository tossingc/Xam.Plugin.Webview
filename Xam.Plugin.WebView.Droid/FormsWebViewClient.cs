using System;
using System.Threading;
using Android.Webkit;
using Android.Net.Http;
using Android.Graphics;
using Xam.Plugin.WebView.Abstractions;
using Android.Runtime;
using Android.Content;
using Xamarin.Forms;

namespace Xam.Plugin.WebView.Droid
{
    public class FormsWebViewClient : WebViewClient
    {

        private WeakReference<FormsWebViewRenderer> Reference { get; }

        private FormsWebViewRenderer Renderer =>
            Reference == null || !Reference.TryGetTarget(out var renderer)
                ? null
                : renderer;

        private FormsWebView Element => Renderer?.Element;

        public FormsWebViewClient(FormsWebViewRenderer renderer)
        {
            Reference = new WeakReference<FormsWebViewRenderer>(renderer);
        }

        public override void OnReceivedHttpError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceResponse errorResponse)
        {
            var element = Element;
            if (element == null) return;

            element.HandleNavigationError(errorResponse.StatusCode);
            element.HandleNavigationCompleted(request.Url.ToString());
            element.Navigating = false;
        }

        public override void OnReceivedError(Android.Webkit.WebView view, IWebResourceRequest request, WebResourceError error)
        {
            var element = Element;
            if (element == null) return;

            element.HandleNavigationError((int) error.ErrorCode);
            element.HandleNavigationCompleted(request.Url.ToString());
            element.Navigating = false;
        }

        //For Android < 5.0
        [Obsolete]
        public override void OnReceivedError(Android.Webkit.WebView view, [GeneratedEnum] ClientError errorCode, string description, string failingUrl)
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Lollipop) return;

            var element = Element;
            if (element == null) return;

            element.HandleNavigationError((int)errorCode);
            element.HandleNavigationCompleted(failingUrl.ToString());
            element.Navigating = false;
        }

        //For Android < 5.0
        [Obsolete]
        public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, string url)
        {
            CheckResponseValidity(view, url);

            return base.ShouldOverrideUrlLoading(view, url);
        }

        // NOTE: pulled fix from this unmerged PR - https://github.com/SKLn-Rad/Xam.Plugin.Webview/pull/104
        public override bool ShouldOverrideUrlLoading(Android.Webkit.WebView view, IWebResourceRequest request)
        {
            CheckResponseValidity(view, request.Url.ToString());

            return base.ShouldOverrideUrlLoading(view, request);
        }

        void CheckResponseValidity(Android.Webkit.WebView view, string url)
        {
            var element = Element;
            if (element == null) return;

            var response = element.HandleNavigationStartRequest(url);

            HandleDecisionHandlerDelegateResponse(view, url, response);
        }

        private void HandleDecisionHandlerDelegateResponse(Android.Webkit.WebView view, string url, Abstractions.Delegates.DecisionHandlerDelegate response)
        {
            if (!response.Cancel && !response.OffloadOntoDevice)
            {
                return;
            }

            var finishedManualResetEvent = new ManualResetEvent(false);
            void CancelOrOffloadOntoDevice()
            {
                if (response.OffloadOntoDevice && !AttemptToHandleCustomUrlScheme(view, url))
                {
                    Device.OpenUri(new Uri(url));
                }

                view.StopLoading();

                finishedManualResetEvent.Set();
            }

            if (Device.IsInvokeRequired)
            {
                Device.BeginInvokeOnMainThread(CancelOrOffloadOntoDevice);
            }
            else
            {
                CancelOrOffloadOntoDevice();
            }

            finishedManualResetEvent.WaitOne();
        }

        public override void OnPageStarted(Android.Webkit.WebView view, string url, Bitmap favicon)
        {
            var element = Element;
            if (element == null) return;

            element.Navigating = true;
        }

        bool AttemptToHandleCustomUrlScheme(Android.Webkit.WebView view, string url)
        {
            if (url.StartsWith("mailto"))
            {
                Android.Net.MailTo emailData = Android.Net.MailTo.Parse(url);

                Intent email = new Intent(Intent.ActionSendto);

                email.SetData(Android.Net.Uri.Parse("mailto:"));
                email.PutExtra(Intent.ExtraEmail, new String[] { emailData.To });
                email.PutExtra(Intent.ExtraSubject, emailData.Subject);
                email.PutExtra(Intent.ExtraCc, emailData.Cc);
                email.PutExtra(Intent.ExtraText, emailData.Body);

                if (email.ResolveActivity(Renderer.Context.PackageManager) != null)
                    Renderer.Context.StartActivity(email);

                return true;
            }

            if (url.StartsWith("http"))
            {
                Intent webPage = new Intent(Intent.ActionView, Android.Net.Uri.Parse(url));
                if (webPage.ResolveActivity(Renderer.Context.PackageManager) != null)
                    Renderer.Context.StartActivity(webPage);

                return true;
            }

            return false;
        }

        public override void OnReceivedSslError(Android.Webkit.WebView view, SslErrorHandler handler, SslError error)
        {
            var element = Element;
            if (element == null) return;

            if (FormsWebViewRenderer.IgnoreSSLGlobally)
            {
                handler.Proceed();
            }

            else
            {
                handler.Cancel();
                element.Navigating = false;
            }
        }

        public override async void OnPageFinished(Android.Webkit.WebView view, string url)
        {
            var renderer = Renderer;
            var element = Renderer?.Element;
            if (element == null) return;

            // Add Injection Function
            await renderer.OnJavascriptInjectionRequest(FormsWebView.InjectedFunction);

            // Add Global Callbacks
            if (element.EnableGlobalCallbacks)
                foreach (var callback in FormsWebView.GlobalRegisteredCallbacks)
                    await renderer.OnJavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(callback.Key));

            // Add Local Callbacks
            foreach (var callback in element.LocalRegisteredCallbacks)
                await renderer.OnJavascriptInjectionRequest(FormsWebView.GenerateFunctionScript(callback.Key));

            element.CanGoBack = view.CanGoBack();
            element.CanGoForward = view.CanGoForward();
            element.Navigating = false;

            element.HandleNavigationCompleted(url);
            element.HandleContentLoaded();
        }
    }
}