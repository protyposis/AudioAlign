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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Aurio.Matching;
using Aurio.Matching.HaitsmaKalker2002;
using Aurio.Project;
using Aurio.TaskMonitor;

namespace AudioAlign.Models
{
    public class HaitsmaKalkerFingerprintingModel
    {
        private readonly Profile[] profiles;
        private FingerprintStore store;

        public event EventHandler FingerprintingFinished;

        public HaitsmaKalkerFingerprintingModel()
        {
            FingerprintBerThreshold = 0.45f;
            FingerprintSize = FingerprintStore.DEFAULT_FINGERPRINT_SIZE;
            profiles = FingerprintGenerator.GetProfiles();
            SelectedProfile = profiles[0];
            Reset();
        }

        public Profile[] Profiles
        {
            get { return profiles; }
        }

        public Profile SelectedProfile { get; set; }

        public float FingerprintBerThreshold { get; set; }

        public int FingerprintSize { get; set; }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with a new profile.
        /// </summary>
        /// <param name="profile">the new profile to configure the model with</param>
        public void Reset(Profile profile)
        {
            SelectedProfile = profile ?? throw new ArgumentNullException(nameof(profile));
            store = new FingerprintStore(profile);
        }

        /// <summary>
        /// Resets the model by clearing all data and configuring it with the current profile.
        /// </summary>
        public void Reset()
        {
            Reset(SelectedProfile);
        }

        public void Fingerprint(List<AudioTrack> tracks, ProgressMonitor progressMonitor)
        {
            var selfReference = this;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            long startMemory = GC.GetTotalMemory(false);

            Task.Factory
                .StartNew(
                    () =>
                        Parallel.ForEach<AudioTrack>(
                            tracks,
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = Environment.ProcessorCount
                            },
                            track =>
                            {
                                var startTime = DateTime.Now;
                                var progressReporter = progressMonitor.BeginTask(
                                    "Generating sub-fingerprints for " + track.FileInfo.Name,
                                    true
                                );
                                var generator = new FingerprintGenerator(SelectedProfile, track);
                                int subFingerprintsCalculated = 0;

                                generator.SubFingerprintsGenerated +=
                                    new EventHandler<SubFingerprintsGeneratedEventArgs>(
                                        delegate(object s2, SubFingerprintsGeneratedEventArgs e2)
                                        {
                                            subFingerprintsCalculated += e2.SubFingerprints.Count;
                                            progressReporter.ReportProgress(
                                                (double)e2.Index / e2.Indices * 100
                                            );
                                            store.Add(e2);
                                        }
                                    );

                                generator.Generate();

                                progressReporter.Finish();
                                Debug.WriteLine(
                                    "subfingerprint generation finished with "
                                        + subFingerprintsCalculated
                                        + " hashes in "
                                        + (DateTime.Now - startTime)
                                );
                            }
                        )
                )
                .ContinueWith(
                    task =>
                    {
                        // all running generator tasks have finished
                        stopwatch.Stop();
                        long memory = GC.GetTotalMemory(false) - startMemory;
                        Debug.WriteLine(
                            "Fingerprinting finished in "
                                + stopwatch.Elapsed
                                + " (mem: "
                                + (memory / 1024 / 1024)
                                + " MB)"
                        );

                        FingerprintingFinished?.Invoke(selfReference, EventArgs.Empty);
                    },
                    TaskScheduler.FromCurrentSynchronizationContext()
                );
        }

        public List<Match> FindAllMatches(
            ProgressMonitor progressMonitor,
            Action<List<Match>> callback
        )
        {
            List<Match> matches = null;

            // NOTE The following task is passed the "default" task scheduler, because otherwise
            //      it uses the "current" scheduler, which can be the UI scheduler when called
            //      from a task run by the TaskScheduler.FromCurrentSynchronizationContext(), leading
            //      to a blocked UI.

            Task.Factory
                .StartNew(
                    () =>
                    {
                        var progressReporter = progressMonitor.BeginTask(
                            "Matching hashes...",
                            true
                        );

                        void progressCallback(double progress)
                        {
                            progressReporter.ReportProgress(progress);
                        }

                        Stopwatch sw = new();
                        sw.Start();
                        store.Threshold = FingerprintBerThreshold;
                        store.FingerprintSize = FingerprintSize;
                        matches = store.FindAllMatches(progressCallback);
                        sw.Stop();
                        Debug.WriteLine(matches.Count + " matches found in {0}", sw.Elapsed);

                        matches = MatchProcessor.FilterDuplicateMatches(matches, progressCallback);
                        Debug.WriteLine(matches.Count + " matches found (filtered)");

                        progressReporter.Finish();
                    },
                    CancellationToken.None,
                    TaskCreationOptions.None,
                    TaskScheduler.Default
                ) // Use default scheduler, see NOTE above
                .ContinueWith(
                    task =>
                    {
                        callback.Invoke(matches);
                    },
                    TaskScheduler.FromCurrentSynchronizationContext()
                );

            return matches;
        }
    }
}
