using McCrypt;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace McEncryptor
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormEncryptor());
        }
    }
}
