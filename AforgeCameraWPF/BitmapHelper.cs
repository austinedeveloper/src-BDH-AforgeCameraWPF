using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32.SafeHandles;

namespace AforgeCameraWPF
{
    public static class BitmapHelper
    {
        // My take on this built from a number of 
        // resources.
        //   http://stackoverflow.com/a/7035036
        //   http://stackoverflow.com/a/1470182/360211
        //

        //public static BitmapSource ToBitmapSource(this Bitmap source)
        public static BitmapSource GetBitmapSourceWithHBitmap(Bitmap source)
        {
            using (var handle = new SafeHBitmapHandle(source))
            {
                BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(handle.DangerousGetHandle(),
                    IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(source.Width, source.Height));


                //BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(handle.DangerousGetHandle(),
                //    IntPtr.Zero, Int32Rect.Empty,
                //    BitmapSizeOptions.FromEmptyOptions());

                return bs;
            }
        }

        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        private sealed class SafeHBitmapHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            [SecurityCritical]
            public SafeHBitmapHandle(Bitmap bitmap)
                : base(true)
            {
                SetHandle(bitmap.GetHbitmap());
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            protected override bool ReleaseHandle()
            {
                return DeleteObject(handle) > 0;
            }
        }

        private static ImageSourceConverter imageSourceConverter = new ImageSourceConverter();
        public static ImageSource ConvertBitmapToImageSource(Bitmap source)
        {
            ImageSource convertedImageSource = null;

            try
            {
                if (source != null)
                {
                    //var imageSourceConverter = new ImageSourceConverter();
                    using (var memoryStream = new MemoryStream())
                    {
                        source.Save(memoryStream, ImageFormat.Bmp);
                        byte[] snapshotBytes = memoryStream.ToArray();
                        convertedImageSource = (ImageSource)imageSourceConverter.ConvertFrom(snapshotBytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "BitmapToImageSourceConverter");
                convertedImageSource = null;
            }

            return convertedImageSource;
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void CopyMemory(IntPtr dest, IntPtr source, int Length);

        public static WriteableBitmap GetWritableBitmap(Bitmap source)
        {
            WriteableBitmap wBitmap = null;
            BitmapData data = null;
            try
            {
                data = source.LockBits(
                    new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                wBitmap = new WriteableBitmap(
                        source.Width,
                        source.Height,
                        96,
                        96,
                        PixelFormats.Pbgra32,
                        null);

                wBitmap.Lock();
                CopyMemory(wBitmap.BackBuffer, data.Scan0, (wBitmap.BackBufferStride * source.Height));
                wBitmap.AddDirtyRect(new Int32Rect(0, 0, source.Width, source.Height));
                wBitmap.Unlock();
            }
            finally
            {
                source.UnlockBits(data);
            }

            return wBitmap;
        }

        public static BitmapImage GetBitmapImageWithMemoryStream(Bitmap source)
        {
            BitmapImage result = null;
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    source.Save(stream, ImageFormat.Bmp);
                    stream.Position = 0;

                    result = new BitmapImage();
                    result.BeginInit();
                    //result.DecodePixelWidth = 96;
                    //result.DecodePixelHeight = 96;

                    // According to MSDN, "The default OnDemand cache option retains access 
                    // to the stream until the image is needed."  Force the bitmap to load 
                    // right now so we can dispose the stream.
                    result.CacheOption = BitmapCacheOption.OnLoad;
                    result.StreamSource = stream;
                    result.EndInit();
                    result.Freeze();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                result = null;
            }

            return result;
        }


        public static void temp(Bitmap bmp)
        {
            var Wbmp = new WriteableBitmap(bmp.Width, bmp.Height, 96, 96, PixelFormats.Bgr32, null);
            var bdata = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Wbmp.WritePixels(new System.Windows.Int32Rect(0, 0, bdata.Width, bdata.Height), bdata.Scan0, bdata.Stride * bdata.Height, bdata.Stride);
            bmp.UnlockBits(bdata);
            bmp.Dispose();
        }
    }
}
