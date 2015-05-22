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
using System.Windows.Input;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using Aurio.WaveControls;

namespace AudioAlign.UI {
    [TemplatePart(Name = "PART_Caret", Type = typeof(UIElement))]
    public class CaretOverlay : VirtualContentViewBase {

        private static readonly DependencyPropertyKey PhysicalCaretOffsetPropertyKey;

        public static readonly DependencyProperty PhysicalCaretOffsetProperty;
        public static readonly DependencyProperty VirtualCaretOffsetProperty;
        public static readonly DependencyProperty SuppressEventsProperty;
        public static readonly DependencyProperty CaretVisibilityProperty;
  
        public class PositionEventArgs : RoutedEventArgs {
            public PositionEventArgs() : base() { }
            public PositionEventArgs(RoutedEvent routedEvent) : base(routedEvent) { }
            public PositionEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source) { }

            public double Position { get; set; }
            public double SourceInterval { get; set; }
        }

        public class IntervalEventArgs : RoutedEventArgs {
            public IntervalEventArgs() : base() { }
            public IntervalEventArgs(RoutedEvent routedEvent) : base(routedEvent) { }
            public IntervalEventArgs(RoutedEvent routedEvent, object source) : base(routedEvent, source) { }

            public double From { get; set; }
            public double To { get; set; }
            public double SourceInterval { get; set; }
        }

        public delegate void PositionEventHandler(object sender, PositionEventArgs e);
        public delegate void IntervalEventHandler(object sender, IntervalEventArgs e);

        public static readonly RoutedEvent PositionSelectedEvent;
        public static readonly RoutedEvent IntervalSelectedEvent;

