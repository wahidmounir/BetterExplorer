﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using BExplorer.Shell;
using BExplorer.Shell.Interop;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using WindowsHelper;

namespace BetterExplorer {

	public partial class IconView : Form {
		private List<IconFile> icons = null;
		private ShellView ShellView;
		private bool IsLibrary;
		private VisualStyleRenderer ItemSelectedRenderer = new VisualStyleRenderer("Explorer::ListView", 1, 3);
		private VisualStyleRenderer ItemHoverRenderer = new VisualStyleRenderer("Explorer::ListView", 1, 2);
		private VisualStyleRenderer Selectedx2Renderer = new VisualStyleRenderer("Explorer::ListView", 1, 6);

		public IconView() {
			InitializeComponent();
			WindowsAPI.SetWindowTheme(lvIcons.Handle, "explorer", 0);
		}

		public void LoadIcons(ShellView shellView, bool isLibrary) {
			tbLibrary.Text = @"C:\Windows\System32\imageres.dll";
			ShellView = shellView;
			IsLibrary = isLibrary;
			ShowDialog();
		}

		private void lvIcons_DrawItem(object sender, DrawListViewItemEventArgs e) {
			e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
			e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
			e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

			if ((e.State & ListViewItemStates.Hot) != 0 && (e.State & ListViewItemStates.Selected) == 0) {
				ItemHoverRenderer.DrawBackground(e.Graphics, e.Bounds);
			}
			else if ((e.State & ListViewItemStates.Hot) != 0 && (e.State & ListViewItemStates.Selected) != 0) {
				Selectedx2Renderer.DrawBackground(e.Graphics, e.Bounds);
			}
			else if ((e.State & ListViewItemStates.Selected) != 0) {
				ItemSelectedRenderer.DrawBackground(e.Graphics, e.Bounds);
			}
			else {
				e.DrawBackground();
			}
			//if ((e.State & ListViewItemStates.) != 0)
			//{
			//    e.Graphics.FillRectangle(Brushes.White, e.Bounds);
			//}

			Icon ico = icons[(int)e.Item.Tag].Icon;
			if (ico.Width <= 48) {
				e.Graphics.DrawIcon(icons[(int)e.Item.Tag].Icon,
						e.Bounds.X + (e.Bounds.Width - ico.Width) / 2, e.Bounds.Y + (e.Bounds.Height - ico.Height) / 2 - 5);
			}
			e.DrawText(TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter | TextFormatFlags.WordEllipsis);
			//e.DrawDefault = true;
		}

		private void btnLoad_Click(object sender, EventArgs e) {
			var dlg = new CommonOpenFileDialog("Select icon file");
			dlg.AllowNonFileSystemItems = false;
			dlg.Filters.Add(new CommonFileDialogFilter("Icon Files", "*.exe;*.dll;*.icl; *.ico"));
			if (dlg.ShowDialog(this.Handle) == CommonFileDialogResult.Ok) {
				tbLibrary.Text = dlg.FileName;
			}
			BackgroundWorker bw = new BackgroundWorker();
			bw.DoWork += new DoWorkEventHandler(bw_DoWork);
			bw.WorkerReportsProgress = true;
			bw.WorkerSupportsCancellation = true;
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			pbProgress.Visible = true;
			bw.RunWorkerAsync();
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			lvIcons.BeginUpdate();
			lvIcons.Items.Clear();
			foreach (IconFile icon in icons) {
				ListViewItem lvi = new ListViewItem("#" + icon.Index.ToString());
				lvi.Tag = icon.Index;
				lvIcons.Items.Add(lvi);
			}

			lvIcons.EndUpdate();
			pbProgress.Visible = false;
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e) {
			//IconReader ir = new IconReader();
			icons = IconReader.ReadIcons(tbLibrary.Text, new System.Drawing.Size(48, 48));
		}

		private void LoadIcons(object Params) {
			Invoke(new MethodInvoker(
							delegate {
								lvIcons.BeginUpdate();
								lvIcons.Items.Clear();
								//IconReader ir = new IconReader();
								icons = IconReader.ReadIcons(Params.ToString(), new System.Drawing.Size(48, 48));
								foreach (IconFile icon in icons) {
									ListViewItem lvi = new ListViewItem("#" + icon.Index.ToString());
									lvi.Tag = icon.Index;
									lvIcons.Items.Add(lvi);
								}
								lvIcons.EndUpdate();
							}));
		}

		private void btnSet_Click(object sender, EventArgs e) {
			var itemIndex = ShellView.GetFirstSelectedItemIndex();
			this.ShellView.CurrentRefreshedItemIndex = itemIndex;
			if (IsLibrary) {
				BExplorer.Shell.ShellLibrary lib = null;
				try {
					lib = BExplorer.Shell.ShellLibrary.Load(ShellView.GetFirstSelectedItem().DisplayName, false);
				}
				catch {
					lib = BExplorer.Shell.ShellLibrary.Load(ShellView.CurrentFolder.DisplayName, false);
				}
				lib.IconResourceId = new BExplorer.Shell.Interop.IconReference(tbLibrary.Text, (int)lvIcons.SelectedItems[0].Tag);
				lib.Close();

				var item = ShellView.Items[itemIndex];
				item.IsIconLoaded = false;
				ShellView.RefreshItem(ShellView.GetFirstSelectedItemIndex(), true);
			}
			else {
				ShellView.SetFolderIcon(ShellView.GetFirstSelectedItem().ParsingName, tbLibrary.Text, (int)lvIcons.SelectedItems[0].Tag);
			}
		}

		private BackgroundWorker bw = new BackgroundWorker();

		private void LoadLib() {
			bw.DoWork += new DoWorkEventHandler(bw_DoWork);
			bw.WorkerReportsProgress = true;
			bw.WorkerSupportsCancellation = true;
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			pbProgress.Visible = true;
			if (bw.IsBusy) {
				bw.CancelAsync();
			}
			bw.RunWorkerAsync();
		}

		private void IconView_Load(object sender, EventArgs e) {
			LoadLib();
		}

		private void tbLibrary_KeyUp(object sender, KeyEventArgs e) {
			if (e.KeyData == Keys.Enter) {
				LoadLib();
			}
		}
	}

	public class ListView : System.Windows.Forms.ListView {

		public ListView() {
			//Activate double buffering
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

			//Enable the OnNotifyMessage event so we get a chance to filter out
			// Windows messages before they get to the form's WndProc
			this.SetStyle(ControlStyles.EnableNotifyMessage, true);
		}

		protected override void OnNotifyMessage(Message m) {
			//Filter out the WM_ERASEBKGND message
			if (m.Msg != 0x14) {
				base.OnNotifyMessage(m);
			}
		}
	}
}