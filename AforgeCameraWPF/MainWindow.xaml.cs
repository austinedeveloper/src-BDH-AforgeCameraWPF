using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using AForge.Video.DirectShow;
using Microsoft.Win32.SafeHandles;

namespace AforgeCameraWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        #region FIELDS
        private Dispatcher uiThread;
        private WriteableBitmap Wbmp;
        //private Stopwatch sw;

        private FilterInfoCollection videoSources;
        private VideoCaptureDevice selectedVideoSource;
        private CameraResolutions resolutions;
        private int waitForStopInterval;
        private int viewerResolutionIndexDefault = 0;

        private object newFrameLock;
        private int newFrameSubscribeCount;
        private bool stopRequested;
        private bool bypassSelectionChanged;
        private int lastSuccessfulVideoSourceIdx;
        private int lastSuccessfulResolutionIdx;
        private bool autoHiResSnapshotRequested;
        private bool snapshotRequested;
        private bool flashRequested;
        private bool flashIsOn;
        private int waitForAutoFocus;
        private int waitForAutoFocusLimit;
        private int frameCount;
        private bool snapshotCaptured;
        private MediaPlayer clickPlayer;
        private string lastSavedFilename;
        #endregion

        #region WINDOWEVENTS
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Timeline.DesiredFrameRateProperty.OverrideMetadata(
                //    typeof(Timeline),
                //    new FrameworkPropertyMetadata { DefaultValue = this.selectedVideoSource.VideoResolution.AverageFrameRate }
                //    );

                //Timeline.DesiredFrameRateProperty.OverrideMetadata(
                //    typeof(Timeline),
                //    new FrameworkPropertyMetadata { DefaultValue = 10 }
                //    );

                this.uiThread = Application.Current.Dispatcher;
                this.Wbmp = null;
                //this.sw = new Stopwatch();

                this.videoSources = null;
                this.selectedVideoSource = null;
                this.resolutions = null;
                this.waitForStopInterval = 500;

                //this.NewFrameHandler = new Action(this.NewFrame);
                this.newFrameLock = new object();
                this.newFrameSubscribeCount = 0;
                this.stopRequested = false;
                this.bypassSelectionChanged = true;
                this.lastSuccessfulVideoSourceIdx = 0;
                this.lastSuccessfulResolutionIdx = 0;
                this.autoHiResSnapshotRequested = false;
                this.snapshotRequested = false;
                this.flashRequested = false;
                this.flashIsOn = false;
                this.waitForAutoFocus = 0;
                this.waitForAutoFocusLimit = 2;
                this.frameCount = 0;
                this.snapshotCaptured = false;
                this.clickPlayer = new MediaPlayer();
                this.clickPlayer.Volume = 1;
                this.clickPlayer.Open(new Uri(@"Media\Click.wav", UriKind.Relative));
                this.lastSavedFilename = string.Empty;

                // Initialize the Getac flash DLL
                AforgeCameraWPF.GetacDLLWrapper.InitGetacDLL();
                Log.Debug("getac DLL initialized");

                // Get all available video sources.  Note, this call 
                // populates the list of available video sources if 
                // there are any.

                this.videoSources = new FilterInfoCollection(FilterCategory.VideoInputDevice);

                // Check if atleast one video source is available
                if (this.videoSources != null && this.videoSources.Count > 0)
                {
                    // Bind the video sources combobox
                    Binding videoSourcesBinding = new Binding();
                    videoSourcesBinding.Source = this.videoSources;

                    this.VideoSourcesComboBox.DisplayMemberPath = "Name";
                    this.VideoSourcesComboBox.SelectedValuePath = "MonikerString";
                    this.VideoSourcesComboBox.SetBinding(ComboBox.ItemsSourceProperty, videoSourcesBinding);
                    this.VideoSourcesComboBox.SelectionChanged += VideoSourcesComboBox_SelectionChanged;

                    // Use the first video device.
                    this.selectedVideoSource = new VideoCaptureDevice(videoSources[0].MonikerString);
                    this.VideoSourcesComboBox.SelectedIndex = 0;

                    // Initialize the video source and start it.
                    this.InitializeSelectedVideoSource();
                }

                // Disable the save button.
                this.SaveSnapshotButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                this.bypassSelectionChanged = false;
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop and free the webcam object if application
            // is closing

            Log.Debug("Window closing - stoping selected video source.");
            this.stopRequested = true;
            bool stopped = this.StopSelectedVideoSource(10000);
            if (!stopped)
            {
                Log.Debug("Stop failed in window closing");
                if (this.selectedVideoSource != null)
                {
                    Log.Debug("Unhooked new frame event in window closing");
                    this.HookNewFrame(false);
                }
            }
        }
        #endregion

        #region BUTTONEVENTS
        private void AutoHiResSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.AutoHiResSnapshotButton.IsChecked.HasValue && this.AutoHiResSnapshotButton.IsChecked.Value)
                {
                    this.autoHiResSnapshotRequested = true;
                    this.waitForAutoFocusLimit = 5;
                }
                else
                {
                    this.autoHiResSnapshotRequested = false;
                    this.waitForAutoFocusLimit = 1;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void RequestFlashButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.RequestFlashButton.IsChecked.HasValue && this.RequestFlashButton.IsChecked.Value)
                {
                    this.flashRequested = true;
                }
                else
                {
                    this.flashRequested = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void TakeSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string entry = this.TakeSnapshotButton.Content.ToString();
                if (entry.Contains("Snapshot"))
                {
                    this.SetDocumentPhotoResolutionAndTakePicture();
                }
                else
                {
                    this.ReturnToViewFinder();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void SaveSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.pictureBoxVideo.Source != null)
                {
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    string filename = string.Format(@"{0}\AforgeWPF_{1}_{2}x{3}.jpg",
                        path,
                        DateTime.Now.ToString("yyyyMMMdd_hhmmssfff"),
                        this.selectedVideoSource.VideoResolution.FrameSize.Width,
                        this.selectedVideoSource.VideoResolution.FrameSize.Height);

                    Log.Debug(filename);

                    BitmapSource bmp = (BitmapSource)this.pictureBoxVideo.Source.Clone();

                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    BitmapFrame outputFrame = BitmapFrame.Create(bmp);
                    encoder.Frames.Add(outputFrame);
                    encoder.QualityLevel = 100; //The value range is 1 (lowest quality) to 100 (highest quality) inclusive.
                    using (FileStream file = File.OpenWrite(filename))
                    {
                        encoder.Save(file);
                    }

                    this.lastSavedFilename = filename;
                }

                this.ReturnToViewFinder();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void ShowInViewerButton_Click(object sender, RoutedEventArgs e)
        {
            // Windows Photo Viewer is run by dllhost.exe COM Surrogate.  It does 
            // not have it's own exe file, only dll.
            //
            // To start Windows Photo Viewer from command line, run the dll within
            // a com host.
            // 
            // %SystemRoot%\System32\rundll32.exe "%ProgramFiles%\Windows Photo Viewer\PhotoViewer.dll", ImageView_Fullscreen %1
            //
            // where %1 is a full path to a file.

            try
            {
                string arg = string.Format("\"C:\\Program Files (x86)\\Windows Photo Viewer\\PhotoViewer.dll\", ImageView_Fullscreen  {0}", this.lastSavedFilename);
                System.Diagnostics.Process.Start("C:\\Windows\\System32\\rundll32.exe", arg);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void SetDocumentPhotoResolutionAndTakePicture()
        {
            if (!this.autoHiResSnapshotRequested)
            {
                // Take a photo at the current resolution.

                // Reset
                this.ResetForNextSnapShot();
            }
            else
            {
                // Set to highest resolution unless already set
                // and cue the new frame event handler to take 
                // a picture.

                if (this.lastSuccessfulResolutionIdx != this.selectedVideoSource.VideoCapabilities.Length - 1)
                {
                    // stop
                    bool stopped = this.StopSelectedVideoSource();
                    if (stopped)
                    {
                        // change resolution
                        this.bypassSelectionChanged = true;
                        int last = this.selectedVideoSource.VideoCapabilities.Length - 1;
                        this.ResolutionComboBox.SelectedIndex = last;
                        this.selectedVideoSource.VideoResolution = this.selectedVideoSource.VideoCapabilities[last];
                        this.bypassSelectionChanged = false;

                        // Reset
                        this.ResetForNextSnapShot();

                        // Start
                        this.StartSelectedVideoSource();

                        // Toggle and Disable the hi lo res button
                        this.HiLoResolutionButton.Content = "High Resolution";
                    }
                    else
                    {
                        MessageBox.Show("Hi Resolution snapshot failed.  Please try again.", "REQUEST FAILED", MessageBoxButton.OK, MessageBoxImage.Hand);
                    }
                }
                else
                {
                    // Reset
                    this.ResetForNextSnapShot();
                }
            }
        }

        private void ResetForNextSnapShot()
        {
            // Request a snapshot
            this.snapshotRequested = true;
            this.flashIsOn = false;
            this.waitForAutoFocus = 0;
            this.snapshotCaptured = false;
            this.frameCount = 0;

            // Toggle the button content
            this.TakeSnapshotButton.Content = "View Finder";

            // Enable save
            this.SaveSnapshotButton.IsEnabled = true;
        }

        private void ReturnToViewFinder()
        {
            if (!this.autoHiResSnapshotRequested)
            {
                // Toggle the snapshot button content
                this.TakeSnapshotButton.Content = "Take Snapshot";

                // Disable save.
                this.SaveSnapshotButton.IsEnabled = false;
            }
            else
            {
                // Set to lowest resolution, unless already set,
                // then reset for the next photo.

                if (this.lastSuccessfulResolutionIdx != 0)
                {
                    // stop
                    bool stopped = this.StopSelectedVideoSource();
                    if (stopped)
                    {
                        // change resolution
                        this.bypassSelectionChanged = true;
                        this.ResolutionComboBox.SelectedIndex = 0;
                        this.selectedVideoSource.VideoResolution = this.selectedVideoSource.VideoCapabilities[0];
                        this.bypassSelectionChanged = false;

                        // start
                        this.StartSelectedVideoSource();

                        // Toggle and Enable the hi lo res button
                        this.HiLoResolutionButton.Content = "Low Resolution";
                        this.HiLoResolutionButton.IsEnabled = true;

                        // Toggle the snapshot button content
                        this.TakeSnapshotButton.Content = "Take Snapshot";

                        // Disable save.
                        this.SaveSnapshotButton.IsEnabled = false;
                    }
                }
                else
                {
                    // Toggle and Enable the hi lo res button
                    this.HiLoResolutionButton.Content = "Low Resolution";
                    this.HiLoResolutionButton.IsEnabled = true;

                    // Toggle the snapshot button content
                    this.TakeSnapshotButton.Content = "Take Snapshot";

                    // Disable save.
                    this.SaveSnapshotButton.IsEnabled = false;
                }
            }

            this.snapshotRequested = false;
            this.flashIsOn = false;
            this.waitForAutoFocus = 0;
            this.snapshotCaptured = false;
            this.frameCount = 0;
        }

        private void HiLoResolutionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string entry = this.HiLoResolutionButton.Content.ToString();
                if (entry.Contains("High"))
                {
                    // Its on high.  Toggle to Low.
                    this.ResolutionComboBox.SelectedIndex = 0;
                    this.selectedVideoSource.VideoResolution = this.selectedVideoSource.VideoCapabilities[0];
                }
                else
                {
                    // Its on low.  Toggle to High.
                    int last = this.selectedVideoSource.VideoCapabilities.Length - 1;
                    this.ResolutionComboBox.SelectedIndex = last;
                    this.selectedVideoSource.VideoResolution = this.selectedVideoSource.VideoCapabilities[last];
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void VideoSourcesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (this.bypassSelectionChanged)
                {
                    return;
                }

                if (this.VideoSourcesComboBox.SelectedIndex >= 0)
                {
                    FilterInfo item = (FilterInfo)this.VideoSourcesComboBox.SelectedItem;
                    Log.Debug(string.Format("New video source selected:{0} {1}", item.MonikerString, item.Name));

                    if (item != null)
                    {
                        int idx = -1;
                        foreach (FilterInfo fi in this.videoSources)
                        {
                            ++idx;
                            if (fi.MonikerString == item.MonikerString)
                            {
                                break;
                            }
                        }

                        if (idx >= 0)
                        {
                            bool stopped = this.StopSelectedVideoSource();
                            if (stopped)
                            {
                                this.selectedVideoSource = null;
                                this.selectedVideoSource = new VideoCaptureDevice(this.videoSources[idx].MonikerString);
                                this.InitializeSelectedVideoSource();
                            }
                            else
                            {
                                Log.Debug(string.Format("Resetting video source idx to {0}", this.lastSuccessfulVideoSourceIdx));
                                this.bypassSelectionChanged = true;
                                this.VideoSourcesComboBox.SelectedIndex = this.lastSuccessfulVideoSourceIdx;
                                this.bypassSelectionChanged = false;
                                MessageBox.Show("Your request failed.  Video resource reset.  Please try again.", "REQUEST FAILED", MessageBoxButton.OK, MessageBoxImage.Hand);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void ResolutionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (this.bypassSelectionChanged)
                {
                    return;
                }

                Log.Debug("Resolution selection changed.");

                if (this.ResolutionComboBox.SelectedIndex >= 0)
                {
                    CameraResolution item = (CameraResolution)this.ResolutionComboBox.SelectedItem;
                    Log.Debug(string.Format("{0} {1}", item.Id, item.Display));

                    if (item != null)
                    {
                        int idx = this.resolutions.SetSelectedItem(item);
                        if (idx >= 0)
                        {
                            bool stopped = this.StopSelectedVideoSource();

                            if (!this.selectedVideoSource.IsRunning)
                            {
                                Log.Debug(string.Format("Resetting resolution - status is:{0}", this.selectedVideoSource.IsRunning));
                                this.selectedVideoSource.VideoResolution = this.selectedVideoSource.VideoCapabilities[idx];
                                Log.Debug(string.Format("Resolution after reset:{0},{1}", this.selectedVideoSource.VideoResolution.FrameSize.Width, this.selectedVideoSource.VideoResolution.FrameSize.Height));
                                this.StartSelectedVideoSource();

                                int last = this.selectedVideoSource.VideoCapabilities.Length - 1;

                                // Coordinate the auto hi res picture button
                                if (this.ResolutionComboBox.SelectedIndex != 0)
                                {
                                    this.autoHiResSnapshotRequested = false;
                                }

                                // Cooridinate hi lo res button.
                                if (this.ResolutionComboBox.SelectedIndex == last)
                                {
                                    this.HiLoResolutionButton.Content = "High Resolution";
                                }
                                else
                                {
                                    this.HiLoResolutionButton.Content = "Low Resolution";
                                }

                                // Toggle the button content.
                                this.TakeSnapshotButton.Content = "Take Snapshot";

                                // Enable save
                                this.SaveSnapshotButton.IsEnabled = false;
                            }
                            else
                            {
                                Log.Debug("Reset of resolution failed");
                                Log.Debug(string.Format("Resetting resolution idx to {0}", this.lastSuccessfulResolutionIdx));
                                this.bypassSelectionChanged = true;
                                this.ResolutionComboBox.SelectedIndex = this.lastSuccessfulResolutionIdx;
                                this.bypassSelectionChanged = false;
                                MessageBox.Show("Your request to reset resolution failed. Please try again.", "REQUEST FAILED", MessageBoxButton.OK, MessageBoxImage.Hand);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
        #endregion

        #region CAMERAEVENTS
        private void videoSource_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            if (this.snapshotRequested && this.snapshotCaptured)
            {
                // To capture a snapshot, just stop updating the window 
                // with the new bitmap.  The last captured imagesource
                // represents the snapshot.

                return;
            }

            if (this.stopRequested)
            {
                Log.Debug("NEW FRAME BONKED ON STOP REQUESTED XXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                return;
            }

            Bitmap clonedBitmap = (Bitmap)eventArgs.Frame.Clone();

            // Control access using a lock.  The points of contention 
            // are between stopping and starting the video source and 
            // getting new frames from Aforge.
            if (Monitor.TryEnter(this.newFrameLock))
            {
                try
                {
                    if (this.uiThread != null && !this.uiThread.CheckAccess())
                    {
                        this.uiThread.Invoke(() =>
                        {
                            try
                            {
                                if (this.Wbmp == null || this.Wbmp.Width != clonedBitmap.Width || this.Wbmp.Height != clonedBitmap.Height)
                                {
                                    this.Wbmp = new WriteableBitmap(clonedBitmap.Width, clonedBitmap.Height, 96, 96, PixelFormats.Bgr32, null);
                                    this.pictureBoxVideo.Source = Wbmp;
                                }

                                //sw.Restart();
                                BitmapData bdata = clonedBitmap.LockBits(new System.Drawing.Rectangle(0, 0, clonedBitmap.Width, clonedBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                //sw.Stop();
                                //Log.Debug(string.Format("ElapsedTime:{0} at {1}x{2}", sw.Elapsed, this.selectedVideoSource.VideoResolution.FrameSize.Width, this.selectedVideoSource.VideoResolution.FrameSize.Height));

                                Wbmp.WritePixels(new System.Windows.Int32Rect(0, 0, bdata.Width, bdata.Height), bdata.Scan0, bdata.Stride * bdata.Height, bdata.Stride);
                                clonedBitmap.UnlockBits(bdata);
                                clonedBitmap.Dispose();

                                if (this.snapshotRequested)
                                {
                                    Log.Debug("--");
                                    Log.Debug("SnapshotRequested");
                                    if (this.waitForAutoFocus > this.waitForAutoFocusLimit)
                                    {
                                        if (this.flashRequested)
                                        {
                                            Log.Debug("Flash requested");
                                            if (this.flashIsOn)
                                            {
                                                if (this.frameCount > 0)
                                                {

                                                    Log.Debug("Flash is ON");
                                                    Log.Debug("Taking picture");
                                                    this.snapshotCaptured = true;
                                                    if (this.clickPlayer != null)
                                                    {
                                                        clickPlayer.Play();
                                                        clickPlayer.Position = TimeSpan.FromSeconds(0);
                                                    }

                                                    Log.Debug("Snapshot captured");
                                                }
                                                else
                                                {
                                                    Log.Debug(string.Format("Incrementing frame count: {0}", this.frameCount));
                                                    this.frameCount++;
                                                }
                                            }
                                            else
                                            {
                                                if (this.autoHiResSnapshotRequested)
                                                {
                                                    Log.Debug("Turning on LONG flash");
                                                    //uint interval = 2000;
                                                    //AforgeCameraWPF.GetacDLLWrapper.CameraFlashLEDON_Timer(interval);
                                                    AforgeCameraWPF.GetacDLLWrapper.CameraFlashLEDON();
                                                }
                                                else
                                                {
                                                    Log.Debug("Turning flash ON");
                                                    AforgeCameraWPF.GetacDLLWrapper.CameraFlashLEDON();
                                                }

                                                this.flashIsOn = true;
                                            }
                                        }
                                        else
                                        {
                                            Log.Debug("Taking picture");
                                            this.snapshotCaptured = true;
                                            if (this.clickPlayer != null)
                                            {
                                                clickPlayer.Play();
                                                clickPlayer.Position = TimeSpan.FromSeconds(0);
                                            }

                                            Log.Debug("Snapshot captured");
                                        }
                                    }
                                    else
                                    {
                                        Log.Debug(string.Format("Incrementing auto focus counter {0}", this.waitForAutoFocus));
                                        ++this.waitForAutoFocus;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex);
                            }
                            finally
                            {
                                clonedBitmap.Dispose();
                            }
                        },
                        DispatcherPriority.Render);
                    }
                    else
                    {
                        Log.Debug("uiThread is null");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
                finally
                {
                    Monitor.Exit(this.newFrameLock);
                }
            }
            else
            {
                Log.Debug("NEW FRAME BONKED ON LOCK XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
            }
        }

        private void videoSource_Exception(object sender, AForge.Video.VideoSourceErrorEventArgs eventArgs)
        {
            try
            {
                Log.Debug("********** Video Source Exception **********");
                Log.Debug(eventArgs.Description);
                Log.Debug("********************************************");
            }
            catch
            {
            }
        }
        #endregion

        #region CAMERACOMMANDS
        private void InitializeSelectedVideoSource()
        {
            try
            {
                if (this.videoSources == null)
                {
                    Log.Debug("Initialize bypassed - video sources is null");
                    return;
                }

                if (this.videoSources.Count == 0)
                {
                    Log.Debug("Initialize bypassed - video sources count is zero.");
                    return;
                }

                if (this.selectedVideoSource == null)
                {
                    Log.Debug("Initialize bypassed - selected video source is null.");
                    return;
                }

                if (this.selectedVideoSource.IsRunning)
                {
                    Log.Debug("Initialize bypassed - selected video source is running.");
                    return;
                }

                if (this.selectedVideoSource.VideoCapabilities == null)
                {
                    Log.Debug("Initialize bypassed - selected video source capabilities is null.");
                    return;
                }

                if (this.selectedVideoSource.VideoCapabilities.Length == 0)
                {
                    Log.Debug("Initialize bypassed - selected video source capabilities length is zero.");
                    return;
                }

                Log.Debug("Initializing video source");

                Binding resolutionsBinding = null;
                if (this.resolutions == null)
                {
                    // First time in.

                    Log.Debug("Creating resolution collection");
                    this.resolutions = new CameraResolutions();
                    this.resolutions.ResetItems(this.selectedVideoSource.VideoCapabilities);

                    // Bind the resolutions combobox
                    Log.Debug("Binding resolutions combobox");
                    resolutionsBinding = new Binding();
                    resolutionsBinding.Source = this.resolutions.Items;
                    this.ResolutionComboBox.DisplayMemberPath = "Display";
                    this.ResolutionComboBox.SelectedValuePath = "Id";
                    this.ResolutionComboBox.SetBinding(ComboBox.ItemsSourceProperty, resolutionsBinding);
                    this.ResolutionComboBox.SelectionChanged += this.ResolutionComboBox_SelectionChanged;
                }
                else
                {
                    Log.Debug("Clearing resolutions binding");
                    BindingOperations.ClearBinding(this.ResolutionComboBox, ComboBox.ItemsSourceProperty);
                    Log.Debug("Resetting resolution collection");
                    this.resolutions.ResetItems(this.selectedVideoSource.VideoCapabilities);
                    Log.Debug("Rebinding resolutions combobox");
                    resolutionsBinding = new Binding();
                    resolutionsBinding.Source = this.resolutions.Items;
                    this.ResolutionComboBox.SetBinding(ComboBox.ItemsSourceProperty, resolutionsBinding);
                }

                // Select the highest resolution
                //int highestRes = this.resolutions.IndexOfHighestResolution();
                //this.ResolutionComboBox.SelectedIndex = highestRes;
                //this.selectedVideoSource.VideoResolution = selectedVideoSource.VideoCapabilities[highestRes];
                // todo selecting the first resolution so that I can test the high res snapshot approach.

                this.bypassSelectionChanged = true;
                this.ResolutionComboBox.SelectedIndex = viewerResolutionIndexDefault;
                this.selectedVideoSource.VideoResolution = selectedVideoSource.VideoCapabilities[viewerResolutionIndexDefault];
                this.bypassSelectionChanged = false;

                // Start the video source.
                Log.Debug("Starting selected video source");
                this.StartSelectedVideoSource();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private void StartSelectedVideoSource()
        {
            Log.Debug((string.Format("Status before start:{0}", this.selectedVideoSource.IsRunning)));
            if (this.selectedVideoSource != null && !this.selectedVideoSource.IsRunning)
            {
                this.HookNewFrame(true);
                this.selectedVideoSource.Start();
                Log.Debug((string.Format("Status after start:{0}", this.selectedVideoSource.IsRunning)));

                this.lastSuccessfulVideoSourceIdx = this.VideoSourcesComboBox.SelectedIndex;
                this.lastSuccessfulResolutionIdx = this.ResolutionComboBox.SelectedIndex;
                Log.Debug(string.Format("Saved last successful indexes - Video Source:{0} Resolution:{1}", this.lastSuccessfulVideoSourceIdx, this.lastSuccessfulResolutionIdx));
            }
            else
            {
                Log.Debug("Start bypassed");
            }
        }

        private bool StopSelectedVideoSource(int timeoutInterval = 5000)
        {
            Log.Debug("Stopping video source");
            bool stopped = false;

            // When this is true, the new frame event handler
            // will bonk out immediately which, hopefully, will 
            // give this method a better chance to get the lock.
            this.stopRequested = true;

            try
            {
                if (Monitor.TryEnter(this.newFrameLock, timeoutInterval))
                {
                    try
                    {
                        if (this.selectedVideoSource != null)
                        {
                            this.HookNewFrame(false);

                            this.selectedVideoSource.SignalToStop();
                            Log.Debug((string.Format("Status after signal stop:{0}", this.selectedVideoSource.IsRunning)));
                            Thread.Sleep(this.waitForStopInterval);
                            Log.Debug("ABOUT TO WAIT FOR STOP");
                            this.selectedVideoSource.WaitForStop();
                            Log.Debug("STOP COMPLETED");


                            int counter = 0;
                            while (this.selectedVideoSource.IsRunning && counter < 20)
                            {
                                ++counter;
                                Log.Debug(string.Format("Waiting for stop:{0}", counter));
                                Thread.Sleep(this.waitForStopInterval);
                            }

                            if (this.selectedVideoSource.IsRunning)
                            {
                                Log.Debug(string.Format("Stop failed - IsRunning:{0}", this.selectedVideoSource.IsRunning));
                            }
                            else
                            {
                                Log.Debug(string.Format("Stop succeeded - IsRunning:{0}", this.selectedVideoSource.IsRunning));
                                stopped = true;
                            }
                        }
                        else
                        {
                            Log.Debug("Stop bypassed - selected video source is null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                    finally
                    {
                        Monitor.Exit(this.newFrameLock);
                    }
                }
                else
                {
                    Log.Debug("XXXXXXXXXXXXX STOP FAILED TO GET THE LOCK");
                }
            }
            finally
            {
                this.stopRequested = false;
            }

            return stopped;
        }

        private void HookNewFrame(bool hook)
        {
            Log.Debug(string.Format("HookNewFrame:{0}", hook));
            try
            {
                if (this.selectedVideoSource != null)
                {
                    if (hook)
                    {
                        if (this.newFrameSubscribeCount == 0)
                        {
                            this.selectedVideoSource.NewFrame += new AForge.Video.NewFrameEventHandler(videoSource_NewFrame);
                            this.selectedVideoSource.VideoSourceError += new AForge.Video.VideoSourceErrorEventHandler(videoSource_Exception);
                            this.selectedVideoSource.ProvideSnapshots = true;
                            this.selectedVideoSource.Start();
                            this.newFrameSubscribeCount++;
                            Log.Debug(string.Format("Hooked to new frame:{0}", this.newFrameSubscribeCount));
                        }
                        else
                        {
                            Log.Debug(string.Format("WARNING: HOOK TO NEW FRAME BYPASSED - COUNT IS:{0}", this.newFrameSubscribeCount));
                        }
                    }
                    else
                    {
                        if (this.newFrameSubscribeCount > 0)
                        {
                            for (int u = 0; u < this.newFrameSubscribeCount; u++)
                            {
                                this.selectedVideoSource.NewFrame -= this.videoSource_NewFrame;
                                this.selectedVideoSource.VideoSourceError -= new AForge.Video.VideoSourceErrorEventHandler(videoSource_Exception);
                                Log.Debug(string.Format("Unhooked new frame:{0}", this.newFrameSubscribeCount));
                                this.newFrameSubscribeCount--;
                            }
                        }
                        else
                        {
                            Log.Debug(string.Format("WARNING: UNHOOK FROM NEW FRAME BYPASSED - COUNT IS:{0}", this.newFrameSubscribeCount));
                        }
                    }
                }
                else
                {
                    Log.Debug("Hook to new frame bypassed -- selected video source is null");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
        #endregion

        private double zoomValue = 1;

        private void ZoomInSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.zoomValue < 4)
            {
                this.zoomValue++;
                pictureScale.ScaleX = zoomValue;
                pictureScale.ScaleY = zoomValue;
                pictureScale.CenterX = this.pictureBoxVideo.RenderSize.Width / 2;
                pictureScale.CenterY = this.pictureBoxVideo.RenderSize.Height / 2;
            }
        }

        private void ZoomOutSnapshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.zoomValue > 1)
            {
                this.zoomValue--;
                pictureScale.ScaleX = zoomValue;
                pictureScale.ScaleY = zoomValue;
                pictureScale.CenterX = this.pictureBoxVideo.RenderSize.Width / 2;
                pictureScale.CenterY = this.pictureBoxVideo.RenderSize.Height / 2;
            }
        }
    }
}

