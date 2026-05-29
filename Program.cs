using System;
using System.Windows.Forms;

namespace teklaUDA4._8Win
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            //AllocConsole();

            //[System.Runtime.InteropServices.DllImport("kernel32.dll")]
            //static extern bool AllocConsole();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}