using System;
using Xam.Plugin.WebView.Abstractions.Delegates;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace SampleApp.Samples
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class StringDataSample : ContentPage
    {
        public StringDataSample()
        {
            InitializeComponent();
            stringContent.Source = @"
<!DOCTYPE HTML>
<html>
	<head>
		<meta name='viewport' content='width=device-width, initial-scale=1.0'/>
	</head>
	<body style='padding:5px 15px'>
		<p>This is a new message.
<br/>
<br/><a href='https://vfdworld.com'>https://vfdworld.com</a>
<br/>
<br/>
<br/>Your Cloud-based, Email-to-Fax Provider
<br/>Dan Jonke
<br/>Senior Software Engineer
<br/><a href='tel:4147210303'>414.721.0303</a> Phone
<br/><a href='mailto:djonke@iscinternational.com'>djonke@iscinternational.com</a>
<br/>Learn More at <a href='http://ISCFAX.COM'>ISCFAX.COM</a>
<br/>
<br/>
<br/></p>
	</body>
</html>
            ";
        }

        private void FormsWebView_OnNavigationStarted(object sender, DecisionHandlerDelegate e)
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri))
            {
                // if this is not a valid absolute URI, try to load it in the WebView itself
                e.Cancel = false;
                e.OffloadOntoDevice = false;
                return;
            }

            if (Device.RuntimePlatform == Device.Android && e.Uri.StartsWith("data:text/html"))
            {
                // NOTE: this is a workaround for a this issue: https://github.com/SKLn-Rad/Xam.Plugin.Webview/issues/88
                // https://github.com/Nico04/Xam.Plugin.Webview/commit/223983d4d4da0208f3979a9779652bcdfea341e6#commitcomment-29184077
                e.Cancel = false;
                e.OffloadOntoDevice = false;
                return;
            }

            if (e.Uri.StartsWith("file://", StringComparison.CurrentCultureIgnoreCase))
            {
                e.Cancel = false;
                e.OffloadOntoDevice = false;
                return;
            }

            if (e.Uri.StartsWith("mailto:", StringComparison.CurrentCultureIgnoreCase))
            {
                // we are handling mail links
                e.Cancel = true;
                e.OffloadOntoDevice = false;
                return;
            }

            // delegate everything else to Xamarin.Forms.Device.OpenUri(..)
            e.Cancel = true;
            e.OffloadOntoDevice = false;

            Xamarin.Forms.Device.OpenUri(uri);
        }
    }
}