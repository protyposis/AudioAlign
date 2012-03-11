using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using AudioAlign.WaveControls;
using System.Windows.Media;
using AudioAlign.Audio.Matching.Dixon2005;

namespace AudioAlign {
    public class DtwPath : Control {

        /// <summary>
        /// Specified how much times the scroll mode bitmap should be larger than the actual control's width.
        /// The bigger it is, the more memory is consumed, but the less bitmap copy operations need to be executed.
        /// </summary>
        private const int SCROLL_WIDTH_FACTOR = 3;

        private WriteableBitmap writeableBitmap;
        private int position;
        private int[] pixelColumn;
        //private SpectrogramMode mode;

        private IMatrix cellCostMatrix;
        private IMatrix totalCostMatrix;
        private int size;
        int[] pixels;
        int pathColor, minColor, maxColor, undefColor;

        private int[] colorPalette;
        private bool paletteDemo = false;
        private int paletteDemoIndex = 0;

        public bool Drawing { get; set; }

        //public SpectrogramMode Mode {
        //    get { return (SpectrogramMode)GetValue(ModeProperty); }
        //    set { SetValue(ModeProperty, value); }
        //}

        //public static readonly DependencyProperty ModeProperty =
        //    DependencyProperty.Register("Mode", typeof(SpectrogramMode), typeof(Spectrogram),
        //    new UIPropertyMetadata(SpectrogramMode.Scroll, OnModeChanged));

        //private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
        //    Spectrogram spectrogram = d as Spectrogram;
        //    spectrogram.mode = (SpectrogramMode)e.NewValue;
        //}

        public int SpectrogramSize {
            get { return (int)GetValue(SpectrogramSizeProperty); }
            set { SetValue(SpectrogramSizeProperty, value); }
        }

        public static readonly DependencyProperty SpectrogramSizeProperty =
            DependencyProperty.Register("SpectrogramSize", typeof(int), typeof(DtwPath),
            new UIPropertyMetadata(1024) {
                CoerceValueCallback = CoerceSpectrogramSize,
                PropertyChangedCallback = OnSpectrogramSizeChanged
            });

        private static object CoerceSpectrogramSize(DependencyObject d, object value) {
            int i = (int)value;
            return i < 1 ? 1 : i;
        }

        private static void OnSpectrogramSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            DtwPath spectrogram = d as DtwPath;
            spectrogram.InitializeBitmap(true);
        }

        public float Minimum {
            get { return (float)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(float), typeof(DtwPath), new UIPropertyMetadata(-100f));

        public float Maximum {
            get { return (float)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(float), typeof(DtwPath), new UIPropertyMetadata(0f));

        public DtwPath() {
            ColorGradient gradient = new ColorGradient(0, 1);
            gradient.AddStop(Colors.Black, 0);
            gradient.AddStop(Colors.White, 1);
            colorPalette = gradient.GetGradient(500).Select(c => GetColorValue(c)).ToArray();

            pathColor = GetColorValue(Colors.Red);
            minColor = GetColorValue(Colors.Green);
            maxColor = GetColorValue(Colors.Magenta);
            undefColor = GetColorValue(Colors.Blue);

            ClipToBounds = true;
            //mode = Mode;
        }

        public void Init(int size, IMatrix cellCostMatrix, IMatrix totalCostMatrix) {
            this.cellCostMatrix = cellCostMatrix;
            this.totalCostMatrix = totalCostMatrix;
            this.size = 700;
            InitializeBitmap(true);
        }

        public void Refresh(int i, int j, int minI, int minJ) {
            List<DTW.Pair> path = OLTW.OptimalWarpingPath(totalCostMatrix, minI, minJ);

            int iOffset = i - writeableBitmap.PixelWidth;
            int jOffset = j - writeableBitmap.PixelHeight;

            if(iOffset < 0) iOffset = 0;
            if(jOffset < 0) jOffset = 0;

            // draw cost matrix
            int color;
            for (int x = 0; x < writeableBitmap.PixelWidth; x++) {
                for (int y = 0; y < writeableBitmap.PixelHeight; y++) {
                    double val = cellCostMatrix[iOffset + x, jOffset + y] / 1000;
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
            path.Reverse();
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

        protected override void OnRenderSizeChanged(System.Windows.SizeChangedInfo sizeInfo) {
            base.OnRenderSizeChanged(sizeInfo);

            // recreate bitmap for current control size
            //InitializeSpectrogramBitmap(true);
        }

        public void Reset() {
            InitializeBitmap(true);
        }

        private void InitializeBitmap(bool sizeChanged) {
            writeableBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);
            pixels = new int[writeableBitmap.PixelWidth * writeableBitmap.PixelHeight];
            InvalidateVisual();
        }

        private static int GetColorValue(Color c) {
            return c.A << 24 | c.R << 16 | c.G << 8 | c.B;
        }

        private static void CopyPixels(WriteableBitmap src, WriteableBitmap dest) {
            int twoThirds = src.PixelWidth / 3 * 2;
            int third = src.PixelWidth / 3;
            int height = src.PixelHeight;

            Int32Rect srcRect = new Int32Rect(twoThirds, 0, third, height);
            Int32Rect destRect = new Int32Rect(0, 0, third, height);

            //// pixel copy with intermediate buffer
            //int[] buffer = new int[twoThirds * height];
            //src.CopyPixels(srcRect, buffer, third * 4, 0);
            //dest.WritePixels(destRect, buffer, third * 4, 0);

            // direct pixel copy
            dest.WritePixels(srcRect, src.BackBuffer, src.BackBufferStride * src.PixelHeight, 
                src.BackBufferStride, destRect.X, destRect.Y);
        }
    }
}
