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
