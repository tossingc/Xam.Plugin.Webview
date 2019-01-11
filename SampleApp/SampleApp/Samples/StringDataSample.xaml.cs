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
<!doctype html>
<html>
<head>
	<meta name='viewport' content='width=device-width, user-scalable=0, initial-scale=1, maximum-scale=1, minimum-scale=1' />
</head>
    <body contenteditable='true'>
		<h1>This is a HTML string</h1>
	</body>
</html>
            ";
        }

        private async void Switch_OnToggled(object sender, ToggledEventArgs e)
        {
            await stringContent.InjectJavascriptAsync(
                $"document.body.setAttribute('contenteditable', '{e.Value.ToString().ToLowerInvariant()}')");
        }
    }
}