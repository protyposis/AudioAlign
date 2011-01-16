using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AudioAlign.Audio.Project;
using System.IO;
using AudioAlign.WaveControls;
using AudioAlign.Audio;
using System.Diagnostics;
using System.Windows.Threading;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private TrackList<AudioTrack> trackList;
        private MultitrackPlayer player;

        public MainWindow() {
            InitializeComponent();

            trackList = new TrackList<AudioTrack>();
        }

        private void btnAddWaveform_Click(object sender, RoutedEventArgs e) {
            // http://msdn.microsoft.com/en-us/library/aa969773.aspx
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "audio"; // Default file name
            dlg.DefaultExt = ".wav"; // Default file extension
            dlg.Filter = "Wave files (.wav)|*.wav"; // Filter files by extension

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true) {
                // Open document
                AddFile(dlg.FileName);
            }
        }

        private void AddFile(string fileName) {
            if (AudioStreamFactory.IsSupportedFile(fileName)) {
                AudioTrack audioTrack = new AudioTrack(new FileInfo(fileName));
                multiTrackViewer1.Items.Add(audioTrack);
                trackList.Add(audioTrack);
            }
        }

        private void multiTrackViewer1_Drop(object sender, DragEventArgs e) {
            // source: http://stackoverflow.com/questions/332859/detect-dragndrop-file-in-wpf
            if (e.Data is DataObject && ((DataObject)e.Data).ContainsFileDropList()) {
                foreach (string filePath in ((DataObject)e.Data).GetFileDropList()) {
                    AddFile(filePath);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            CommandBinding playBinding = new CommandBinding(MediaCommands.Play);
            CommandBindings.Add(playBinding);

            CommandBinding pauseBinding = new CommandBinding(MediaCommands.Pause);
            CommandBindings.Add(pauseBinding);

            player = new MultitrackPlayer(trackList);

            playBinding.CanExecute += new CanExecuteRoutedEventHandler(playCommandBinding_CanExecute);
            playBinding.Executed += new ExecutedRoutedEventHandler(playCommandBinding_Executed);
            pauseBinding.CanExecute += new CanExecuteRoutedEventHandler(pauseCommandBinding_CanExecute);
            pauseBinding.Executed += new ExecutedRoutedEventHandler(pauseCommandBinding_Executed);

            player.VolumeAnnounced += new EventHandler<Audio.NAudio.StreamVolumeEventArgs>(
                delegate(object sender2, Audio.NAudio.StreamVolumeEventArgs e2) {
                    if (e2.MaxSampleValues.Length >= 2) {
                        stereoVUMeter1.AmplitudeLeft = e2.MaxSampleValues[0];
                        stereoVUMeter1.AmplitudeRight = e2.MaxSampleValues[1];
                    }
                });

            player.CurrentTimeChanged += new EventHandler<ValueEventArgs<TimeSpan>>(
                delegate(object sender2, ValueEventArgs<TimeSpan> e2) {
                    multiTrackViewer1.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                        new DispatcherOperationCallback(delegate {
                            multiTrackViewer1.VirtualCaretOffset = e2.Value.Ticks;
                            return null;
                    }), null);
                });

            player.PlaybackStateChanged += new EventHandler(
                delegate(object sender2, EventArgs e2) {
                    CommandManager.InvalidateRequerySuggested();
                });

            volumeSlider.ValueChanged += new RoutedPropertyChangedEventHandler<double>(
                delegate(object sender2, RoutedPropertyChangedEventArgs<double> e2) {
                    player.Volume = (float)e2.NewValue;
                });
        }

        private void Window_Closed(object sender, EventArgs e) {
            player.Dispose();
        }

        private void playCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = player.CanPlay;
            e.Handled = true;
        }

        private void pauseCommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = player.CanPause;
            e.Handled = true;
        }

        private void playCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            player.CurrentTime = new TimeSpan(multiTrackViewer1.VirtualCaretOffset);
            player.Play();
        }

        private void pauseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e) {
            player.Pause();
        }
    }
}
