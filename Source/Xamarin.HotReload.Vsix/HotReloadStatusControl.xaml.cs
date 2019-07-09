using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Xamarin.HotReload.Ide
{
	/// <summary>
	/// Interaction logic for HotReloadStatusControl.xaml
	/// </summary>
	public partial class HotReloadStatusControl : UserControl
	{
		public HotReloadStatusControl ()
		{
			InitializeComponent ();

			HotReloadState = HotReloadState.Disabled;
		}

		HotReloadState hotReloadState = HotReloadState.Disabled;

		bool hotReloadEnabled = true;
		internal bool HotReloadEnabled {
			get { return hotReloadEnabled; }
			set {
				hotReloadEnabled = value;
				img.Dispatcher.Invoke (() => {
					Visibility = hotReloadEnabled ? Visibility.Visible : Visibility.Collapsed;
				});
			}
		}

		internal HotReloadState HotReloadState {
			get {
				return hotReloadState;
			}
			set {
				hotReloadState = value;
				img.Dispatcher.Invoke (() => {
					switch (value) {
					case HotReloadState.Enabled:
						SetImage ("hrenabled.png");
						img.ToolTip = "Hot Reload Connected";
						break;
					case HotReloadState.Failed:
						SetImage ("hrfailed.png");
						img.ToolTip = "Hot Reload connection failed, check output log for more info...";
						break;
					case HotReloadState.Starting:
						SetImage ("hrneutral.png");
						img.ToolTip = "Hot Reload connection is starting...";
						break;
					default:
						SetImage ("hrdisabled.png");
						img.ToolTip = "Hot Reload waiting for debugging session...";
						break;
					}
				});
			}
		}

		void SetImage (string filename)
		{
			var path = $"pack://application:,,,/Xamarin.HotReload.Vsix;component/Resources/{filename}";

			img.Source = new BitmapImage (new Uri (path));
		}
	}
}
