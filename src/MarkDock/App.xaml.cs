using System;
using System.Windows;
using System.Windows.Threading;

namespace MarkDock
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Globaler Fehler-Handler: fängt unerwartete Fehler ab, die sonst die App
            // ohne Vorwarnung komplett abstürzen lassen würden. Zeigt stattdessen eine
            // Meldung, die App läuft danach weiter (statt sofort zu beenden).
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Ein unerwarteter Fehler ist aufgetreten:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\nMarkDock versucht weiterzulaufen.",
                "Unerwarteter Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"Ein schwerwiegender Fehler ist aufgetreten:\n\n{ex.GetType().Name}: {ex.Message}",
                    "Schwerwiegender Fehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
