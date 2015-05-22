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
using Aurio.Matching;
using Aurio.Project;
using Aurio.TaskMonitor;
using AudioAlign.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace AudioAlign.ViewModels {
    public class EchoprintFingerprintingViewModel {

        private ProgressMonitor progressMonitor;
        private TrackList<AudioTrack> trackList;
        private Collection<Match> matchCollection;
        private EchoprintFingerprintingModel model;

        private readonly DelegateCommand fingerprintCommand;
        private readonly DelegateCommand findMatchesCommand;
        private readonly DelegateCommand clearCommand;

        public EchoprintFingerprintingViewModel(ProgressMonitor progressMonitor, TrackList<AudioTrack> trackList, Collection<Match> matchCollection) {
            this.progressMonitor = progressMonitor;
            this.trackList = trackList;
            this.matchCollection = matchCollection;

            model = new EchoprintFingerprintingModel();
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
