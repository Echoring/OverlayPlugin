﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using Advanced_Combat_Tracker;
using Newtonsoft.Json.Linq;
using RainbowMage.OverlayPlugin.Updater;
using SharpCompress.Archives.Zip;

namespace RainbowMage.OverlayPlugin
{
    public partial class WSConfigPanel : UserControl
    {
        const string MKCERT_DOWNLOAD = "https://github.com/FiloSottile/mkcert/releases/download/v1.4.3/mkcert-v1.4.3-windows-amd64.exe";
        const string NGROK_CHOCO_SCRIPT = "https://raw.githubusercontent.com/ngrok/choco-ngrok/main/tools/chocolateyinstall.ps1";

        IPluginConfig _config;
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage",
            "CA2213:Disposable fields should be disposed",
            Justification = "_server is disposed of by TinyIoCContainer")]
        WSServer _server;
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage",
            "CA2213:Disposable fields should be disposed",
            Justification = "_plugin is disposed of by TinyIoCContainer")]
        PluginMain _plugin;
        Registry _registry;
        Process _ngrok;
        string _ngrokPrefix = null;

        private enum TunnelStatus
        {
            Unknown,
            Inactive,
            Downloading,
            Launching,
            Active,
            Error
        };

        public WSConfigPanel(TinyIoCContainer container)
        {
            InitializeComponent();

            _config = container.Resolve<IPluginConfig>();
            _server = container.Resolve<WSServer>();
            _plugin = container.Resolve<PluginMain>();
            _registry = container.Resolve<Registry>();


            ipTxt.Text = _config.WSServerIP;
            portTxt.Text = "" + _config.WSServerPort;
            sslBox.Checked = _config.WSServerSSL;
            sslBox.Enabled = _server.IsSSLPossible();

            for (var i = 0; i < regionCb.Items.Count; i++)
            {
                if ((string)regionCb.Items[i] == _config.TunnelRegion)
                {
                    regionCb.SelectedIndex = i;
                    break;
                }
            }

            txtNgrokToken.Text = _config.Token;

            UpdateStatus(null, new WSServer.StateChangedArgs(_server.IsRunning(), _server.IsFailed()));
            _server.OnStateChanged += UpdateStatus;

            UpdateTunnelStatus(TunnelStatus.Inactive);

            lblUrlConfidentWarning.Visible = false;

            if (ActGlobals.oFormActMain != null)
            {
                ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.BackColorSettingChanged += ActColorSettings_ColorSettingChanged;
                ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.ForeColorSettingChanged += ActColorSettings_ColorSettingChanged;
                ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSettingChanged += ActColorSettings_ColorSettingChanged;
                ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSettingChanged += ActColorSettings_ColorSettingChanged;
                ActGlobals.oFormActMain.ActColorSettings.InvertColorsChanged += ActColorSettings_InvertColorsChanged;
                UpdateActColorSettings();
            }
        }

        private void ActColorSettings_InvertColorsChanged(object sender, EventArgs e)
        {
            UpdateActColorSettings();
        }
        private void ActColorSettings_ColorSettingChanged(Color NewColor)
        {
            UpdateActColorSettings();
        }
        private void UpdateActColorSettings()
        {
            this.BackColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.BackColorSetting;
            this.ForeColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.ForeColorSetting;
            // TabPages
            introPage.BackColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.BackColorSetting;
            settingsPage.BackColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.BackColorSetting;
            tunnelPage.BackColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.BackColorSetting;
            // GroupBoxes
            urlGeneratorBox.ForeColor = ActGlobals.oFormActMain.ActColorSettings.MainWindowColors.ForeColorSetting;
            if (ActGlobals.oFormActMain.ActColorSettings.InvertColors)
            {
                // TextBoxes
                txtNgrokToken.BorderStyle = BorderStyle.FixedSingle;
                txtOverlayUrl.BorderStyle = BorderStyle.FixedSingle;
                ipTxt.BorderStyle = BorderStyle.FixedSingle;
                portTxt.BorderStyle = BorderStyle.FixedSingle;
                // ComboBoxes
                cbOverlay.FlatStyle = FlatStyle.Flat;
                regionCb.FlatStyle = FlatStyle.Flat;
            }
            else
            {
                // TextBoxes
                txtNgrokToken.BorderStyle = BorderStyle.Fixed3D;
                txtOverlayUrl.BorderStyle = BorderStyle.Fixed3D;
                ipTxt.BorderStyle = BorderStyle.Fixed3D;
                portTxt.BorderStyle = BorderStyle.Fixed3D;
                // ComboBoxes
                cbOverlay.FlatStyle = FlatStyle.Standard;
                regionCb.FlatStyle = FlatStyle.Standard;
            }
            // Text Boxes
            txtNgrokToken.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            txtNgrokToken.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            txtOverlayUrl.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            txtOverlayUrl.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            ipTxt.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            ipTxt.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            portTxt.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            portTxt.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            logDisplay.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            logDisplay.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            simpLogBox.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            simpLogBox.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            // ComboBoxes
            cbOverlay.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            cbOverlay.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
            regionCb.BackColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.BackColorSetting;
            regionCb.ForeColor = ActGlobals.oFormActMain.ActColorSettings.InternalWindowColors.ForeColorSetting;
        }

        public void Stop()
        {
            if (_ngrok != null && !_ngrok.HasExited)
                _ngrok.Kill();
        }

        private void UpdateStatus(object sender, WSServer.StateChangedArgs e)
        {
            startBtn.Enabled = true;
            stopBtn.Enabled = false;

            if (e.Running)
            {
                statusLabel.Text = "Running";
                statusLabel.ForeColor = Color.ForestGreen;

                startBtn.Enabled = false;
                stopBtn.Enabled = true;
            }
            else if (e.Failed)
            {
                statusLabel.Text = "Failed";
                statusLabel.ForeColor = Color.DarkRed;
            }
            else
            {
                statusLabel.Text = "Stopped";
                statusLabel.ForeColor = Color.Gray;
            }
        }

        private void UpdateTunnelStatus(TunnelStatus status)
        {
            switch (status)
            {
                case TunnelStatus.Unknown:
                    simpStatusLbl.Text = "Unknown";
                    simpStatusLbl.ForeColor = Color.Gray;

                    simpStartBtn.Enabled = false;
                    simpStopBtn.Enabled = false;
                    break;

                case TunnelStatus.Downloading:
                    simpStatusLbl.Text = "Downloading client...";
                    simpStatusLbl.ForeColor = Color.CornflowerBlue;

                    simpStartBtn.Enabled = false;
                    simpStopBtn.Enabled = false;
                    break;

                case TunnelStatus.Launching:
                    simpStatusLbl.Text = "Launching...";
                    simpStatusLbl.ForeColor = Color.CornflowerBlue;

                    simpStartBtn.Enabled = false;
                    simpStopBtn.Enabled = false;
                    break;

                case TunnelStatus.Active:
                    simpStatusLbl.Text = "Active";
                    simpStatusLbl.ForeColor = Color.ForestGreen;

                    simpStartBtn.Enabled = false;
                    simpStopBtn.Enabled = true;
                    break;

                case TunnelStatus.Inactive:
                    simpStatusLbl.Text = "Inactive";
                    simpStatusLbl.ForeColor = Color.Gray;

                    simpStartBtn.Enabled = true;
                    simpStopBtn.Enabled = false;
                    break;

                case TunnelStatus.Error:
                    simpStatusLbl.Text = "Error";
                    simpStatusLbl.ForeColor = Color.DarkRed;

                    simpStartBtn.Enabled = true;
                    simpStopBtn.Enabled = false;
                    break;
            }
        }

        private void startBtn_Click(object sender, EventArgs e)
        {
            _config.WSServerRunning = true;
            _server.Start();
        }

        private void stopBtn_Click(object sender, EventArgs e)
        {
            _config.WSServerRunning = false;
            _server.Stop();
        }

        private void genSslBtn_Click(object sender, EventArgs e)
        {
            genSslBtn.Enabled = false;
            logDisplay.Text = "Generating SSL Certificate. Please wait...\r\n";

            Task.Run(GenSsl);
        }

        private void GenSsl()
        {
            try
            {
                var mkcertPath = Path.Combine(_plugin.PluginDirectory, "mkcert.exe");

                if (!File.Exists(mkcertPath))
                {
                    logDisplay.AppendText("Downloading mkcert...\r\n");

                    try
                    {
                        HttpClientWrapper.Get(MKCERT_DOWNLOAD, new Dictionary<string, string>(), mkcertPath, null, false);
                    }
                    catch (Exception e)
                    {
                        logDisplay.AppendText(string.Format("\nFailed: {0}", e));
                        genSslBtn.Enabled = true;
                        return;
                    }
                }

                logDisplay.AppendText("Installing CA...\r\n");
                if (!RunLogCmd(mkcertPath, "-install"))
                {
                    logDisplay.AppendText("\r\nFailed!\r\n");
                    genSslBtn.Enabled = true;
                    return;
                }

                logDisplay.AppendText("Generating certificate...\r\n");
                if (!RunLogCmd(mkcertPath, string.Format("-pkcs12 -p12-file \"{0}\" localhost 127.0.0.1 ::1", _server.GetCertPath())))
                {
                    logDisplay.AppendText("\r\nFailed!\r\n");
                    genSslBtn.Enabled = true;
                    return;
                }

                logDisplay.AppendText("\r\nDone.\r\n");

                sslBox.Enabled = _server.IsSSLPossible();
                sslBox.Checked = sslBox.Enabled;
                _config.WSServerSSL = sslBox.Enabled;
                genSslBtn.Enabled = true;
            }
            catch (Exception e)
            {
                logDisplay.AppendText(string.Format("\r\nException: {0}", e));
                genSslBtn.Enabled = true;
            }
        }

        private bool RunLogCmd(string file, string args)
        {
            using (var p = new Process())
            {
                p.StartInfo.FileName = file;
                p.StartInfo.Arguments = args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardOutput = true;

                DataReceivedEventHandler showLine = (sender, e) =>
                {
                    if (e.Data != null) logDisplay.AppendText(e.Data.Replace("\n", "\r\n") + "\r\n");
                };

                p.OutputDataReceived += showLine;
                p.ErrorDataReceived += showLine;

                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                p.WaitForExit(10000);

                return p.ExitCode == 0;
            }
        }

        private void sslBox_CheckedChanged(object sender, EventArgs e)
        {
            _config.WSServerSSL = sslBox.Checked;
        }

        private void portTxt_Leave(object sender, EventArgs e)
        {
            var valid = true;
            int port = 0;
            try
            {
                port = int.Parse(portTxt.Text);
            }
            catch
            {
                valid = false;
            }

            if (valid && (port < 1 || port > 65535))
            {
                valid = false;
            }

            if (valid)
            {
                _config.WSServerPort = port;
            }
            else
            {
                MessageBox.Show(
                    string.Format("{0} is not a valid port. Should be a number between 1 and 65535.", portTxt.Text),
                    "Invalid Port",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                    );
            }
        }

        private void ipTxt_Leave(object sender, EventArgs e)
        {
            IPAddress addr = null;
            if (ipTxt.Text == "*" || IPAddress.TryParse(ipTxt.Text, out addr))
            {
                _config.WSServerIP = ipTxt.Text;
            }
            else
            {
                MessageBox.Show(
                    string.Format("{0} is not a IP address.", ipTxt.Text),
                    "Invalid Address",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                    );
            }
        }

        public void RebuildOverlayOptions()
        {
            cbOverlay.Items.Clear();

            foreach (var preset in _registry.OverlayPresets)
            {
                cbOverlay.Items.Add(new
                {
                    label = preset.Name,
                    preset
                });
            }
        }

        private void cbOverlay_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbOverlay.SelectedIndex == -1) return;

            var item = cbOverlay.Items[cbOverlay.SelectedIndex];
            var preset = (IOverlayPreset)item.GetType().GetProperty("preset").GetValue(item);
            if (preset == null) return;

            var hostUrl = "";
            if (_ngrokPrefix != null)
            {
                hostUrl += _ngrokPrefix;
            }
            else
            {
                if (_config.WSServerSSL)
                {
                    hostUrl += "wss://";
                }
                else
                {
                    hostUrl += "ws://";
                }

                if (_config.WSServerIP == "0.0.0.0" || _config.WSServerIP == "*")
                {
                    hostUrl += "127.0.0.1";
                }
                else if (_config.WSServerIP == "[::]")
                {
                    hostUrl += "[::1]";
                }
                else
                {
                    hostUrl += _config.WSServerIP;
                }
                hostUrl += ":" + _config.WSServerPort;
            }

