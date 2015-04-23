using AudioAlign.Audio.Matching;
using AudioAlign.Audio.Matching.Wang2003;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.TaskMonitor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioAlign.Models {
    public class WangFingerprintingModel {

        private Profile profile;
        private FingerprintStore store;

        public event EventHandler FingerprintingFinished;

        public WangFingerprintingModel() {
            Reset(new Profile());
        }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with a new profile.
        /// </summary>
        /// <param name="profile">the new profile to configure the model with</param>
        public void Reset(Profile profile) {
            if (profile == null) {
                throw new ArgumentNullException("profile must not be null");
            }

            this.profile = profile;
            store = new FingerprintStore(profile);
        }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with the current profile.
        /// </summary>
        public void Reset() {
            Reset(profile);
        }

        public void Fingerprint(List<AudioTrack> tracks, ProgressMonitor progressMonitor) {
            var selfReference = this;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            long startMemory = GC.GetTotalMemory(false);

            Task.Factory.StartNew(() => Parallel.ForEach<AudioTrack>(tracks,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                track => {
                    var startTime = DateTime.Now;
                    var progressReporter = progressMonitor.BeginTask("Generating fingerprint hashes for " + track.FileInfo.Name, true);
                    var generator = new FingerprintGenerator(profile);
                    int hashesCalculated = 0;

                    generator.FingerprintHashesGenerated += delegate(object s, FingerprintHashEventArgs e) {
                        hashesCalculated += e.Hashes.Count;
                        progressReporter.ReportProgress((double)e.Index / e.Indices * 100);
                        store.Add(e);
                    };

                    generator.Generate(track);

                    progressReporter.Finish();
                    Debug.WriteLine("Fingerprint hash generation finished with " + hashesCalculated + " hashes in " + (DateTime.Now - startTime));

                })).ContinueWith(task => {
                    // all running generator tasks have finished
                    stopwatch.Stop();
                    long memory = GC.GetTotalMemory(false) - startMemory;
                    Debug.WriteLine("Fingerprinting finished in " + stopwatch.Elapsed + " (mem: " + (memory / 1024 / 1024) + " MB)");
                    
                    if (FingerprintingFinished != null) {
                        FingerprintingFinished(selfReference, EventArgs.Empty);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public List<Match> FindAllMatches() {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            List<Match> matches = store.FindAllMatches();
            sw.Stop();
            Debug.WriteLine(matches.Count + " matches found in {0}", sw.Elapsed);
            matches = MatchProcessor.FilterDuplicateMatches(matches);
            Debug.WriteLine(matches.Count + " matches found (filtered)");
            return matches;
        }
    }
}
