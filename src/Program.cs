using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace PageantBridge
{
    enum Errors
    {
        ALREADY_RUNNING = 2,
    }

    class PublicException : Exception
    {
        public Errors statusCode { get; set; }

        public PublicException(Errors code) : this(code, "Already running") {}



        public PublicException(Errors code, string message)
            : base(message)
        {
            this.statusCode = code;
        }
    }

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
            catch (PublicException e)
            {
                Environment.ExitCode = e.statusCode.GetHashCode();
                Application.Exit();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                Environment.ExitCode = 255;
                Application.Exit();
            }

            
        }
    }
}
