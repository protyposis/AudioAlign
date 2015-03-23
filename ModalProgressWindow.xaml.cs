using AudioAlign.Audio;
using AudioAlign.Audio.TaskMonitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for ModalProgressWindow.xaml
    /// 
    /// The difference from this modal dialog to the WPF built in Window.ShowModal() is
    /// that ShowModal is a blocking call that waits until the modal dialog answers and 
    /// closes, whereas this modal dialog is an overlay window to block the user from 
    /// interacting with the GUI while a long running task is running.
    /// </summary>
    public partial class ModalProgressWindow : Window {

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        private ProgressMonitor progressMonitor;
        private IntPtr ownerWindowHandle;
        private bool blockClosing;

        public ModalProgressWindow() {
            InitializeComponent();
            progressMonitor = ProgressMonitor.GlobalInstance;
        }

        public ModalProgressWindow(ProgressMonitor progressMonitor)
            : this() {
                this.progressMonitor = progressMonitor;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (Owner == null) {
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

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;

            EnableWindow(ownerWindowHandle, true);
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.IsEnabled = true;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
            blockClosing = true;
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = e.Value;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
                progressBarLabel.Text = "";
            });
            blockClosing = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
            if (blockClosing) {
                // Block closing the window, e.g. by clicking the close button or pressing ALT+F4
                e.Cancel = true;
            }
        }
    }
}