        static CaretOverlay() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CaretOverlay), 
                new FrameworkPropertyMetadata(typeof(CaretOverlay)));

            PhysicalCaretOffsetPropertyKey = DependencyProperty.RegisterReadOnly("PhysicalCaretOffset",
                typeof(double), typeof(CaretOverlay),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnPhysicalCaretOffsetChanged)) { Inherits = true });
            PhysicalCaretOffsetProperty = PhysicalCaretOffsetPropertyKey.DependencyProperty;

            VirtualCaretOffsetProperty = DependencyProperty.Register("VirtualCaretOffset",
                typeof(long), typeof(CaretOverlay),
                new FrameworkPropertyMetadata(new PropertyChangedCallback(OnVirtualCaretOffsetChanged)) { Inherits = true,
                CoerceValueCallback = CoerceVirtualCaretOffset});

            SuppressEventsProperty = DependencyProperty.RegisterAttached("SuppressEvents", typeof(bool),
                  typeof(CaretOverlay), new FrameworkPropertyMetadata(false, OnSuppressEventsChanged));

            CaretVisibilityProperty = DependencyProperty.Register(
                "CaretVisibility", typeof(Visibility), typeof(CaretOverlay),
                    new FrameworkPropertyMetadata { AffectsRender = true, DefaultValue = Visibility.Visible });


            PositionSelectedEvent = EventManager.RegisterRoutedEvent("PositionSelected", RoutingStrategy.Bubble,
                typeof(PositionEventHandler), typeof(CaretOverlay));

            IntervalSelectedEvent = EventManager.RegisterRoutedEvent("IntervalSelected", RoutingStrategy.Bubble,
                typeof(IntervalEventHandler), typeof(CaretOverlay));
        }

        internal static object CoerceVirtualCaretOffset(DependencyObject d, object value) {
            long newValue = (long)value;

            long lowerBound = 0L;
            if (newValue < lowerBound) {
                return lowerBound;
            }

            long upperBound = ((VirtualView)d).VirtualViewportMaxWidth;
            if (newValue > upperBound) {
                return upperBound;
            }

            return newValue;
        }

        private static void OnPhysicalCaretOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            CaretOverlay caretOverlay = d as CaretOverlay;
            //Debug.WriteLine("CaretOverlay OnPhysicalCaretOffsetChanged {0} -> {1} ({2})", e.OldValue, e.NewValue, caretOverlay.Name);
        }

        private static void OnVirtualCaretOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            CaretOverlay caretOverlay = d as CaretOverlay;
            //Debug.WriteLine("CaretOverlay OnVirtualCaretOffsetChanged {0} -> {1} ({2})", e.OldValue, e.NewValue, caretOverlay.Name);
            caretOverlay.PhysicalCaretOffset = caretOverlay.VirtualToPhysicalIntervalOffset((long)e.NewValue);
        }

        public static void OnSuppressEventsChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args) {
        }

        public static void SetSuppressEvents(DependencyObject element, Boolean value) {
            element.SetValue(SuppressEventsProperty, value);
        }
        public static bool GetSuppressEvents(DependencyObject element) {
            return (bool)element.GetValue(SuppressEventsProperty);
        }

        public CaretOverlay() {
            Loaded += new RoutedEventHandler(CaretOverlay_Loaded);
        }

        private void CaretOverlay_Loaded(object sender, RoutedEventArgs e) {
            Thumb thumb = GetTemplateChild("PART_Caret") as Thumb;
            if (thumb != null) {
                // if the caret is a thumb, enhance the caret overview's functionality
                thumb.DragDelta += new DragDeltaEventHandler(thumb_DragDelta);
            }
        }

        private void thumb_DragDelta(object sender, DragDeltaEventArgs e) {
            RaiseEvent(new PositionEventArgs(PositionSelectedEvent, this) {
                Position = PhysicalCaretOffset + e.HorizontalChange, SourceInterval = ActualWidth
            });
        }

        private Point mouseDownPosition;

        public double PhysicalCaretOffset {
            get { return (double)GetValue(PhysicalCaretOffsetProperty); }
            private set { SetValue(PhysicalCaretOffsetPropertyKey, value); }
        }

        public long VirtualCaretOffset {
            get { return (long)GetValue(VirtualCaretOffsetProperty); }
            private set { SetValue(VirtualCaretOffsetProperty, value); }
        }

        public Visibility CaretVisibility {
            get { return (Visibility)GetValue(CaretVisibilityProperty); }
            set { SetValue(CaretVisibilityProperty, value); }
        }

        public event PositionEventHandler PointSelected {
            add { AddHandler(PositionSelectedEvent, value); }
            remove { RemoveHandler(PositionSelectedEvent, value); }
        }

        public event IntervalEventHandler IntervalSelected {
            add { AddHandler(IntervalSelectedEvent, value); }
            remove { RemoveHandler(IntervalSelectedEvent, value); }
        }

        protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e) {
            base.OnPreviewMouseDown(e);
            //Debug.WriteLine("CaretOverlay OnPreviewMouseDown @ " + Mouse.GetPosition(this));
            mouseDownPosition = Mouse.GetPosition(this);

            bool suppressEvent = false;
            VisualTreeHelper.HitTest(this,
                new HitTestFilterCallback(delegate(DependencyObject target) {
                    //Debug.WriteLine("HitTestFilter: " + target.GetType());
                    return HitTestFilterBehavior.Continue; 
                }),
                new HitTestResultCallback(delegate(HitTestResult target) {
                    //Debug.WriteLine("HitTestResult: " + target.VisualHit + " / " 
                    //    + target.VisualHit.GetValue(SuppressEventsProperty));
                    if ((bool)target.VisualHit.GetValue(SuppressEventsProperty) == true) {
                        suppressEvent = true;
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue; 
                }), 
                new PointHitTestParameters(mouseDownPosition));
            if (suppressEvent) {
                mouseDownPosition = new Point(-1, -1);
            }
        }

        protected override void OnPreviewMouseUp(System.Windows.Input.MouseButtonEventArgs e) {
            base.OnPreviewMouseUp(e);
            //Debug.WriteLine("CaretOverlay OnPreviewMouseUp @ " + Mouse.GetPosition(this));

            if (mouseDownPosition == new Point(-1, -1)) {
                return;
            }

            Point mouseUpPosition = Mouse.GetPosition(this);
            if (mouseUpPosition == mouseDownPosition) {
                // pseudo click event
                //Debug.WriteLine("CaretOverlay PseudoClick @ " + mouseDownPosition);
                RaiseEvent(new PositionEventArgs(PositionSelectedEvent, this) {
                    Position = mouseDownPosition.X, SourceInterval = ActualWidth
                });
                Keyboard.Focus(this);
            }
            else {
                // interval selection event
                RaiseEvent(new IntervalEventArgs(IntervalSelectedEvent, this) {
                    From = mouseDownPosition.X, To = mouseUpPosition.X, SourceInterval = ActualWidth
                });
            }
        }

        protected override void OnViewportWidthChanged(long oldValue, long newValue) {
            base.OnViewportWidthChanged(oldValue, newValue);
            PhysicalCaretOffset = VirtualToPhysicalIntervalOffset(VirtualCaretOffset);
        }

        protected override void OnViewportOffsetChanged(long oldValue, long newValue) {
            base.OnViewportOffsetChanged(oldValue, newValue);
            PhysicalCaretOffset = VirtualToPhysicalIntervalOffset(VirtualCaretOffset);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
            base.OnRenderSizeChanged(sizeInfo);
            OnViewportWidthChanged(VirtualViewportWidth, VirtualViewportWidth);
        }
    }
}
