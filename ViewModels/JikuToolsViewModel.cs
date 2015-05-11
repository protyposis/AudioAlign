using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Aurio.Audio.Project;
using AudioAlign.Models;
using Match = Aurio.Audio.Matching.Match;

namespace AudioAlign.ViewModels {
    public class JikuToolsViewModel {

        private readonly DelegateCommand moveCommand;
        private readonly DelegateCommand timestampAlignCommand;
        private readonly DelegateCommand driftCorrectCommand;
        private readonly DelegateCommand evaluateOffsetsCommand;

        public JikuToolsViewModel(TrackList<AudioTrack> trackList, Collection<Match> matchCollection) {
            moveCommand = new DelegateCommand(o => {
                try {
                    JikuDatasetUtils.Move(trackList, SelectionRegex, MovementOffset);
                }
                catch (Exception ex) {
                    Console.WriteLine("moving failed:");
                    Console.WriteLine(ex);
                }
            });
            timestampAlignCommand = new DelegateCommand(o => {
                JikuDatasetUtils.TimestampAlign(trackList, SelectionRegex);
            });
            driftCorrectCommand = new DelegateCommand(o => {
                DriftCorrect(matchCollection);
            });
            evaluateOffsetsCommand = new DelegateCommand(o => {
                JikuDatasetUtils.EvaluateOffsets(trackList);
            });

            SelectionRegex = ".*";
        }

        public string SelectionRegex { get; set; }
        public TimeSpan MovementOffset { get; set; }

        /// <summary>
        /// Reads drift factors from a config file and applies them to all matches by scaling 
        /// their 2 match positions by the drift factor.
        /// </summary>
        private void DriftCorrect(Collection<Match> matchCollection) {
            try {
                var dialog = new Microsoft.Win32.OpenFileDialog {
                    DefaultExt = ".txt",
                    Filter = "Drift Config|*.txt",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true) {
                    // read drift config
                    Dictionary<string, double> mapping = new Dictionary<string, double>();
                    using (StreamReader reader = File.OpenText(dialog.FileName)) {
                        string line;
                        while ((line = reader.ReadLine()) != null) {
                            string[] parts = line.Split(';');
                            mapping.Add(parts[0], Double.Parse(parts[1]));
                        }
                    }

                    // adjust matching points
                    foreach (Match m in matchCollection) {
                        foreach (string filePattern in mapping.Keys) {
                            double factor = mapping[filePattern];
                            string regex = "^" + filePattern.Replace(".", "\\.").Replace("*", ".+") + "$";

                            if (Regex.IsMatch(m.Track1.FileInfo.Name, regex)) {
                                m.Track1Time = new TimeSpan((long)(m.Track1Time.Ticks * factor));
                                Debug.WriteLine("adjusted " + m.Track1.Name);
                            }
                            if (Regex.IsMatch(m.Track2.FileInfo.Name, regex)) {
                                m.Track2Time = new TimeSpan((long)(m.Track2Time.Ticks * factor));
                                Debug.WriteLine("adjusted " + m.Track2.Name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine(ex.ToString());
            }
        }

        public DelegateCommand MoveCommand { get { return moveCommand; } }
        public DelegateCommand TimestampAlignCommand { get { return timestampAlignCommand; } }
        public DelegateCommand DriftCorrectCommand { get { return driftCorrectCommand; } }
        public DelegateCommand EvaluateOffsetsCommand { get { return evaluateOffsetsCommand; } }
    }
}
