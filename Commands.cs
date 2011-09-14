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
    }
}
