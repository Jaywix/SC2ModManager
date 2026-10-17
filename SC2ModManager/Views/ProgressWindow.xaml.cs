/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 *
 * Created on: April 1, 2026
 * Last updated: October 14, 2026
 * Author: Jacob Wixom
 *
*/
using SC2ModManager.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace SC2ModManager.Views
{
    /// <summary>
    ///     Progress window for the long gamedata jobs (backup / restore). The DataContext is a
    ///     ProgressViewModel. Cancel and the X both ask the job to stop, the window only actually closes
    ///     when the job is done and calls CloseWhenDone, so closing it can't leave the job running unseen.
    /// </summary>
    public partial class ProgressWindow : Window
    {
        private bool allowClose;

        public ProgressWindow()
        {
            InitializeComponent();
        }

        private ProgressViewModel? Vm => DataContext as ProgressViewModel;

        private void Cancel_Click(object sender, RoutedEventArgs e) => Vm?.RequestCancel();

        protected override void OnClosing(CancelEventArgs e)
        {
            // The X used to close the window while the job kept going in the background. Now it counts as
            // Cancel (if cancel is allowed right now) and the window stays up until the job actually stops
            if (!allowClose)
            {
                e.Cancel = true;
                Vm?.RequestCancel();
            }

            base.OnClosing(e);
        }

        /// <summary>Closes the window for real. The job calls this when it's finished, cancelled, or failed.</summary>
        public void CloseWhenDone()
        {
            allowClose = true;
            Close();
        }
    }
}
