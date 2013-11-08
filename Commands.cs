using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace AudioAlign {
    static class Commands {
        public static readonly RoutedUICommand PlayToggle = new RoutedUICommand("Play/Pause", "PlayToggle", typeof(MainWindow));
        public static readonly RoutedUICommand FileExportVegasEDL = new RoutedUICommand("Export to Sony Vegas EDL", "FileExportVegasEDL", typeof(MainWindow));
        public static readonly RoutedUICommand FileProjectAdd = new RoutedUICommand("Add", "FileProjectAdd", typeof(MainWindow));
        public static readonly RoutedUICommand AddAudioFile = new RoutedUICommand("Add audio file", "AddAudioFile", typeof(MainWindow));
        public static readonly RoutedUICommand DebugRefreshMultiTrackViewer = new RoutedUICommand("Refresh MultiTrackViewer", "DebugRefreshMultiTrackViewer", typeof(MainWindow));
        public static readonly RoutedUICommand ViewZoomToFit = new RoutedUICommand("Zoom to fit", "ViewZoomToFit", typeof(MainWindow));
        public static readonly RoutedUICommand ViewFitTracksVertically = new RoutedUICommand("Resize track heights", "ViewFitTracksVertically", typeof(MainWindow));
        public static readonly RoutedUICommand ViewGroupMatchingTracks = new RoutedUICommand("Group matching tracks", "ViewGroupMatchingTracks", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByOffset = new RoutedUICommand("Offset", "ViewOrderTracksByOffset", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByLength = new RoutedUICommand("Length", "ViewOrderTracksByLength", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByName = new RoutedUICommand("Name", "ViewOrderTracksByName", typeof(MainWindow));
        public static readonly RoutedUICommand ViewDisplayMatches = new RoutedUICommand("Display matches", "ViewDisplayMatches", typeof(MainWindow));
        public static readonly RoutedUICommand ViewDisplayTrackHeaders = new RoutedUICommand("Display track headers", "ViewDisplayTrackHeaders", typeof(MainWindow));
        public static readonly RoutedUICommand ViewTimelineScreenshotVisible = new RoutedUICommand("Copy visible timeline to clipboard", "ViewTimelineScreenshotVisible", typeof(MainWindow));
        public static readonly RoutedUICommand ViewTimelineScreenshotFull = new RoutedUICommand("Copy full timeline to clipboard", "ViewTimelineScreenshotFull", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterVolume = new RoutedUICommand("Master Volume", "MonitorMasterVolume", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterCorrelation = new RoutedUICommand("Master Correlation", "MonitorMasterCorrelation", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorFrequencyGraph = new RoutedUICommand("Frequency Graph", "MonitorFrequencyGraph", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorSpectrogram = new RoutedUICommand("Spectrogram", "MonitorSpectrogram", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnmuteAll = new RoutedUICommand("Unmute All", "TracksUnmuteAll", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnsoloAll = new RoutedUICommand("Unsolo All", "TracksUnsoloAll", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnlockAll = new RoutedUICommand("Unlock All", "TracksUnlockAll", typeof(MainWindow));
    }
}
