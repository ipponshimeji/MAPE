namespace MAPE.Windows.GUI {
	partial class NotifyIconComponent {
		/// <summary>
		/// 必要なデザイナー変数です。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// 使用中のリソースをすべてクリーンアップします。
		/// </summary>
		/// <param name="disposing">マネージ リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
		protected override void Dispose(bool disposing) {
			if (disposing && (components != null)) {
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region コンポーネント デザイナーで生成されたコード

		/// <summary>
		/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディターで変更しないでください。
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NotifyIconComponent));
			this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
			this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.StartMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.StopMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuItemSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.OpenMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuItemSeparator2 = new System.Windows.Forms.ToolStripSeparator();
			this.ExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.contextMenuStrip.SuspendLayout();
			// 
			// notifyIcon
			// 
			this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
			resources.ApplyResources(this.notifyIcon, "notifyIcon");
			// 
			// contextMenuStrip
			// 
			this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.StartMenuItem,
            this.StopMenuItem,
            this.menuItemSeparator1,
            this.OpenMenuItem,
            this.menuItemSeparator2,
            this.ExitMenuItem});
			this.contextMenuStrip.Name = "contextMenuStrip";
			resources.ApplyResources(this.contextMenuStrip, "contextMenuStrip");
			// 
			// StartMenuItem
			// 
			this.StartMenuItem.Name = "StartMenuItem";
			resources.ApplyResources(this.StartMenuItem, "StartMenuItem");
			// 
			// StopMenuItem
			// 
			this.StopMenuItem.Name = "StopMenuItem";
			resources.ApplyResources(this.StopMenuItem, "StopMenuItem");
			// 
			// menuItemSeparator1
			// 
			this.menuItemSeparator1.Name = "menuItemSeparator1";
			resources.ApplyResources(this.menuItemSeparator1, "menuItemSeparator1");
			// 
			// OpenMenuItem
			// 
			this.OpenMenuItem.Name = "OpenMenuItem";
			resources.ApplyResources(this.OpenMenuItem, "OpenMenuItem");
			// 
			// menuItemSeparator2
			// 
			this.menuItemSeparator2.Name = "menuItemSeparator2";
			resources.ApplyResources(this.menuItemSeparator2, "menuItemSeparator2");
			// 
			// ExitMenuItem
			// 
			this.ExitMenuItem.Name = "ExitMenuItem";
			resources.ApplyResources(this.ExitMenuItem, "ExitMenuItem");
			this.contextMenuStrip.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.NotifyIcon notifyIcon;
		private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
		private System.Windows.Forms.ToolStripSeparator menuItemSeparator1;
		private System.Windows.Forms.ToolStripSeparator menuItemSeparator2;
		internal System.Windows.Forms.ToolStripMenuItem StartMenuItem;
		internal System.Windows.Forms.ToolStripMenuItem StopMenuItem;
		internal System.Windows.Forms.ToolStripMenuItem OpenMenuItem;
		internal System.Windows.Forms.ToolStripMenuItem ExitMenuItem;
	}
}
