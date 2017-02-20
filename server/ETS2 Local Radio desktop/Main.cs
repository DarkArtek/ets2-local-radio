﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Ets2SdkClient;
using ETS2_Local_Radio_server.Properties;
using Gma.System.MouseKeyHook;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ETS2_Local_Radio_server
{
    public partial class Main : Form
    {
        public Ets2SdkTelemetry Telemetry;

        public SimpleHTTPServer myServer;

        private IKeyboardMouseEvents m_GlobalHook;
        private SimpleJoystick joystick;
        private bool[] previousState;

        public int amount = 0;

        public static Coordinates coordinates;

        public static object ets2data;
        public static Commands commandsData;

        public static string simulatorNotRunning = "Simulator not yet running";
        public static string simulatorNotDriving = "Simulator running, let's get driving!";
        public static string simulatorRunning = "Simulator running!";

        public static string installOverlay =
            "Do you want to install the in-game overlay?\n(This will overwrite an already existing d3d9.dll, and it may in rare cases cause the game to crash when exiting the game)";

        public static string currentGame = "ets2";

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            Log.Clear();

            //Global keyboard hook logic by https://github.com/gmamaladze/globalmousekeyhook/blob/vNext/Demo/Main.cs
            Subscribe();

            //Add Firewall exception
            AddException();

            //Load languages to combobox:
            LoadLanguages();

            //Check plugins:
            if (PluginExists("ats"))
            {
                installAtsButton.Image = Resources.check;
            }
            if (PluginExists("ets2"))
            {
                installEts2Button.Image = Resources.check;
            }
            if (!PluginExists("ats") && !PluginExists("ets2"))
            {
                groupInfo.Enabled = false;
                groupSettings.Enabled = false;
            }

            //Load the keys:
            nextKeyTextBox.Text = ConfigurationManager.AppSettings["NextKey"];
            previousKeyTextBox.Text = ConfigurationManager.AppSettings["PreviousKey"];
            stopKeyTextBox.Text = ConfigurationManager.AppSettings["StopKey"];
            volumeUpKeyTextBox.Text = ConfigurationManager.AppSettings["VolumeUpKey"];
            volumeDownKeyTextBox.Text = ConfigurationManager.AppSettings["VolumeDownKey"];
            makeFavouriteKeyTextbox.Text = ConfigurationManager.AppSettings["MakeFavouriteKey"];

            nextButtonTextBox.Text = ConfigurationManager.AppSettings["NextButton"];
            previousButtonTextBox.Text = ConfigurationManager.AppSettings["PreviousButton"];
            stopButtonTextBox.Text = ConfigurationManager.AppSettings["StopButton"];
            volumeUpButtonTextBox.Text = ConfigurationManager.AppSettings["VolumeUpButton"];
            volumeDownButtonTextBox.Text = ConfigurationManager.AppSettings["VolumeDownButton"];
            makeFavouriteButtonTextbox.Text = ConfigurationManager.AppSettings["MakeFavouriteButton"];

            comboController.SelectedText = ConfigurationManager.AppSettings["Controller"];

            //Load favourites
            Favourites.Load();

            //Start telemetry grabbing:
            Telemetry = new Ets2SdkTelemetry(250);
            Telemetry.Data += Telemetry_Data;

            if (Telemetry.Error != null)
            {
                MessageBox.Show(
                    "General info:\r\nFailed to open memory map " + Telemetry.Map +
                        " - on some systems you need to run the client (this app) with elevated permissions, because e.g. you're running Steam/ETS2 with elevated permissions as well. .NET reported the following Exception:\r\n" +
                        Telemetry.Error.Message + "\r\n\r\nStacktrace:\r\n" + Telemetry.Error.StackTrace);
            }

            //Open server:
            myServer = new SimpleHTTPServer(Directory.GetCurrentDirectory() + "\\web", Int32.Parse(ConfigurationManager.AppSettings["Port"]));
            writeFile("none", "0", "0");

            //Load IP addresses:
            LoadAddresses();

            if (AttachJoystick())
            {
                foreach (var item in joystick.AvailableDevices)
                {
                    comboController.Items.Add(item.InstanceName);
                }
            }

            currentGameTimer.Start();

            //Add handlers:
            nextKeyTextBox.KeyDown += keyInput;
            previousKeyTextBox.KeyDown += keyInput;
            stopKeyTextBox.KeyDown += keyInput;
            volumeUpKeyTextBox.KeyDown += keyInput;
            volumeDownKeyTextBox.KeyDown += keyInput;
            makeFavouriteKeyTextbox.KeyDown += keyInput;

            //Remove key binding:
            nextKeyTextBox.KeyDown += removeBinding;
            previousKeyTextBox.KeyDown += removeBinding;
            stopKeyTextBox.KeyDown += removeBinding;
            volumeUpKeyTextBox.KeyDown += removeBinding;
            volumeDownKeyTextBox.KeyDown += removeBinding;
            makeFavouriteKeyTextbox.KeyDown += removeBinding;

            nextButtonTextBox.KeyDown += removeBinding;
            previousButtonTextBox.KeyDown += removeBinding;
            stopButtonTextBox.KeyDown += removeBinding;
            volumeUpButtonTextBox.KeyDown += removeBinding;
            volumeDownButtonTextBox.KeyDown += removeBinding;
            makeFavouriteButtonTextbox.KeyDown += removeBinding;
        }

        private void LoadLanguages()
        {
            try
            {
                if (!Directory.Exists(Directory.GetCurrentDirectory() + "\\web\\lang"))
                {
                    throw new Exception("\\web\\lang directory not found");
                }
                foreach (string file in Directory.GetFiles(Directory.GetCurrentDirectory() + "\\web\\lang"))
                {
                    if (file.EndsWith(".json"))
                    {
                        comboLang.Items.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
                if (ConfigurationManager.AppSettings["Language"] != null)
                {
                    comboLang.Text = ConfigurationManager.AppSettings["Language"];
                }
                else
                {
                    comboLang.Text = "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private bool PluginExists(string game)
        {
            string folder = "";
            if (game == "ets2")
            {
                folder = ConfigurationManager.AppSettings["Ets2Folder"];
            }
            if (game == "ats")
            {
                folder = ConfigurationManager.AppSettings["AtsFolder"];
            }
            try
            {
                if (folder != null)
                {
                    if (Directory.Exists(folder + @"\bin\win_x86\plugins") &&
                        Directory.Exists(folder + @"\bin\win_x64\plugins"))
                    {
                        if (
                            File.Exists(folder + @"\bin\win_x86\plugins\ets2-telemetry.dll") &&
                            File.Exists(folder + @"\bin\win_x64\plugins\ets2-telemetry.dll"))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private bool ChooseFolder(string game)
        {
            if (folderDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    DialogResult result = MessageBox.Show(installOverlay, "ETS2 Local Radio server",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Cancel)
                    {
                        string folder = folderDialog.SelectedPath;
                        Directory.CreateDirectory(folder + @"\bin\win_x86\plugins");
                        Directory.CreateDirectory(folder + @"\bin\win_x64\plugins");

                        File.Copy(Directory.GetCurrentDirectory() + @"\plugins\bin\win_x86\plugins\ets2-telemetry.dll",
                            folder + @"\bin\win_x86\plugins\ets2-telemetry.dll", true);
                        File.Copy(Directory.GetCurrentDirectory() + @"\plugins\bin\win_x64\plugins\ets2-telemetry.dll",
                            folder + @"\bin\win_x64\plugins\ets2-telemetry.dll", true);
                        if (result == DialogResult.Yes)
                        {
                            File.Copy(Directory.GetCurrentDirectory() + @"\plugins\bin\win_x86\d3d9.dll",
                                folder + @"\bin\win_x86\d3d9.dll", true);
                            File.Copy(Directory.GetCurrentDirectory() + @"\plugins\bin\win_x64\d3d9.dll",
                                folder + @"\bin\win_x64\d3d9.dll", true);
                        }

                        if (game == "ets2")
                        {
                            SaveAppSettings("Ets2Folder", folder);
                        }
                        if (game == "ats")
                        {
                            SaveAppSettings("AtsFolder", folder);
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void LoadAddresses()
        {
            IPHostEntry host;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    comboIP.Items.Add("http://" + ip.ToString() + ":" + ConfigurationManager.AppSettings["Port"]);
                }
            }
            comboIP.SelectedIndex = 0;
        }

        private bool AttachJoystick()
        {
            try
            {
                //Initialise joystick:
                string name = null;
                if (ConfigurationManager.AppSettings["Controller"] != null)
                {
                    name = ConfigurationManager.AppSettings["Controller"];
                }
                joystick = new SimpleJoystick(name);
                //comboController.SelectedText = ConfigurationManager.AppSettings["Controller"];

                //Start joystick input timer:
                joystickTimer.Start();
                return true;
            }
            catch (Exception ex)
            {
                Log.Write(ex.ToString());
                return false;
            }
        }

        private void Telemetry_Data(Ets2Telemetry data, bool updated)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new TelemetryData(Telemetry_Data), new object[2] { data, updated });
                    return;
                }

                ets2data = data;
                coordinates = new Coordinates(data.Physics.CoordinateX, data.Physics.CoordinateY, data.Physics.CoordinateZ);
                locationLabel.Text = coordinates.X + "; " + coordinates.Y + "; " + coordinates.Z;

                if (data.Version.Ets2Major == 0)
                {
                    statusLabel.Text = simulatorNotRunning;
                    statusLabel.ForeColor = Color.Red;
                }
                else if (data.Time == 0)
                {
                    statusLabel.Text = simulatorNotDriving;
                    statusLabel.ForeColor = Color.DarkOrange;
                }
                else
                {
                    statusLabel.Text = simulatorRunning;
                    statusLabel.ForeColor = Color.DarkGreen;
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
            }
        }

        private static void DeleteException()
        {
            Process netsh = new Process();
            string arguments = "advfirewall firewall delete rule name=\"ETS2 Local Radio\" dir=in protocol=TCP localport=" + ConfigurationManager.AppSettings["Port"];
            netsh.StartInfo.FileName = "netsh";
            netsh.StartInfo.Arguments = arguments;
            netsh.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            netsh.Start();
        }

        private static void AddException()
        {
            DeleteException();
            // to prevent duplicates

            Process netsh = new Process();
            string arguments = "advfirewall firewall add rule name=\"ETS2 Local Radio\" dir=in action=allow protocol=TCP localport=" + ConfigurationManager.AppSettings["Port"];
            netsh.StartInfo.FileName = "netsh";
            netsh.StartInfo.Arguments = arguments;
            netsh.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            netsh.Start();
        }

        private void Main_FormClosing(object sender, EventArgs e)
        {
            try
            {
                //Global keyboard hook logic by https://github.com/gmamaladze/globalmousekeyhook/blob/vNext/Demo/Main.cs
                Favourites.Save();
                Unsubscribe();
                myServer.Stop();
                writeFile("none", "0", "0");
                DeleteException();
                joystickTimer.Stop();
                joystick.Release();
            }
            catch (Exception ex)
            {
                Log.Write(ex.ToString());
            }
            finally
            {
                Application.Exit();
            }
        }

        public void Subscribe()
        {
            // Note: for the application hook, use the Hook.AppEvents() instead
            m_GlobalHook = Hook.GlobalEvents();

            m_GlobalHook.KeyDown += GlobalHookKeyDown;
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (ConfigurationManager.AppSettings["PreviousKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["PreviousKey"], true))
                {
                    Console.WriteLine("Fired event PreviousKey");
                    keyTimeout.Stop();
                    amount--;
                    keyTimeout.Start();
                }

                if (ConfigurationManager.AppSettings["NextKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["NextKey"], true))
                {
                    Console.WriteLine("Fired event NextKey");
                    keyTimeout.Stop();
                    amount++;
                    keyTimeout.Start();
                }
                if (ConfigurationManager.AppSettings["StopKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["StopKey"], true))
                {
                    Console.WriteLine("Fired event StopKey");

                    writeFile("stop", "0");
                }
                if (ConfigurationManager.AppSettings["VolumeUpKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["VolumeUpKey"], true))
                {
                    Console.WriteLine("Fired event VolumeUpKey");

                    writeFile("volume", "5");
                }
                if (ConfigurationManager.AppSettings["VolumeDownKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["VolumeDownKey"], true))
                {
                    Console.WriteLine("Fired event VolumeDownKey");

                    writeFile("volume", "-5");
                }
                if (ConfigurationManager.AppSettings["MakeFavouriteKey"] != "" && e.KeyCode == (Keys)Enum.Parse(typeof(Keys), ConfigurationManager.AppSettings["MakeFavouriteKey"], true))
                {
                    Console.WriteLine("Fired event MakeFavouriteKey");

                    writeFile("favourite", "0");
                }
            }
            catch (Exception ex)
            {
                Log.Write(ex.ToString());
            }
        }

        public void Unsubscribe()
        {
            m_GlobalHook.KeyDown -= GlobalHookKeyDown;

            //It is recommened to dispose it
            m_GlobalHook.Dispose();
        }
        private void keyTimeout_Tick(object sender, EventArgs e)
        {
            keyTimeout.Stop();
            writeFile("next", amount.ToString());
            amount = 0;
            Console.WriteLine(amount);
        }

        private void writeFile(string action, string amount, string id = null)
        {
            if (id == null)
            {
                id = Guid.NewGuid().ToString("n");
            }

            Commands command = new Commands(id, action, amount, comboLang.SelectedItem.ToString());
            commandsData = command;
            //StreamWriter streamWriter = new StreamWriter(ConfigurationManager.AppSettings["Folder"] + "\\commands.json");
            //streamWriter.WriteLine(json);
            //streamWriter.Close();
        }
        private void saveButton_Click(object sender, EventArgs e)
        {
            SaveAppSettings("NextKey", nextKeyTextBox.Text);
            SaveAppSettings("PreviousKey", previousKeyTextBox.Text);
            SaveAppSettings("StopKey", stopKeyTextBox.Text);
            SaveAppSettings("VolumeUpKey", volumeUpKeyTextBox.Text);
            SaveAppSettings("VolumeDownKey", volumeDownKeyTextBox.Text);
            SaveAppSettings("MakeFavouriteKey", makeFavouriteKeyTextbox.Text);

            SaveAppSettings("NextButton", nextButtonTextBox.Text);
            SaveAppSettings("PreviousButton", previousButtonTextBox.Text);
            SaveAppSettings("StopButton", stopButtonTextBox.Text);
            SaveAppSettings("VolumeUpButton", volumeUpButtonTextBox.Text);
            SaveAppSettings("VolumeDownButton", volumeDownButtonTextBox.Text);
            SaveAppSettings("MakeFavouriteButton", makeFavouriteButtonTextbox.Text);
        }

        public static void SaveAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                Console.WriteLine(configFile.FilePath);
            }
            catch (ConfigurationErrorsException ex)
            {
                Console.WriteLine("Error writing app settings");
                Log.Write(ex.ToString());
            }
        }

        private void keyInput(object sender, KeyEventArgs e)
        {
            TextBox txtBox = (TextBox)sender;
            e.Handled = true;
            e.SuppressKeyPress = true;
            txtBox.Text = e.KeyCode.ToString();
        }
        private void removeBinding(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Escape || e.KeyCode == Keys.Back)
            {
                TextBox txtBox = (TextBox)sender;
                e.Handled = true;
                e.SuppressKeyPress = true;
                txtBox.Clear();
            }
        }

        private void URLLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

            string productName = (string)reg.GetValue("ProductName");

            Console.WriteLine("Current OS: " + productName);
            try
            {
                Process.Start(@"C:\Program Files\Mozilla Firefox\firefox.exe", comboIP.SelectedItem.ToString());
                return;
            }
            catch (Exception)
            {
            }
            try
            {
                Process.Start(@"C:\Program Files (x86)\Mozilla Firefox\firefox.exe", comboIP.SelectedItem.ToString());
                return;
            }
            catch (Exception)
            {
            }
            if (productName.StartsWith("Windows 10"))
            {
                try
                {
                    Process.Start("microsoft-edge:" + comboIP.SelectedItem.ToString());
                    return;
                }
                catch (Exception)
                {
                }
            }
            Process.Start(comboIP.SelectedItem.ToString());
        }

        private void Koenvh_Click(object sender, EventArgs e)
        {
            Process.Start("http://koenvh.nl");
        }

        private void comboLang_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                Language language = new Language(comboLang.SelectedItem.ToString());
                dynamic server = language.GetFile();

                groupInfo.Text = (server["info"] ?? groupInfo.Text);
                gameInfo.Text = (server["game"] ?? gameInfo.Text);
                statusInfo.Text = (server["status"] ?? statusInfo.Text);
                simulatorNotRunning = (server["simulator-not-running"] ?? simulatorNotRunning);
                simulatorNotDriving = (server["simulator-not-driving"] ?? simulatorNotDriving);
                simulatorRunning = (server["simulator-running"] ?? simulatorRunning);
                coordinatesInfo.Text = (server["coordinates"] ?? coordinatesInfo.Text);
                URLInfo.Text = (server["url"] ?? URLInfo.Text);
                URLLabel.Text = (server["open-local-radio"] ?? URLLabel.Text);
                groupSettings.Text = (server["settings"] ?? groupSettings.Text);
                keyLabel.Text = (server["keyboard"] ?? keyLabel.Text);
                buttonLabel.Text = (server["controller"] ?? buttonLabel.Text);
                nextKeyLabel.Text = (server["next-station-key"] ?? nextKeyLabel.Text);
                previousKeyLabel.Text = (server["previous-station-key"] ?? previousKeyLabel.Text);
                stopKeyLabel.Text = (server["stop-playback-key"] ?? stopKeyLabel.Text);
                volumeUpKeyLabel.Text = (server["volume-up-key"] ?? volumeUpKeyLabel.Text);
                volumeDownKeyLabel.Text = (server["volume-down-key"] ?? volumeDownKeyLabel.Text);
                makeFavouriteKeyLabel.Text = (server["make-favourite-key"] ?? makeFavouriteKeyLabel.Text);
                saveButton.Text = (server["save"] ?? saveButton.Text);
                groupController.Text = (server["controller"] ?? groupController.Text);
                groupInstall.Text = (server["install"] ?? groupInstall.Text);
                installAtsButton.Text = (server["install-plugin-ats"] ?? installAtsButton.Text);
                installEts2Button.Text = (server["install-plugin-ets2"] ?? installEts2Button.Text);
                installOverlay = (server["install-overlay"] ?? installOverlay);
                folderDialog.Description = (server["ets2-folder-dialog"] ?? folderDialog.Description);
                Station.NowPlaying = (server["now-playing"] ?? Station.NowPlaying);

                SaveAppSettings("Language", comboLang.SelectedItem.ToString());

                writeFile("language", "0");
            }
            catch (Exception ex)
            {
                Console.Write(ex.ToString());
                MessageBox.Show(ex.Message);
            }
        }

        private void joystickTimer_Tick(object sender, EventArgs e)
        {
            if (joystick == null)
            {
                AttachJoystick();
                if (joystick == null)
                {
                    return;
                }
            }
            try
            {
                bool[] controllerInput = new bool[joystick.State.GetButtons().Length + 4]; // = joystick.State.GetButtons().Concat(joystick.State.GetPointOfViewControllers()).ToArray();
                joystick.State.GetButtons().CopyTo(controllerInput, 0);
                bool[] povState = new bool[4] { false, false, false, false };

                switch (joystick.State.GetPointOfViewControllers()[0])
                {
                    case 0:
                        povState[0] = true;
                        break;
                    case 9000:
                        povState[1] = true;
                        break;
                    case 18000:
                        povState[2] = true;
                        break;
                    case 27000:
                        povState[3] = true;
                        break;
                    default:
                        break;
                }
                povState.CopyTo(controllerInput, joystick.State.GetButtons().Length);

                for (int i = 0; i < controllerInput.Length; i++)
                {

                    if (controllerInput[i] == true && controllerInput[i] != previousState[i])
                    {
                        if (ConfigurationManager.AppSettings["NextButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event NextButton");
                            keyTimeout.Stop();
                            amount++;
                            keyTimeout.Start();
                        }
                        if (ConfigurationManager.AppSettings["PreviousButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event PreviousButton");
                            keyTimeout.Stop();
                            amount--;
                            keyTimeout.Start();
                        }
                        if (ConfigurationManager.AppSettings["StopButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event StopButton");

                            writeFile("stop", "0");
                        }
                        if (ConfigurationManager.AppSettings["VolumeUpButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event VolumeUpButton");

                            writeFile("volume", "5");
                        }
                        if (ConfigurationManager.AppSettings["VolumeDownButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event VolumeDownButton");

                            writeFile("volume", "-5");
                        }
                        if (ConfigurationManager.AppSettings["MakeFavouriteButton"] == i.ToString())
                        {
                            Console.WriteLine("Fired event MakeFavouriteButton");

                            writeFile("favourite", "0");
                        }
                        if (nextButtonTextBox.Focused)
                        {
                            nextButtonTextBox.Text = i.ToString();
                        }
                        if (previousButtonTextBox.Focused)
                        {
                            previousButtonTextBox.Text = i.ToString();
                        }
                        if (stopButtonTextBox.Focused)
                        {
                            stopButtonTextBox.Text = i.ToString();
                        }
                        if (volumeUpButtonTextBox.Focused)
                        {
                            volumeUpButtonTextBox.Text = i.ToString();
                        }
                        if (volumeDownButtonTextBox.Focused)
                        {
                            volumeDownButtonTextBox.Text = i.ToString();
                        }
                        if (makeFavouriteButtonTextbox.Focused)
                        {
                            makeFavouriteButtonTextbox.Text = i.ToString();
                        }
                    }
                }
                previousState = controllerInput;
            }
            catch (Exception)
            {
                //Log.Write(ex.ToString());
            }
        }

        private void currentGameTimer_Tick(object sender, EventArgs e)
        {
            if (currentGame != "ets2")
            {
                if (Process.GetProcessesByName("eurotrucks2").Length > 0)
                {
                    currentGame = "ets2";
                    gameLabel.Text = "Euro Truck Simulator 2";
                    writeFile("game", "0", "0");
                }
            }
            if (currentGame != "ats")
            {
                if (Process.GetProcessesByName("amtrucks").Length > 0)
                {
                    currentGame = "ats";
                    gameLabel.Text = "American Truck Simulator";
                    writeFile("game", "0", "0");
                }
            }
        }

        private void installAtsButton_Click(object sender, EventArgs e)
        {
            if (ChooseFolder("ats"))
            {
                installAtsButton.Image = Resources.check;
                groupInfo.Enabled = true;
                groupSettings.Enabled = true;
            }
        }

        private void installEts2Button_Click(object sender, EventArgs e)
        {
            if (ChooseFolder("ets2"))
            {
                installEts2Button.Image = Resources.check;
                groupInfo.Enabled = true;
                groupSettings.Enabled = true;
            }
        }

        private void comboController_SelectedIndexChanged(object sender, EventArgs e)
        {
            SaveAppSettings("Controller", comboController.SelectedItem.ToString());

            AttachJoystick();
        }
    }
}