#if DEBUG
            var resourcesPath = "file:///" + _plugin.PluginDirectory.Replace('\\', '/') + "/libs/resources";
#else
            var resourcesPath = "file:///" + _plugin.PluginDirectory.Replace('\\', '/') + "/resources";
#endif

            var url = preset.Url.Replace("\\", "/").Replace("%%", resourcesPath);
            UriBuilder uri = new UriBuilder(url);
            NameValueCollection query_params = HttpUtility.ParseQueryString(uri.Query);

            if (preset.Supports.Contains("modern"))
            {
                query_params.Add("OVERLAY_WS", hostUrl + "/ws");
            }
            else if (preset.Supports.Contains("actws"))
            {
                query_params.Add("HOST_PORT", hostUrl + "/");
            }
            else
            {
                url = "";
            }

            uri.Query = HttpUtility.UrlDecode(query_params.ToString());

            if ((uri.Port == 443 && uri.Scheme == "https") || (uri.Port == 80 && uri.Scheme == "http"))
            {
                uri.Port = -1;
            }

            txtOverlayUrl.Text = (url != "") ? uri.ToString() : url;
        }

        private void txtOverlayUrl_Click(object sender, EventArgs e)
        {
            txtOverlayUrl.SelectAll();
            txtOverlayUrl.Copy();
        }

        private class ShowLineArgs : EventArgs
        {
            public string Data { get; private set; }
            public ShowLineArgs(string d)
            {
                Data = d;
            }
        }

        private void simpStartBtn_Click(object sender, EventArgs e)
        {
            simpStartBtn.Enabled = false;

            // Named helper function to allow recursion/retry
            void StartTunnel()
            {
                try
                {
                    var ngrokPath = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "ngrok-" + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + ".exe");
                    var ngrokConfigPath = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "ngrok.yml");

                    if (File.Exists(ngrokConfigPath))
                    {
                        var oldConfig = File.ReadAllText(ngrokConfigPath);
                        if (!Regex.IsMatch(oldConfig, "^version: 2$", RegexOptions.Multiline))
                        {
                            simpLogBox.AppendText("Old config file found. Updating ngrok...\r\n");
                            File.Delete(ngrokPath);
                            File.Delete(ngrokConfigPath);
                        }
                    }

                    var fetchedNgrok = false; // Used to prevent infinite recursion in case ngrok reports too old version but redownloading doesn't fix it
                    if (!File.Exists(ngrokPath))
                    {
                        if (!FetchNgrok(ngrokPath))
                        {
                            UpdateTunnelStatus(TunnelStatus.Error);
                            return;
                        }
                        fetchedNgrok = true;
                    }

                    UpdateTunnelStatus(TunnelStatus.Launching);

                    if (_ngrok != null && !_ngrok.HasExited)
                    {
                        simpLogBox.AppendText("Detected left over ngrok process. Performing cleanup...\r\n");
                        _ngrok.Kill();
                        _ngrok = null;
                    }

                    // Enforce sensible settings
                    ipTxt.Text = "127.0.0.1";
                    sslBox.Checked = false;

                    _config.WSServerIP = "127.0.0.1";
                    _config.WSServerSSL = false;

                    if (_config.WSServerPort < 1024)
                    {
                        portTxt.Text = "10501";
                        _config.WSServerPort = 10501;
                    }

                    simpLogBox.AppendText("Launching WSServer...\r\n");
                    _config.WSServerRunning = true;
                    _server.Start();

                    simpLogBox.AppendText("Launching ngrok...\r\n");

                    var region = _config.TunnelRegion;
                    if (region == null)
                    {
                        region = "us";
                    }
                    else
                    {
                        region = region.Split(' ')[0];
                    }

                    var config = @"
authtoken: " + (this.txtNgrokToken.Text) + @"
region: " + region + @"
console_ui: false
web_addr: 127.0.0.1:" + (_config.WSServerPort + 1) + @"
tunnels:
    wsserver:
        proto: http
        addr: 127.0.0.1:" + _config.WSServerPort + @"
        inspect: false
        schemes:
            - https
version: 2
";
                    File.WriteAllText(ngrokConfigPath, config);

                    var p = new Process();
                    p.StartInfo.FileName = ngrokPath;
                    p.StartInfo.Arguments = "start --config=\"" + ngrokConfigPath + "\" wsserver";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.StartInfo.RedirectStandardOutput = true;

                    var ngrokTooOld = false;

                    DataReceivedEventHandler showLine = (_, ev) =>
                    {
                        if (ev.Data != null)
                        {
                            simpLogBox.AppendText(ev.Data.Replace("\n", "\r\n") + "\r\n");
                            // From https://ngrok.com/docs/errors/
                            // ERR_NGROK_120  Your ngrok agent version "<VERSION>" is no longer supported.
                            // ERR_NGROK_121  Your ngrok agent version "<VERSION>" is too old.
                            if (Regex.IsMatch(ev.Data, "\\bERR_NGROK_12[01]\\b"))
                            {
                                ngrokTooOld = true;
                            }
                        }
                    };

                    p.OutputDataReceived += showLine;
                    p.ErrorDataReceived += showLine;

                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (p.WaitForExit(3000))
                    {
                        // Ensure OutputDataReceived and ErrorDataReceived have finished processing so that ngrokTooOld is reliable
                        p.WaitForExit();
                        simpLogBox.AppendText("ngrok crashed!\r\n");
                        UpdateTunnelStatus(TunnelStatus.Error);
                        if (ngrokTooOld)
                        {
                            if (!fetchedNgrok)
                            {
                                simpLogBox.AppendText("ngrok version is too old!\r\n");
                                simpLogBox.AppendText("Deleting old version and retrying...\r\n");
                                File.Delete(ngrokPath);
                                // The inner StartTunnel call will try to download and either succeed and set fetchedNgrok = true,
                                // or fail and not reach here, either way avoiding infinite recursion.
                                StartTunnel();
                            }
                            else
                            {
                                simpLogBox.AppendText("Downloaded ngrok version is too old. Please notify OverlayPlugin devs.\r\n");
                            }
                        }
                        return;
                    }

                    _ngrok = p;

                    var apiUrl = "http://127.0.0.1:" + (_config.WSServerPort + 1) + "/api/tunnels";
                    var headers = new Dictionary<string, string>()
                    {
                        { "Accept", "application/json" },
                    };

                    string data = null;
                    while (true)
                    {
                        try
                        {
                            data = HttpClientWrapper.Get(apiUrl, headers, null, null, false);
                            break;
                        }
                        catch (HttpClientException ex)
                        {
                            if (!ex.Retry)
                            {
                                simpLogBox.AppendText(string.Format("Failed: {0}\r\n", ex));
                                UpdateTunnelStatus(TunnelStatus.Error);
                                return;
                            }
                            else
                            {
                                Thread.Sleep(500);
                            }
                        }
                        catch (Exception ex)
                        {
                            simpLogBox.AppendText(string.Format("Failed: {0}\r\n", ex));
                            UpdateTunnelStatus(TunnelStatus.Error);
                            return;
                        }
                    }

                    var tunnels = JObject.Parse(data);
                    var done = false;
                    foreach (var tun in tunnels["tunnels"])
                    {
                        if (tun["name"] != null && tun["name"].ToString() == "wsserver" && tun["public_url"] != null)
                        {
                            _ngrokPrefix = tun["public_url"].ToString().Replace("https://", "wss://");

                            // Update the generated URL box
                            cbOverlay_SelectedIndexChanged(null, null);
                            done = true;
                            break;
                        }
                    }

                    if (done)
                    {
                        simpLogBox.AppendText("Done!\r\n");
                        simpLogBox.AppendText("\r\n#############################################\r\nUse the URL Generator below to generate URLs for you.\r\n\r\n");
                        simpLogBox.AppendText("\r\nIf you know what you're doing or you're using an overlay that isn't listed, here are some query strings for you:\r\n");
                        simpLogBox.AppendText("\r\n    ?HOST_PORT=" + _ngrokPrefix + "\r\n    ?OVERLAY_WS=" + _ngrokPrefix + "/ws\r\n");
                        simpLogBox.AppendText("#############################################\r\n");

                        UpdateTunnelStatus(TunnelStatus.Active);

                        p.Exited += (_, ev) =>
                        {
                            UpdateTunnelStatus(TunnelStatus.Error);
                        };
                    }
                    else
                    {
                        simpLogBox.AppendText("Failed!\r\n");
                        UpdateTunnelStatus(TunnelStatus.Error);
                    }
                }
                catch (Exception ex)
                {
                    simpLogBox.AppendText(string.Format("\r\nUncaught exception: {0}\r\n\r\n", ex));
                    UpdateTunnelStatus(TunnelStatus.Error);
                }
            }

            Task.Run(StartTunnel);
        }

        private bool NgrokProgressCallback(long resumed, long dltotal, long dlnow, long ultotal, long ulnow)
        {
            var percent = ((float)resumed + dlnow) / ((float)resumed + dltotal);
            var dlMib = ((resumed + dlnow) / 1024 / 1024);
            var totalMib = ((resumed + dltotal) / 1024 / 1024);

            // Avoid NaN%
            if (dlMib == 0) percent = 0;

            var lines = simpLogBox.Lines;
            lines[lines.Length - 1] = Math.Round(percent * 100, 2) + "% (" + dlMib + " MiB / " + totalMib + " MiB)";
            simpLogBox.Lines = lines;

            return false;
        }

        private bool FetchNgrok(string ngrokPath)
        {
            try
            {
                UpdateTunnelStatus(TunnelStatus.Downloading);

                simpLogBox.AppendText("Fetching latest ngrok version...\r\n");
                string ngrokScript;
                try
                {
                    ngrokScript = HttpClientWrapper.Get(NGROK_CHOCO_SCRIPT);
                }
                catch (Exception e)
                {
                    simpLogBox.AppendText(string.Format("\r\nFailed: {0}\r\n\r\n", e));
                    return false;
                }

                //  url           = 'https://bin.equinox.io/a/dpwUDwNBzwV/ngrok-v3-3.3.0-windows-386.zip'
                //  url64bit      = 'https://bin.equinox.io/a/516AtQ83xaN/ngrok-v3-3.3.0-windows-amd64.zip'
                var urlKey = Environment.Is64BitOperatingSystem ? "url64bit" : "url";
                var match = Regex.Match(ngrokScript, "^\\s*" + urlKey + "\\s*=\\s*'(https://[^']+)'\\s*$", RegexOptions.Multiline);
                if (match == Match.Empty)
                {
                    simpLogBox.AppendText("Failed to find download URL in ngrok install script! Please notify OverlayPlugin devs.\r\n");
                    return false;
                }
                var ngrokUrl = match.Groups[1].Captures[0].Value;

                simpLogBox.AppendText(string.Format("Downloading ngrok client from {0}\r\n", ngrokUrl));
                try
                {
                    HttpClientWrapper.Get(ngrokUrl, new Dictionary<string, string>(), ngrokPath + ".zip", NgrokProgressCallback, false);
                }
                catch (Exception e)
                {
                    simpLogBox.AppendText(string.Format("\r\nFailed: {0}\r\n\r\n", e));
                    return false;
                }

                simpLogBox.AppendText("\r\nExtracting ngrok client...\r\n");
                try
                {
                    using (var archive = ZipArchive.Open(ngrokPath + ".zip"))
                    using (var reader = archive.ExtractAllEntries())
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (reader.Entry.Key == "ngrok.exe")
                            {
                                using (var writer = File.OpenWrite(ngrokPath))
                                {
                                    reader.WriteEntryTo(writer);
                                }
                                break;
                            }
                        }
                    }

                    File.Delete(ngrokPath + ".zip");
                }
                catch (Exception e)
                {
                    simpLogBox.AppendText(string.Format("\r\n{0}\r\n\r\n", e));
                }

                if (!File.Exists(ngrokPath))
                {
                    simpLogBox.AppendText("\r\nExtraction failed!\r\n");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                simpLogBox.AppendText(string.Format("\r\nException: {0}\r\n\r\n", e));
                return false;
            }
        }

        private void simpStopBtn_Click(object sender, EventArgs e)
        {
            try
            {
                simpLogBox.AppendText("\r\nStopping tunnel...\r\n");

                if (_ngrok != null && !_ngrok.HasExited)
                {
                    _ngrok.Kill();
                    _ngrok = null;
                }

                _ngrokPrefix = null;

                simpLogBox.AppendText("Stopping WSServer...\r\n");
                if (_server.IsRunning())
                {
                    _server.Stop();
                }

                simpLogBox.AppendText("Done!\r\n");
                UpdateTunnelStatus(TunnelStatus.Inactive);
            }
            catch (Exception ex)
            {
                simpLogBox.AppendText(string.Format("\r\nFailed: {0}\r\n\r\n", ex));
                UpdateTunnelStatus(TunnelStatus.Error);
            }
        }

        private void regionCb_SelectedIndexChanged(object sender, EventArgs e)
        {
            _config.TunnelRegion = (string)regionCb.SelectedItem;
        }

        private void txtNgrokTokenChanged(object sender, EventArgs e)
        {
            _config.Token = txtNgrokToken.Text;
        }
    }
}
