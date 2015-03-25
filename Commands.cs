using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace AudioAlign {
    static class Commands {
        public static readonly RoutedUICommand PlayToggle = new RoutedUICommand("Play/Pause", "PlayToggle", typeof(MainWindow));
        public static readonly RoutedUICommand FileExportVegasEDL = new RoutedUICommand("Export to Vegas EDL", "FileExportVegasEDL", typeof(MainWindow));
        public static readonly RoutedUICommand FileExportSyncXML = new RoutedUICommand("Export Sync XML", "FileExportSyncXML", typeof(MainWindow));
        public static readonly RoutedUICommand FileExportMatchesCSV = new RoutedUICommand("Export Matches to CSV", "FileExportMatchesCSV", typeof(MainWindow));
        public static readonly RoutedUICommand FileProjectAdd = new RoutedUICommand("Add", "FileProjectAdd", typeof(MainWindow));
        public static readonly RoutedUICommand AddAudioFile = new RoutedUICommand("Add Audio File", "AddAudioFile", typeof(MainWindow));
        public static readonly RoutedUICommand FileExportAudioMix = new RoutedUICommand("Export audio mix", "FileExportAudioMix", typeof(MainWindow));
        public static readonly RoutedUICommand FileOpenRecentProject = new RoutedUICommand("Open Recent Project", "FileOpenRecentProject", typeof(MainWindow));
        public static readonly RoutedUICommand DebugRefreshMultiTrackViewer = new RoutedUICommand("Refresh MultiTrackViewer", "DebugRefreshMultiTrackViewer", typeof(MainWindow));
        public static readonly RoutedUICommand ViewZoomToFit = new RoutedUICommand("Zoom to Fit", "ViewZoomToFit", typeof(MainWindow));
        public static readonly RoutedUICommand ViewFitTracksVertically = new RoutedUICommand("Resize Track Heights", "ViewFitTracksVertically", typeof(MainWindow));
        public static readonly RoutedUICommand ViewGroupMatchingTracks = new RoutedUICommand("Group Matching Tracks", "ViewGroupMatchingTracks", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByOffset = new RoutedUICommand("Offset", "ViewOrderTracksByOffset", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByLength = new RoutedUICommand("Length", "ViewOrderTracksByLength", typeof(MainWindow));
        public static readonly RoutedUICommand ViewOrderTracksByName = new RoutedUICommand("Name", "ViewOrderTracksByName", typeof(MainWindow));
        public static readonly RoutedUICommand ViewDisplayMatches = new RoutedUICommand("Display Matches", "ViewDisplayMatches", typeof(MainWindow));
        public static readonly RoutedUICommand ViewDisplayTrackHeaders = new RoutedUICommand("Display Track Headers", "ViewDisplayTrackHeaders", typeof(MainWindow));
        public static readonly RoutedUICommand ViewTimelineScreenshotVisible = new RoutedUICommand("Copy Visible Timeline to Clipboard", "ViewTimelineScreenshotVisible", typeof(MainWindow));
        public static readonly RoutedUICommand ViewTimelineScreenshotFull = new RoutedUICommand("Copy Full Timeline to Clipboard", "ViewTimelineScreenshotFull", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterVolume = new RoutedUICommand("Master Volume", "MonitorMasterVolume", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorMasterCorrelation = new RoutedUICommand("Master Correlation", "MonitorMasterCorrelation", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorFrequencyGraph = new RoutedUICommand("Frequency Graph", "MonitorFrequencyGraph", typeof(MainWindow));
        public static readonly RoutedUICommand MonitorSpectrogram = new RoutedUICommand("Spectrogram", "MonitorSpectrogram", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnmuteAll = new RoutedUICommand("Unmute All", "TracksUnmuteAll", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnsoloAll = new RoutedUICommand("Unsolo All", "TracksUnsoloAll", typeof(MainWindow));
        public static readonly RoutedUICommand TracksUnlockAll = new RoutedUICommand("Unlock All", "TracksUnlockAll", typeof(MainWindow));
        public static readonly RoutedUICommand TracksResetVolume = new RoutedUICommand("Reset Volume", "TracksResetVolume", typeof(MainWindow));
        public static readonly RoutedUICommand TracksResetColors = new RoutedUICommand("Reset Colors", "TracksResetColors", typeof(MainWindow));
    }
}
