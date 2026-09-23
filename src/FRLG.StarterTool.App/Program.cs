namespace FRLG.StarterTool.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--beep-selftest")
        {
            Environment.ExitCode = BeepSelfTest.Run(args);
            return;
        }

        ApplicationConfiguration.Initialize();

        Win32.InitDarkModeSupport();

        Application.Run(new MainForm());
    }    
}