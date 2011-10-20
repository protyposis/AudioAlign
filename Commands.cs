using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace AudioAlign {
    static class Commands {
        public static readonly RoutedUICommand PlayToggle = new RoutedUICommand("Play/Pause", "PlayToggle", typeof(MainWindow));
        public static readonly RoutedUICommand AddAudioFile = new RoutedUICommand("Add audio file", "AddAudioFile", typeof(MainWindow));
        public static readonly RoutedUICommand DebugRefreshMultiTrackViewer = new RoutedUICommand("Refresh MultiTrackViewer", "DebugRefreshMultiTrackViewer", typeof(MainWindow));
        public static readonly RoutedUICommand ViewZoomToFit = new RoutedUICommand("Zoom to fit", "ViewZoomToFit", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterVolume = new RoutedUICommand("Master Volume", "MonitorMasterVolume", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterCorrelation = new RoutedUICommand("Master Correlation", "MonitorMasterCorrelation", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorFrequencyGraph = new RoutedUICommand("Frequency Graph", "MonitorFrequencyGraph", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorSpectrogram = new RoutedUICommand("Spectrogram", "MonitorSpectrogram", typeof(MainWindow));
    }
}
