using System;
using System.Windows.Forms;

namespace UOX3SpawnEditor
{
    internal static class UOX3SpawnEditor
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
