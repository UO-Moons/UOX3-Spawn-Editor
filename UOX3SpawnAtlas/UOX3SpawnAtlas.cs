using System;
using System.Windows.Forms;

namespace UOX3SpawnAtlas
{
    internal static class UOX3SpawnAtlas
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
