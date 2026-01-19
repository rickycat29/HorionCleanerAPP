using System;
using System.Windows.Forms;

namespace HorionCleaner
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppFlowContext());
        }
    }

    public sealed class AppFlowContext : ApplicationContext
    {
        private bool _transitioning;

        public AppFlowContext()
        {
            ShowSplash();
        }

        private void ShowSplash()
        {
            _transitioning = false;
            var splash = new SplashForm();

            splash.FormClosed += (s, e) =>
            {
                // Se lo splash viene chiuso manualmente, esci.
                if (!_transitioning) ExitThread();
            };

            splash.SplashFinished += () =>
            {
                _transitioning = true;

                // chiudi splash senza far terminare l'app
                splash.Hide();
                splash.Close();

                ShowLogin();
            };

            splash.Show();
        }

        private void ShowLogin()
        {
            _transitioning = false;
            var login = new LoginForm();

            login.FormClosed += (s, e) =>
            {
                if (!_transitioning) ExitThread();
            };

            login.LoginSuccess += () =>
            {
                _transitioning = true;

                login.Hide();
                login.Close();

                ShowMain();
            };

            login.Show();
        }

        private void ShowMain()
        {
            var main = new HorionCleaner();
            main.FormClosed += (s, e) => ExitThread();
            main.Show();
        }
    }
}
