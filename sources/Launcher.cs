using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Reflection;


namespace Launcher
{
    public class Category
    {
        public string Name { get; set; }
        public List<Software> Softwares { get; set; }
    }

    public class Software
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Icon { get; set; }
        public string Arguments { get; set; }
        public Dictionary<string, object> Environments { get; set; }
        public bool Separator { get; set; }

        private bool visible = true;
        public bool Visible
        {
            get { return visible; }
            set { visible = value; }
        }
    }

    public class Program
    {
        private const uint INFO_ICON = 0x000000100;
        private const uint INFO_SMALLICON = 0x000000001;
        private const uint INFO_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private static NotifyIcon notifyIcon;
        private static List<Category> launcherCategories;
        private static Icon trayIcon;
        private static string jsonPath;

        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            trayIcon = GetSelfIcon();
            jsonPath = GetConfigPath();

            ParseArguments(args);
            InitNotifyIcon();
            ImportConfig();
            CreateDefaultItems();

            Application.Run();
        }

        // functions.

        private static void AddMenuItems(ToolStripItemCollection collection, List<Software> softwares)
        {
            foreach (var software in softwares)
            {
                if (!software.Visible)
                {
                    continue;
                }
                if (software.Separator)
                {
                    collection.Add(new ToolStripSeparator());
                }
                else
                {
                    var menuItem = new ToolStripMenuItem(software.Name);
                    string expandedPath = Environment.ExpandEnvironmentVariables(software.Path);

                    if (Directory.Exists(expandedPath))
                    {
                        menuItem.Click += (s, args) => OpenFolder(expandedPath);

                        Icon folderIcon = GetFolderIcon(expandedPath);
                        menuItem.Image = folderIcon.ToBitmap();
                    }
                    else
                    {
                        menuItem.Click += (s, args) => LaunchSoftware(expandedPath, software.Arguments, software.Environments);
                    }

                    if (!string.IsNullOrEmpty(software.Icon))
                    {
                        string expandedIconPath = Environment.ExpandEnvironmentVariables(software.Icon);

                        if (File.Exists(expandedIconPath))
                        {
                            using (Icon icon = Icon.ExtractAssociatedIcon(expandedIconPath))
                            {
                                menuItem.Image = icon.ToBitmap();
                            }
                        }
                    }
                    collection.Add(menuItem);
                }
            }
        }

        private static string GetConfigPath()
        {
            string exePath = Assembly.GetExecutingAssembly().Location;
            string exeDir = Path.GetDirectoryName(exePath);
            string filePath = Path.Combine(exeDir, "QuickLauncher.json");
            return filePath;
        }

        private static void InitNotifyIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = trayIcon;
            notifyIcon.Text = "Quick Launcher";
            notifyIcon.Visible = true;
            notifyIcon.MouseClick += ShowNotifyIconMenu;
        }

        private static void ImportConfig()
        {
            using (StreamReader reader = new StreamReader(jsonPath))
            {
                string jsonText = reader.ReadToEnd();
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                launcherCategories = serializer.Deserialize<List<Category>>(jsonText);
            }
        }

        private static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                string flag = args[i];
                string path = args[i + 1];

                if (!File.Exists(path))
                {
                    continue;
                }
                else if (flag.Equals("-json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonPath = path;
                }
                else if (flag.Equals("-icon", StringComparison.OrdinalIgnoreCase))
                {
                    trayIcon = new Icon(path);
                }
            }
        }

        // slots.

        private static void OpenFolder(string folderPath)
        {
            try
            {
                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to open folder: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LaunchSoftware(string path, string arguments, Dictionary<string, object> environments)
        {
            try
            {
                if (environments != null && environments.Count > 0)
                {
                    foreach (var environment in environments)
                    {
                        string key = environment.Key;
                        object value = environment.Value;

                        if (value is string)
                        {
                            string stringValue = (string)value;
                            string expandedValue = Environment.ExpandEnvironmentVariables(stringValue);
                            Environment.SetEnvironmentVariable(key, expandedValue);
                        }
                        else if (value is System.Collections.ArrayList)
                        {
                            System.Collections.ArrayList arrayValue = (System.Collections.ArrayList)value;
                            string joinedValue = string.Join(";", arrayValue.ToArray());
                            string expandedValue = Environment.ExpandEnvironmentVariables(joinedValue);
                            Environment.SetEnvironmentVariable(key, expandedValue);
                        }
                    }
                }

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to launch software: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void EditConfig(object sender, EventArgs e)
        {
            try
            {
                Process.Start(jsonPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to open JSON file: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ExitApp(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private static void ShowNotifyIconMenu(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                notifyIcon.ContextMenuStrip = CreateDefaultItems();
            }
            else
            {
                notifyIcon.ContextMenuStrip = CreateMenuItems();
            }

            MethodInfo methodInfo = typeof(NotifyIcon).GetMethod(
                "ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            methodInfo.Invoke(notifyIcon, null);
        }

        private static void UpdateMenuItems(object sender, EventArgs e)
        {
            try
            {
                ImportConfig();
                CreateMenuItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to update menu items: " + ex.Message,
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // structures.

        private static Icon GetSelfIcon()
        {
            Assembly assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
            {
                return Icon.ExtractAssociatedIcon(assembly.Location);
            }
            return null;
        }

        private static Icon GetFolderIcon(string folderPath)
        {
            SHFILEINFO info = new SHFILEINFO();
            IntPtr hImg = SHGetFileInfo(
                folderPath,
                FILE_ATTRIBUTE_DIRECTORY,
                ref info,
                (uint)Marshal.SizeOf(info),
                INFO_ICON | INFO_SMALLICON | INFO_USEFILEATTRIBUTES);

            if (hImg != IntPtr.Zero)
                return Icon.FromHandle(info.hIcon);
            else
                return null;
        }

        private static ContextMenuStrip CreateDefaultItems()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Update", null, UpdateMenuItems);
            menu.Items.Add("Edit", null, EditConfig);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, ExitApp);
            return menu;
        }

        private static ContextMenuStrip CreateMenuItems()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            foreach (var category in launcherCategories)
            {
                if (!string.IsNullOrEmpty(category.Name))
                {
                    var categoryMenuItem = new ToolStripMenuItem(category.Name);
                    menu.Items.Add(categoryMenuItem);
                    AddMenuItems(categoryMenuItem.DropDownItems, category.Softwares);
                }
                else
                {
                    AddMenuItems(menu.Items, category.Softwares);
                }
            }
            return menu;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref SHFILEINFO info,
            uint cbSizeFileInfo,
            uint uFlags
        );

    }
}
