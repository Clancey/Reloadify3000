namespace Xamarin.HotReload.Vsix
{
	partial class HotReloadOptionsControl
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && (components != null)) {
				components.Dispose ();
			}
			base.Dispose (disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent ()
		{
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.labelDesc = new System.Windows.Forms.Label();
            this.checkEnable = new System.Windows.Forms.CheckBox();
            this.labelPreview = new System.Windows.Forms.Label();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.labelDesc);
            this.flowLayoutPanel1.Controls.Add(this.checkEnable);
            this.flowLayoutPanel1.Controls.Add(this.labelPreview);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(16, 17);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(911, 380);
            this.flowLayoutPanel1.TabIndex = 3;
            this.flowLayoutPanel1.WrapContents = false;
            // 
            // labelDesc
            // 
            this.labelDesc.AutoSize = true;
            this.labelDesc.Location = new System.Drawing.Point(3, 0);
            this.labelDesc.Name = "labelDesc";
            this.labelDesc.Size = new System.Drawing.Size(812, 50);
            this.labelDesc.TabIndex = 5;
            this.labelDesc.Text = "Xamarin Hot Reload enables developers to continually save changes to their XAML f" +
    "iles and immediately view the results during a debugging session.";
            // 
            // checkEnable
            // 
            this.checkEnable.AutoSize = true;
            this.checkEnable.Checked = true;
            this.checkEnable.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkEnable.Location = new System.Drawing.Point(10, 64);
            this.checkEnable.Margin = new System.Windows.Forms.Padding(10, 14, 2, 14);
            this.checkEnable.Name = "checkEnable";
            this.checkEnable.Size = new System.Drawing.Size(366, 29);
            this.checkEnable.TabIndex = 4;
            this.checkEnable.Text = "Enable Xamarin Hot Reload (Preview)";
            this.checkEnable.UseVisualStyleBackColor = true;
            // 
            // labelPreview
            // 
            this.labelPreview.AutoSize = true;
            this.labelPreview.Location = new System.Drawing.Point(3, 107);
            this.labelPreview.Name = "labelPreview";
            this.labelPreview.Size = new System.Drawing.Size(444, 25);
            this.labelPreview.TabIndex = 6;
            this.labelPreview.Text = "Xamarin Hot Reload is currently a Preview feature.";
            // 
            // HotReloadOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.flowLayoutPanel1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "HotReloadOptionsControl";
            this.Padding = new System.Windows.Forms.Padding(16, 17, 16, 17);
            this.Size = new System.Drawing.Size(943, 414);
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
		private System.Windows.Forms.Label labelDesc;
		private System.Windows.Forms.CheckBox checkEnable;
		private System.Windows.Forms.Label labelPreview;
	}
}
