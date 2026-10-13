using System;
using System.Windows.Forms;
using Tetris118.Data;
using Tetris118.UI;

namespace Tetris118
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                SaveStore.Log(e.Exception);
                MessageBox.Show("An unexpected error occurred. The game will close to protect your save.\nDetails were written to errors.log.",
                    "Tetris 118", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            { var error = e.ExceptionObject as Exception; if (error != null) SaveStore.Log(error); };
            try { Application.Run(new MainForm()); }
            catch (Exception ex)
            {
                SaveStore.Log(ex);
                MessageBox.Show("Could not start Tetris 118.\n" + ex.Message, "Tetris 118", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
