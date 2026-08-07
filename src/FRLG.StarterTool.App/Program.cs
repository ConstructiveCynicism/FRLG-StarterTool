namespace FRLG.StarterTool.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Win32.InitDarkModeSupport();

        Application.Run(new MainForm());
    }    
}