using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Aurio.Audio.DataStructures.Matrix;
using Aurio.Audio.Matching.Dixon2005;
using Aurio.WaveControls;

namespace AudioAlign.UI {
    public class DtwPath : Control {

        private WriteableBitmap writeableBitmap;
        private IMatrix<double> cellCostMatrix;
        private IMatrix<double> totalCostMatrix;
        private int size;
        private int[] pixels;
        private int pathColor, minColor, maxColor, undefColor;
        private int[] colorPalette;

        public DtwPath() {
            ColorGradient gradient = new ColorGradient(0, 1);
            gradient.AddStop(Colors.Black, 0);
            gradient.AddStop(Colors.White, 1);
            colorPalette = gradient.GetGradient(256).Select(c => GetColorValue(c)).ToArray();

            pathColor = GetColorValue(Colors.LimeGreen);
            minColor = GetColorValue(Colors.Magenta);
            maxColor = GetColorValue(Colors.Red);
            undefColor = GetColorValue(Colors.White);

            ClipToBounds = true;
        }

        public void Init(int size, IMatrix<double> cellCostMatrix, IMatrix<double> totalCostMatrix) {
            this.cellCostMatrix = cellCostMatrix;
            this.totalCostMatrix = totalCostMatrix;
            this.size = (int)this.Width;
            InitializeBitmap(true);
        }

        public void Refresh(int i, int j, int minI, int minJ) {
            List<DTW.Pair> path = DTW.OptimalWarpingPath(totalCostMatrix, minI, minJ);

            int iOffset = i - writeableBitmap.PixelWidth;
            int jOffset = j - writeableBitmap.PixelHeight;

            if(iOffset < 0) iOffset = 0;
            if(jOffset < 0) jOffset = 0;

            // draw cost matrix
            int color;
            for (int x = 0; x < writeableBitmap.PixelWidth; x++) {
                for (int y = 0; y < writeableBitmap.PixelHeight; y++) {
                    double val = cellCostMatrix[iOffset + x, jOffset + y];
                    if (totalCostMatrix[iOffset + x, jOffset + y] == double.PositiveInfinity)
                        color = undefColor;
                    else if (val < 0)
                        color = minColor;
                    else if (val >= colorPalette.Length)
                        color = maxColor;
                    else
                        color = colorPalette[(int)val];
                    pixels[writeableBitmap.PixelWidth * y + x] = color;
                }
            }

            // draw path
            foreach (DTW.Pair p in path) {
                if (p.i1 <= iOffset || p.i2 <= jOffset) {
                    break;
                }
                pixels[writeableBitmap.PixelWidth * (p.i2 - jOffset - 1) + (p.i1 - iOffset - 1)] = pathColor;
            }

            // paint
            writeableBitmap.WritePixels(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight),
                pixels, writeableBitmap.PixelWidth*4, 0);
            InvalidateVisual();
        }

        protected override void OnRender(System.Windows.Media.DrawingContext drawingContext) {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (writeableBitmap != null) {
                drawingContext.DrawDrawing(new ImageDrawing(writeableBitmap,
                    new Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight)));
            }
        }

        private void InitializeBitmap(bool sizeChanged) {
            writeableBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            pixels = new int[writeableBitmap.PixelWidth * writeableBitmap.PixelHeight];
            InvalidateVisual();
        }

        private static int GetColorValue(Color c) {
            return c.A << 24 | c.R << 16 | c.G << 8 | c.B;
        }
    }
}
