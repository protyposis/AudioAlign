using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Aurio.Project;
using Aurio.WaveControls;

namespace AudioAlign.UI {
    [TemplatePart(Name = "PART_VerticalScrollBar", Type = typeof(ScrollBar))]
    public class MultiTrackListBox : ListBox {
        
        public static readonly DependencyProperty VirtualViewportWidthProperty;
        public static readonly DependencyProperty TrackHeadersVisibilityProperty;
        public static readonly DependencyProperty ControlsVisibilityProperty;

        static MultiTrackListBox() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiTrackListBox), new FrameworkPropertyMetadata(typeof(MultiTrackListBox)));

            VirtualViewportWidthProperty = VirtualViewBase.VirtualViewportWidthProperty.AddOwner(typeof(MultiTrackListBox), new FrameworkPropertyMetadata() { Inherits = true });
            
            TrackHeadersVisibilityProperty = DependencyProperty.Register(
                "TrackHeadersVisibility", typeof(Visibility), typeof(MultiTrackListBox),
                    new FrameworkPropertyMetadata { AffectsRender = true, DefaultValue = Visibility.Visible });

            ControlsVisibilityProperty = DependencyProperty.Register(
                "ControlsVisibility", typeof(Visibility), typeof(MultiTrackListBox),
                    new FrameworkPropertyMetadata { AffectsRender = true, DefaultValue = Visibility.Visible });
        }

        public long VirtualViewportWidth {
            get { return (long)GetValue(VirtualViewportWidthProperty); }
            set { SetValue(VirtualViewportWidthProperty, value); }
        }

        public Visibility TrackHeadersVisibility {
            get { return (Visibility)GetValue(TrackHeadersVisibilityProperty); }
            set { SetValue(TrackHeadersVisibilityProperty, value); }
        }

        public Visibility ControlsVisibility {
            get { return (Visibility)GetValue(ControlsVisibilityProperty); }
            set { SetValue(ControlsVisibilityProperty, value); }
        }
    }
}
