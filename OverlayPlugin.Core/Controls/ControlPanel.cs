﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Threading;

namespace RainbowMage.OverlayPlugin
{
    public partial class ControlPanel : UserControl
    {
        PluginMain pluginMain;
        PluginConfig config;

        public ControlPanel(PluginMain pluginMain, PluginConfig config)
        {
            InitializeComponent();
            tableLayoutPanel0.PerformLayout();

            this.pluginMain = pluginMain;
            this.config = config;

            this.checkBoxAutoHide.Checked = this.config.HideOverlaysWhenNotActive;
            this.menuFollowLatestLog.Checked = this.config.FollowLatestLog;
            
            PluginMain.Logger.RegisterListener(addLogEntry);
            PluginMain.AddonRegistered += (o, e) => InitializeOverlayConfigTabs();
            InitializeOverlayConfigTabs();
        }

        private void addLogEntry(LogEntry entry)
        {
            logBox.AppendText($"[{entry.Time}] {entry.Level}: {entry.Message}\r\n");
        }

        private void InitializeOverlayConfigTabs()
        {
            tabControl.TabPages.Clear();

            foreach (var source in this.pluginMain.EventSources)
            {
                AddConfigTab(source);
            }

            foreach (var overlay in this.pluginMain.Overlays)
            {
                AddConfigTab(overlay);
            }

            if (tabControl.TabCount == 0)
            {
                tabControl.TabPages.Add(this.tabPageMain);
            }
        }

        private void AddConfigTab(IOverlay overlay)
        {
            var tabPage = new TabPage
            {
                Name = overlay.Name,
                Text = overlay.GetType().Name
            };

            var addon = pluginMain.Addons.FirstOrDefault(x => x.OverlayType == overlay.GetType());
            if (addon != null)
            {
                var control = addon.CreateOverlayConfigControlInstance(overlay);
                if (control != null)
                {
                    control.Dock = DockStyle.Fill;
                    control.BackColor = SystemColors.ControlLightLight;
                    tabPage.Controls.Add(control);

                    this.tabControl.TabPages.Add(tabPage);
                    //this.tabControl.SelectTab(tabPage);
                }
            }
        }

        private void AddConfigTab(IEventSource source)
        {
            var tabPage = new TabPage
            {
                Name = source.Name,
                Text = "Event Source " + source.GetType().Name
            };

            var addon = pluginMain.Addons.FirstOrDefault(x => x.EventSourceType == source.GetType());
            if (addon != null)
            {
                var control = addon.CreateEventSourceControlInstance(source);
                if (control != null)
                {
                    control.Dock = DockStyle.Fill;
                    control.BackColor = SystemColors.ControlLightLight;
                    tabPage.Controls.Add(control);

                    this.tabControl.TabPages.Add(tabPage);
                    //this.tabControl.SelectTab(tabPage);
                }
            }
        }


        private void menuLogCopy_Click(object sender, EventArgs e)
        {
            /*if (listViewLog.SelectedIndices.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (int index in listViewLog.SelectedIndices)
                {
                    sb.AppendFormat(
                        "{0}: {1}: {2}",
                        PluginMain.Logger.Logs[index].Time,
                        PluginMain.Logger.Logs[index].Level,
                        PluginMain.Logger.Logs[index].Message);
                    sb.AppendLine();
                }
                Clipboard.SetText(sb.ToString());
            }*/
        }

        /*
        private void listViewLog_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= PluginMain.Logger.Logs.Count) 
            {
                e.Item = new ListViewItem("");
                e.Item.SubItems.Add("");
                e.Item.SubItems.Add("");
                return;
            };

            try
            {
                var log = PluginMain.Logger.Logs[e.ItemIndex];
                e.Item = new ListViewItem(log.Time.ToString() ?? "");
                e.Item.UseItemStyleForSubItems = true;
                e.Item.SubItems.Add(log.Level.ToString() ?? "");
                e.Item.SubItems.Add(log.Message ?? "Null");

                e.Item.ForeColor = Color.Black;
                if (log.Level == LogLevel.Warning)
                {
                    e.Item.BackColor = Color.LightYellow;
                }
                else if (log.Level == LogLevel.Error)
                {
                    e.Item.BackColor = Color.LightPink;
                }
                else
                {
                    e.Item.BackColor = Color.White;
                }
            } catch(Exception)
            {
                // We should log this but can't since it'd spam the log like crazy.
            }
        }*/

