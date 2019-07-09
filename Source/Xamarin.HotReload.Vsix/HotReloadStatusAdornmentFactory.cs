using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Formatting;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Windows;

namespace Xamarin.HotReload.Ide
{
	//[Export (typeof (IWpfTextViewCreationListener))]
	//[ContentType ("XAML")]
	//[TextViewRole (PredefinedTextViewRoles.Editable)]
	sealed class AdornmentFactory : IWpfTextViewCreationListener
	{
		//[Export (typeof (AdornmentLayerDefinition))]
		//[Name ("HotReloadStatus")]
		//[Order (After = PredefinedAdornmentLayers.Caret)]
		//[TextViewRole (PredefinedTextViewRoles.Editable)]
		public AdornmentLayerDefinition editorAdornmentLayer = null;

		IdeManager ide;
		HotReloadStatusAdornment adornment;

		[ImportingConstructor]
		public AdornmentFactory (IdeManager ide)
		{
			this.ide = ide;
		}

		public void TextViewCreated (IWpfTextView textView)
		{
			adornment = new HotReloadStatusAdornment (ide, textView);
		}
	}

	class HotReloadStatusAdornment : IDisposable
	{
		IdeManager ide;
		HotReloadStatusControl _root;
		IWpfTextView _view;
		IAdornmentLayer _adornmentLayer;

		public HotReloadStatusAdornment (IdeManager ide, IWpfTextView view)
		{
			this.ide = ide;
			ide.AgentStatusChanged += IdeManager_AgentStatusChanged;
			//ide.Settings.SettingsChanged += IdeManager_SettingsChanged;

			_view = view;
			_root = new HotReloadStatusControl ();
			//_root.Visibility = ide.Settings.HotReloadEnabled ? Visibility.Visible : Visibility.Collapsed;

			_adornmentLayer = _view.GetAdornmentLayer ("HotReloadStatus");

			_view.ViewportHeightChanged += delegate { OnSizeChange (); };
			_view.ViewportWidthChanged += delegate { OnSizeChange (); };
		}

		void IdeManager_SettingsChanged (object sender, EventArgs e)
		{
			//if (_root != null)
			//	_root.HotReloadEnabled = IdeManager.Instance.Settings.HotReloadEnabled;
		}

		void IdeManager_AgentStatusChanged (object sender, AgentStatusMessage msg)
		{
			if (_root != null)
				_root.HotReloadState = msg.State;
		}

		public void OnSizeChange ()
		{
			_adornmentLayer.RemoveAdornment (_root);

			if (_root.ActualWidth <= 0) {
				_root.Measure (new Size (double.PositiveInfinity, double.PositiveInfinity));
				_root.Arrange (new Rect (0, 0, _root.DesiredSize.Width, _root.DesiredSize.Height));
			}

			Canvas.SetLeft (_root, _view.ViewportRight - _root.ActualWidth);
			Canvas.SetTop (_root, _view.ViewportTop);

			_adornmentLayer.AddAdornment (AdornmentPositioningBehavior.ViewportRelative, null, null, _root, null);
		}

		public void Dispose()
		{
			ide.AgentStatusChanged -= IdeManager_AgentStatusChanged;
			//ide.Settings.SettingsChanged -= IdeManager_SettingsChanged;
			ide = null;
		}
	}
}
