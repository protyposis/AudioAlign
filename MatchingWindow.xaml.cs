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
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Audio;
using AudioAlign.Audio.Matching.HaitsmaKalker2002;
using AudioAlign.WaveControls;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.Matching;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AudioAlign {
    /// <summary>
    /// Interaction logic for MatchingWindow.xaml
    /// </summary>
    public partial class MatchingWindow : Window {

        private FingerprintStore fingerprintStore;
        private TrackList<AudioTrack> trackList;
        private MultiTrackViewer multiTrackViewer;

        private volatile int numTasksRunning;

        public MatchingWindow(TrackList<AudioTrack> trackList, MultiTrackViewer multiTrackViewer) {
            InitializeComponent();
            this.fingerprintStore = new FingerprintStore();
            this.trackList = trackList;
            this.multiTrackViewer = multiTrackViewer;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            // INIT PROGRESSBAR
            progressBar.IsEnabled = false;
            //ProgressMonitor.Instance.ProcessingStarted += Instance_ProcessingStarted;
            //ProgressMonitor.Instance.ProcessingProgressChanged += Instance_ProcessingProgressChanged;
            //ProgressMonitor.Instance.ProcessingFinished += Instance_ProcessingFinished;
            bestMatchRadioButton.IsChecked = true;
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e) {
            ProgressMonitor.Instance.ProcessingStarted -= Instance_ProcessingStarted;
            ProgressMonitor.Instance.ProcessingProgressChanged -= Instance_ProcessingProgressChanged;
            ProgressMonitor.Instance.ProcessingFinished -= Instance_ProcessingFinished;
            multiTrackViewer.Matches.Clear();
        }

        private void Instance_ProcessingStarted(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.IsEnabled = true;
                progressBarLabel.Text = ProgressMonitor.Instance.StatusMessage;
            });
        }

        private void Instance_ProcessingProgressChanged(object sender, ValueEventArgs<float> e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = e.Value;
                progressBarLabel.Text = ProgressMonitor.Instance.StatusMessage;
            });
        }

        private void Instance_ProcessingFinished(object sender, EventArgs e) {
            progressBar.Dispatcher.BeginInvoke((Action)delegate {
                progressBar.Value = 0;
                progressBar.IsEnabled = false;
                progressBarLabel.Text = "";
            });
        }

        private void scanTracksButton_Click(object sender, RoutedEventArgs e) {
            // calculate subfingerprints
            numTasksRunning = trackList.Count;
            foreach (AudioTrack audioTrackFE in trackList) {
                // local reference is needed for the async task to reference
                // the right object, instead of always the last one in the list (see: http://stackoverflow.com/questions/2925303/foreach-loop-and-tasks)
                AudioTrack audioTrack = audioTrackFE;

                Task.Factory.StartNew(() => {
                    DateTime startTime = DateTime.Now;
                    ProgressReporter progressReporter = ProgressMonitor.Instance.BeginTask("Generating sub-fingerprints for " + audioTrack.FileInfo.Name, true);

                    FingerprintGenerator fpg = new FingerprintGenerator(audioTrack);
                    int subFingerprintsCalculated = 0;
                    fpg.SubFingerprintCalculated += new EventHandler<SubFingerprintEventArgs>(delegate(object s2, SubFingerprintEventArgs e2) {
                        subFingerprintsCalculated++;
                        progressReporter.ReportProgress((double)e2.Timestamp.Ticks / audioTrack.Length.Ticks * 100);
                        fingerprintStore.Add(e2.AudioTrack, e2.SubFingerprint, e2.Timestamp);
                    });
                    fpg.Completed += new EventHandler(FingerprintGenerator_Completed);
                    fpg.Generate();

                    ProgressMonitor.Instance.EndTask(progressReporter);
                    Debug.WriteLine("subfingerprint generation finished - " + (DateTime.Now - startTime));
                }, TaskCreationOptions.LongRunning);
            }
        }

        private void FingerprintGenerator_Completed(object sender, EventArgs e) {
            if (--numTasksRunning == 0) {
                // all running generator tasks have finished
                multiTrackViewer.Dispatcher.BeginInvoke((Action)delegate {
                    // calculate fingerprints / matches after processing of all tracks has finished
                    List<Match> matches = fingerprintStore.FindAllMatchingMatches();
                    multiTrackViewer.Matches.Clear();
                    Debug.WriteLine(matches.Count + " matches found");
                    foreach (Match match in matches) {
                        multiTrackViewer.Matches.Add(match);
                    }
                    matchGrid.ItemsSource = matches;
                });
            }
        }

        private void alignTracksButton_Click(object sender, RoutedEventArgs e) {
            List<Match> matches = fingerprintStore.FindAllMatchingMatches();
            Dictionary<AudioTrack, List<Match>> mapping = new Dictionary<AudioTrack, List<Match>>();

            foreach (AudioTrack audioTrack in trackList) {
                if (!mapping.ContainsKey(audioTrack)) {
                    mapping.Add(audioTrack, new List<Match>());
                }
                List<Match> audioTrackMatches = mapping[audioTrack];
                foreach (Match match in matches) {
                    if (match.Track1 == audioTrack || match.Track2 == audioTrack) {
                        audioTrackMatches.Add(match);
                    }
                }
                //matches.RemoveAll(m => audioTrackMatches.Contains(m));
                //if (audioTrackMatches.Count == 0) {
                //    mapping.Remove(audioTrack);
                //}
            }

            //if (matches.Count != 0) {
            //    throw new Exception(matches.Count + "unmapped match(es) left");
            //}

            if ((bool)bestMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderByDescending(m => m.Similarity);
                    Match bestMatch = sortedMatches.First();
                    if (bestMatch.Track1.Offset.Ticks + bestMatch.Track1Time.Ticks < bestMatch.Track2.Offset.Ticks + bestMatch.Track2Time.Ticks) {
                        // align track 1
                        bestMatch.Track1.Offset = new TimeSpan(bestMatch.Track2.Offset.Ticks + bestMatch.Track2Time.Ticks - bestMatch.Track1Time.Ticks);
                    }
                    else {
                        // align track 2
                        bestMatch.Track2.Offset = new TimeSpan(bestMatch.Track1.Offset.Ticks + bestMatch.Track1Time.Ticks - bestMatch.Track2Time.Ticks);
                    }
                    Debug.WriteLine("best match: " + bestMatch);
                }
            }
            else if ((bool)firstMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderBy(m => m.Track1Time);
                    Match firstMatch = sortedMatches.First();
                    if (firstMatch.Track1.Offset.Ticks + firstMatch.Track1Time.Ticks < firstMatch.Track2.Offset.Ticks + firstMatch.Track2Time.Ticks) {
                        // align track 1
                        firstMatch.Track1.Offset = new TimeSpan(firstMatch.Track2.Offset.Ticks + firstMatch.Track2Time.Ticks - firstMatch.Track1Time.Ticks);
                    }
                    else {
                        // align track 2
                        firstMatch.Track2.Offset = new TimeSpan(firstMatch.Track1.Offset.Ticks + firstMatch.Track1Time.Ticks - firstMatch.Track2Time.Ticks);
                    }
                    Debug.WriteLine("first match: " + firstMatch);
                }
            }
            else if ((bool)lastMatchRadioButton.IsChecked) {
                foreach (AudioTrack audioTrack in mapping.Keys) {
                    if (audioTrack == trackList[0]) {
                        continue;
                    }
                    IEnumerable<Match> sortedMatches = mapping[audioTrack].OrderBy(m => m.Track1Time);
                    Match lastMatch = sortedMatches.Last();
                    if (lastMatch.Track1.Offset.Ticks + lastMatch.Track1Time.Ticks < lastMatch.Track2.Offset.Ticks + lastMatch.Track2Time.Ticks) {
                        // align track 1
                        lastMatch.Track1.Offset = new TimeSpan(lastMatch.Track2.Offset.Ticks + lastMatch.Track2Time.Ticks - lastMatch.Track1Time.Ticks);
                    }
                    else {
                        // align track 2
                        lastMatch.Track2.Offset = new TimeSpan(lastMatch.Track1.Offset.Ticks + lastMatch.Track1Time.Ticks - lastMatch.Track2Time.Ticks);
                    }
                    Debug.WriteLine("last match: " + lastMatch);
                }
            }
            else if ((bool)averageMatchRadioButton.IsChecked) {
                //
            }
            else if ((bool)windowedAverageMatchRadioButton.IsChecked) {
                //
            }
            else if ((bool)allMatchRadioButton.IsChecked) {
                //
            }
        }
    }
}
