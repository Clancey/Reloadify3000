using System;
using MonoDevelop.Components;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Ide.Composition;

using Xamarin.HotReload.Ide;
using Xwt;

namespace Xamarin.HotReload.VSMac
{
	public class HotReloadOptionsPanel : OptionsPanel
	{
		HotReloadOptionsWidget widget;
		ISettingsProvider settings;

		public HotReloadOptionsPanel ()
		{
			settings = CompositionManager.Instance.GetExportedValue<ISettingsProvider> ();
		}

		public override Control CreatePanelWidget ()
		{
			widget = new HotReloadOptionsWidget ();
			widget.HotReloadEnabled = settings.HotReloadEnabled;
			return widget.ToGtkWidget ();
		}

		public override void ApplyChanges ()
		{
			settings.HotReloadEnabled = widget.HotReloadEnabled;
		}
	}

	class HotReloadOptionsWidget : Xwt.VBox
	{
		readonly CheckBox checkEnabled;

		public HotReloadOptionsWidget()
		{
			var t = new RichTextView ();
			t.LoadText ("Xamarin Hot Reload enables developers to continually save changes to their XAML files and immediately view the results during a debugging session.", Xwt.Formats.TextFormat.Markdown);
			t.ReadOnly = true;
			t.Selectable = true;
			t.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor;
			t.LineSpacing = 3;
			PackStart (t);

			checkEnabled = new CheckBox ("Enable Xamarin Hot Reload (Preview)");
			PackStart (checkEnabled);

			var t2 = new RichTextView ();
			t2.LoadText ("Hot Reload is currently a _Preview_ feature.", Xwt.Formats.TextFormat.Markdown);
			t2.ReadOnly = true;
			t2.Selectable = true;
			t2.BackgroundColor = MonoDevelop.Ide.Gui.Styles.BackgroundColor;
			t2.LineSpacing = 3;
			PackStart (t2);
		}

		public bool HotReloadEnabled {
			get => checkEnabled.State == CheckBoxState.On;
			set => checkEnabled.State = value ? CheckBoxState.On : CheckBoxState.Off;
		}
	}

}
