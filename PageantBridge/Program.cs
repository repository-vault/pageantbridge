using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PageantBridge
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            string Name = "Pageant";
            try
            {
                PageantBridge foo = new PageantBridge(Name, Name);
                Application.Run();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
            }

            
        }
    }
}
