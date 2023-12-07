//
// AudioAlign: Audio Synchronization and Analysis Tool
// Copyright (C) 2010-2015  Mario Guggenberger <mg@protyposis.net>
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AudioAlign.UI;
using Aurio;
using Aurio.TaskMonitor;

namespace AudioAlign
{
    /// <summary>
    /// Interaction logic for ModalProgressWindow.xaml
    ///
    /// The difference from this modal dialog to the WPF built in Window.ShowModal() is
    /// that ShowModal is a blocking call that waits until the modal dialog answers and
    /// closes, whereas this modal dialog is an overlay window to block the user from
    /// interacting with the GUI while a long running task is running.
    /// </summary>
    public partial class ModalProgressWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        private readonly ProgressMonitor progressMonitor;
        private IntPtr ownerWindowHandle;
        private bool blockClosing;

        public ModalProgressWindow()
        {
            InitializeComponent();
            progressMonitor = ProgressMonitor.GlobalInstance;
        }

        public ModalProgressWindow(ProgressMonitor progressMonitor)
            : this()
        {
            this.progressMonitor = progressMonitor;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null)
            {
                throw new InvalidOperationException("Required owner is not set");
            }

            NonClientRegionAPI.Glassify(this);

            // INIT PROGRESSBAR
            progressBar.IsEnabled = false;
            progressMonitor.ProcessingStarted += Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished += Instance_ProcessingFinished;

            // Disable parent window to force the user to wait for this modal window to close
            // http://stackoverflow.com/a/22964229
            ownerWindowHandle = (new WindowInteropHelper(Owner)).Handle;
            EnableWindow(ownerWindowHandle, false);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;

            EnableWindow(ownerWindowHandle, true);
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e)
        {
            progressBar
                .Dispatcher
                .BeginInvoke(
                    (Action)
                        delegate
                        {
                            progressBar.IsEnabled = true;
                            progressBarLabel.Text = progressMonitor.StatusMessage;
                        }
                );
            blockClosing = true;
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e)
        {
            progressBar
                .Dispatcher
                .BeginInvoke(
                    (Action)
                        delegate
                        {
                            progressBar.Value = e.Value;
                            progressBarLabel.Text = progressMonitor.StatusMessage;
                        }
                );
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e)
        {
            progressBar
                .Dispatcher
                .BeginInvoke(
                    (Action)
                        delegate
                        {
                            progressBar.Value = 0;
                            progressBar.IsEnabled = false;
                            progressBarLabel.Text = "";
                        }
                );
            blockClosing = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (blockClosing)
            {
                // Block closing the window, e.g. by clicking the close button or pressing ALT+F4
                e.Cancel = true;
            }
        }
    }
}
