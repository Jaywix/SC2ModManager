/*
 * SC2 Mod Manager
 * A mod manager for Supreme Commander 2 that allows users to easily install, manage, and switch between mods without modifying the original game files.
 * 
 * Created on: April 1, 2026
 * Last updated: April 8, 2026
 * Author: Jacob Wixom
 * 
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC2ModManager.ViewModels
{
    /// <summary>
    ///     This is a simple view model that is used to bind to the progress bar and status text in the UI when performing long-running operations like downloading or applying mods. 
    ///     It implements INotifyPropertyChanged so that the UI can update when the progress or status changes.
    /// </summary>
    public class ProgressViewModel : INotifyPropertyChanged
    {
        private int progress;
        public int Progress
        {
            get => progress;
            set
            {
                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        private string status;
        public string Status
        {
            get => status;
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        // ================= CANCEL =================
        // The Cancel button (and the window's X) ask the work to stop through this. Some steps can't be stopped
        // halfway without breaking the game (like deleting and replacing gamedata), so the work turns cancel off
        // for those with TryLockCancel and a note saying why

        private readonly System.Threading.CancellationTokenSource cancellation = new();
        private readonly object cancelGate = new();

        public System.Threading.CancellationToken CancellationToken => cancellation.Token;

        private bool canCancel = true;
        public bool CanCancel
        {
            get => canCancel;
            private set { canCancel = value; OnPropertyChanged(nameof(CanCancel)); }
        }

        private string cancelNote = string.Empty;
        /// <summary>Shown under the bar when cancel is off, explaining why.</summary>
        public string CancelNote
        {
            get => cancelNote;
            private set { cancelNote = value; OnPropertyChanged(nameof(CancelNote)); }
        }

        public void RequestCancel()
        {
            lock (cancelGate)
            {
                if (!canCancel)
                    return;

                cancellation.Cancel();
                canCancel = false;
            }

            OnPropertyChanged(nameof(CanCancel));
            Status = "Cancelling...";
        }

        /// <summary>
        ///     Turns cancel off for a step that can't be stopped partway. Returns false if they already hit
        ///     Cancel, so the caller stops there instead of starting the step anyway. The lock makes sure a
        ///     click can't sneak in between checking and turning it off.
        /// </summary>
        public bool TryLockCancel(string why)
        {
            lock (cancelGate)
            {
                if (cancellation.IsCancellationRequested)
                    return false;

                canCancel = false;
            }

            OnPropertyChanged(nameof(CanCancel));
            CancelNote = why;
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
