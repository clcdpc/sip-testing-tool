namespace Clc.Sip
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            InitializeSingleFileSettings();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static void InitializeSingleFileSettings()
        {
            using var defaults = typeof(Program).Assembly.GetManifestResourceStream("Clc.Sip.SingleFileDefaults.config");
            if (defaults == null)
            {
                return;
            }

            var configuredPath = AppContext.GetData("APP_CONFIG_FILE") as string;
            var configPath = string.IsNullOrEmpty(configuredPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Clc", "SipTestingTool", "winforms-single-file.config")
                : Path.GetFullPath(configuredPath, AppContext.BaseDirectory);

            if (!File.Exists(configPath))
            {
                var directory = Path.GetDirectoryName(configPath);
                Directory.CreateDirectory(directory);
                var temporary = Path.Combine(directory, ".single-file-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var file = File.Create(temporary))
                    {
                        defaults.CopyTo(file);
                    }
                    try
                    {
                        File.Move(temporary, configPath, overwrite: false);
                    }
                    catch (IOException) when (File.Exists(configPath))
                    {
                        // Another instance finished creating the settings first.
                    }
                }
                finally
                {
                    File.Delete(temporary);
                }
            }

            AppContext.SetData("APP_CONFIG_FILE", configPath);
        }
    }
}
