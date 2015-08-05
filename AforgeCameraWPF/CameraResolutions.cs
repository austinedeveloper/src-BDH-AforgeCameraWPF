using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AForge.Video.DirectShow;

namespace AforgeCameraWPF
{
    public class CameraResolutions
    {
        public CameraResolutions()
        {
            this.items = null;
            this.selectedItem = null;
        }

        private List<CameraResolution> items;
        public List<CameraResolution> Items
        {
            get { return this.items; }
        }

        private CameraResolution selectedItem;
        public CameraResolution SelectedItem
        {
            get { return this.selectedItem; }
        }

        public void ResetItems(VideoCapabilities[] vidCaps)
        {
            if (this.items == null)
            {
                this.items = new List<CameraResolution>();
            }
            else
            {
                this.items.Clear();
            }

            if (vidCaps == null || vidCaps.Length == 0)
            {
                return;
            }

            foreach (VideoCapabilities vidCap in vidCaps)
            {
                this.items.Add(new CameraResolution(vidCap));
            }
        }

        public int SetSelectedItem(CameraResolution item)
        {
            int idx = -1;
            foreach (CameraResolution r in this.items)
            {
                ++idx;
                if (r.Id == item.Id)
                {
                    break;
                }
            }

            if (idx >= 0)
            {
                this.selectedItem = this.items[idx];
            }

            return idx;
        }

        public int IndexOfSelectedItem()
        {
            if (this.items == null)
            {
                return -1;
            }

            int idx = -1;
            foreach (CameraResolution r in this.items)
            {
                ++idx;
                if (r.Id == selectedItem.Id)
                {
                    break;
                }
            }

            return idx;
        }

        public int IndexOfHighestResolution()
        {
            if (this.items == null || this.items.Count == 0)
            {
                this.selectedItem = null;
                return -1;
            }

            int idx = 0;

            int tempIdx = -1;
            Size temp = new Size(0, 0);
            foreach (CameraResolution r in this.items)
            {
                ++tempIdx;
                if (r.FrameSize.Width > temp.Width)
                {
                    idx = tempIdx;
                }
            }

            this.selectedItem = this.items[idx];

            return idx;
        }
    }

    public class CameraResolution
    {
        public CameraResolution(VideoCapabilities vidCap)
        {
            this.id = Guid.NewGuid();
            this.frameSize = vidCap.FrameSize;
            this.frameRate = vidCap.AverageFrameRate;
        }

        private Guid id;
        public Guid Id
        {
            get { return this.id; }
        }

        private Size frameSize;
        public Size FrameSize
        {
            get { return this.frameSize; }
        }

        private int frameRate;
        public int FrameRate
        {
            get { return this.frameRate; }
        }

        public string Display
        {
            get { return string.Format("{0},{1}|{2}", this.frameSize.Width, this.frameSize.Height, this.frameRate); }
        }
    }
}
