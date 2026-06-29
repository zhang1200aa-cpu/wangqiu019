using System;
using System.Drawing;
using TennisTraining.Core;

namespace TennisTraining.Projection
{
    /// <summary>
    /// 位图画布：把投影内容绘制到一张 Bitmap（供 PictureBox 显示或第二屏无边框窗体投影）。
    /// </summary>
    public sealed class BitmapProjectorView : IProjectorView, IDisposable
    {
        private Bitmap _bitmap;
        private Graphics _graphics;

        public int Width { get; }
        public int Height { get; }
        public Bitmap Bitmap => _bitmap;

        public BitmapProjectorView(int width, int height)
        {
            Width = width; Height = height;
            _bitmap = new Bitmap(width, height);
            _graphics = Graphics.FromImage(_bitmap);
        }

        public Graphics Graphics => _graphics;

        public void BeginDraw() { /* Graphics 已就绪 */ }
        public void EndDraw() { _graphics.Flush(); }

        public void Clear(int argb = unchecked((int)0xFF101820))
        {
            using var b = new SolidBrush(Color.FromArgb(argb));
            _graphics.FillRectangle(b, 0, 0, Width, Height);
        }

        public void Dispose()
        {
            try { _graphics?.Dispose(); _bitmap?.Dispose(); } catch { }
        }
    }
}
