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
using System.Windows.Shapes;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Audio.Matching;
using AudioAlign.Audio;
using System.Collections.ObjectModel;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for AnalysisWindow.xaml
    /// </summary>
    public partial class AnalysisWindow : Window {

        private ProgressMonitor progressMonitor;
        //private Analysis analysis;
        private TrackList<AudioTrack> trackList;

        public int AnalysisWindowSize { get; set; }
        public int AnalysisIntervalLength { get; set; }
        public int AnalysisSampleRate { get; set; }

        public AnalysisWindow(TrackList<AudioTrack> trackList) {
            AnalysisWindowSize = 1;
            AnalysisIntervalLength = 30;
            AnalysisSampleRate = 22050;

            InitializeComponent();
            progressMonitor = new ProgressMonitor();
            this.trackList = trackList;

            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            NonClientRegionAPI.Glassify(this);

            progressBar.IsEnabled = false;
            progressMonitor.ProcessingStarted += Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished += Instance_ProcessingFinished;
            ProgressMonitor.GlobalInstance.AddChild(progressMonitor);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            ProgressMonitor.GlobalInstance.RemoveChild(progressMonitor);
            progressMonitor.ProcessingStarted -= Instance_ProcessingStarted;
            progressMonitor.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            progressMonitor.ProcessingFinished -= Instance_ProcessingFinished;
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                parameterGroupBox.IsEnabled = false;
                progressBar.IsEnabled = true;
                progressBarLabel.Text = progressMonitor.StatusMessage;
            });
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
                parameterGroupBox.IsEnabled = true;
            });
        }

        private void analyzeButton_Click(object sender, RoutedEventArgs e) {
            Analysis analysis = new Analysis(trackList, 
                new TimeSpan(0, 0, AnalysisWindowSize),
                new TimeSpan(0, 0, AnalysisIntervalLength), 
                AnalysisSampleRate, 
                progressMonitor);
            ObservableCollection<AnalysisEventArgs> windowResults = new ObservableCollection<AnalysisEventArgs>();
            analysisResultsGrid.ItemsSource = windowResults;
            //analysis.Started += new EventHandler(delegate(object sender2, EventArgs e2) {
            //    analysisResultsGrid.Dispatcher.Invoke((Action)delegate {
            //    });
            //});
            analysis.WindowAnalysed += new EventHandler<AnalysisEventArgs>(delegate(object sender2, AnalysisEventArgs e2) {
                analysisResultsGrid.Dispatcher.Invoke((Action)delegate {
                    windowResults.Add(e2);
                });
            });
            analysis.Finished += new EventHandler<AnalysisEventArgs>(delegate(object sender2, AnalysisEventArgs e2) {
                analysisResultsGrid.Dispatcher.Invoke((Action)delegate {
                    windowResults.Add(e2);
                });
            });
            analysis.ExecuteAsync();
        }
    }
}
