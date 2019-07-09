using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Xamarin.HotReload.Vsix
{
	public partial class HotReloadOptionsControl : UserControl
	{
		public HotReloadOptionsControl ()
		{
			InitializeComponent ();

			labelDesc.Text = Ide.Resources.Strings.Options_HotReload_Desc;
			labelPreview.Text = Ide.Resources.Strings.Options_HotReload_Preview_Desc;
			checkEnable.Text = Ide.Resources.Strings.Options_HotReload_Enable_Text;
		}

		public bool HotReloadEnabled {
			get => this.checkEnable.Checked;
			set => this.checkEnable.Checked = value;
		}
	}
}