        private void menuFollowLatestLog_Click(object sender, EventArgs e)
        {
            this.config.FollowLatestLog = menuFollowLatestLog.Checked;
        }

        private void menuClearLog_Click(object sender, EventArgs e)
        {
            PluginMain.Logger.Logs.Clear();
        }

        private void menuCopyLogAll_Click(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            foreach (var log in PluginMain.Logger.Logs)
            {
                sb.AppendFormat(
                    "{0}: {1}: {2}",
                    log.Time,
                    log.Level,
                    log.Message);
                sb.AppendLine();
            }
            Clipboard.SetText(sb.ToString());
        }

        private void buttonNewOverlay_Click(object sender, EventArgs e)
        {
            var newOverlayDialog = new NewOverlayDialog(pluginMain);
            newOverlayDialog.NameValidator = (name) =>
                {
                    // 空もしくは空白文字のみの名前は許容しない
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        MessageBox.Show("Name must not be empty or white space only.");
                        return false;
                    }
                    // 名前の重複も許容しない
                    else if (config.Overlays.Where(x => x.Name == name).Any())
                    {
                        MessageBox.Show("Name should be unique.");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                };
            
            if (newOverlayDialog.ShowDialog(this.ParentForm) == DialogResult.OK)
            {
                if (this.tabControl.TabCount == 1 && this.tabControl.TabPages[0].Equals(this.tabPageMain))
                {
                    this.tabControl.TabPages.Remove(this.tabPageMain);
                }
                CreateAndRegisterOverlay(newOverlayDialog.SelectedOverlayType, newOverlayDialog.OverlayName);
            }
            
            newOverlayDialog.Dispose();
        }

        private IOverlay CreateAndRegisterOverlay(IOverlayAddonV2 overlayType, string name)
        {
            var config = overlayType.CreateOverlayConfigInstance(name);
            this.config.Overlays.Add(config);

            var overlay = overlayType.CreateOverlayInstance(config);
            pluginMain.RegisterOverlay(overlay);

            AddConfigTab(overlay);

            return overlay;
        }

        private void buttonRemoveOverlay_Click(object sender, EventArgs e)
        {
            if (this.tabControl.SelectedTab.Equals(this.tabPageMain))
                return;

            if (tabControl.SelectedTab == null) // ???
                tabControl.SelectedTab = tabControl.TabPages[0];

            var subLabel = tabControl.SelectedTab.Text;
            if (subLabel.Length > 13 && subLabel.Substring(0, 13) == "Event Source ")
            {
                return;
            }

            string selectedOverlayName = tabControl.SelectedTab.Name;
            int selectedOverlayIndex = tabControl.TabPages.IndexOf(tabControl.SelectedTab);

            // コンフィグ削除
            var configs = this.config.Overlays.Where(x => x.Name == selectedOverlayName);
            foreach (var config in configs.ToArray())
            {
                this.config.Overlays.Remove(config);
            }

            // 動作中のオーバーレイを停止して削除
            var overlays = this.pluginMain.Overlays.Where(x => x.Name == selectedOverlayName);
            foreach (var overlay in overlays)
            {
                overlay.Dispose();
            }
            foreach (var overlay in overlays.ToArray())
            {
                this.pluginMain.Overlays.Remove(overlay);
            }

            // タブページを削除
            this.tabControl.TabPages.RemoveByKey(selectedOverlayName);

            // タープカントロールが
            if (this.tabControl.TabCount == 0)
            {
                this.tabControl.TabPages.Add(this.tabPageMain);
            }
            // 
            if (selectedOverlayIndex > 0)
            {
                this.tabControl.SelectTab(selectedOverlayIndex - 1);
            }

            // タープを更新
            this.tabControl.Update();
        }

        private void checkBoxAutoHide_CheckedChanged(object sender, EventArgs e)
        {
            config.HideOverlaysWhenNotActive = checkBoxAutoHide.Checked;
        }
    }
}