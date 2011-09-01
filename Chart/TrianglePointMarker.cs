using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Research.DynamicDataDisplay.PointMarkers;
using System.Windows.Media;
using System.Windows;

namespace AudioAlign.Chart {
    public class TrianglePointMarker : ShapePointMarker {

        public enum Direction {
            Up,
            Down
        }

        private Direction direction;

        public TrianglePointMarker(Direction direction) {
            this.direction = direction;
        }

        public override void Render(DrawingContext dc, Point screenPoint) {
            Point pt0 = new Point(-Size / 2, -Size / 2);
            Point pt1 = new Point(0, Size / 2);
            Point pt2 = new Point(Size / 2, -Size / 2);

            StreamGeometry streamGeom = new StreamGeometry();
            using (var context = streamGeom.Open()) {
                context.BeginFigure(pt0, true, true);
                context.LineTo(pt1, true, true);
                context.LineTo(pt2, true, true);
            }

            TransformGroup transformGroup = new TransformGroup();
            streamGeom.Transform = transformGroup;
            if (direction == Direction.Up) {
                transformGroup.Children.Add(new RotateTransform(180d));
            }
            transformGroup.Children.Add(new TranslateTransform(screenPoint.X, screenPoint.Y));

            dc.DrawGeometry(Fill, Pen, streamGeom);
        }
    }
}
