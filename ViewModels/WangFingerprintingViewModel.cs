using AudioAlign.Audio.Matching;
using AudioAlign.Audio.Project;
using AudioAlign.Audio.TaskMonitor;
using AudioAlign.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace AudioAlign.ViewModels {
    public class WangFingerprintingViewModel {

        private ProgressMonitor progressMonitor;
        private TrackList<AudioTrack> trackList;
        private Collection<Match> matchCollection;
        private WangFingerprintingModel model;

        private readonly DelegateCommand fingerprintCommand;
        private readonly DelegateCommand findMatchesCommand;
        private readonly DelegateCommand clearCommand;

        public WangFingerprintingViewModel(ProgressMonitor progressMonitor, TrackList<AudioTrack> trackList, Collection<Match> matchCollection) {
            this.progressMonitor = progressMonitor;
            this.trackList = trackList;
            this.matchCollection = matchCollection;

            model = new WangFingerprintingModel();
            model.FingerprintingFinished += FingerprintingFinished;

            fingerprintCommand = new DelegateCommand(o => {
                model.Reset();
                model.Fingerprint(new List<AudioTrack>(trackList), progressMonitor);
            });

            findMatchesCommand = new DelegateCommand(o => {
                FindAndAddMatches();
            });

            clearCommand = new DelegateCommand(o => {
                model.Reset();
            });
        }

        private void FindAndAddMatches() {
            model.FindAllMatches(progressMonitor, matches => {
                foreach (Match match in matches) {
                    matchCollection.Add(match);
                }
            });
        }

        private void FingerprintingFinished(object sender, EventArgs e) {
            FindAndAddMatches();
        }

        public DelegateCommand FingerprintCommand { get { return fingerprintCommand; } }
        public DelegateCommand FindMatchesCommand { get { return findMatchesCommand; } }
        public DelegateCommand ClearCommand { get { return clearCommand; } }
    }
}
