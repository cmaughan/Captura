﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Captura.Models;
using Screna;

namespace Captura.ViewModels
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class ScreenShotModel : NotifyPropertyChanged
    {
        readonly VideoSourcesViewModel _videoSourcesViewModel;
        readonly ISystemTray _systemTray;
        readonly IRegionProvider _regionProvider;
        readonly IMainWindow _mainWindow;
        readonly IVideoSourcePicker _sourcePicker;
        readonly IAudioPlayer _audioPlayer;
        readonly Settings _settings;
        readonly LanguageManager _loc;
        readonly IPlatformServices _platformServices;

        public IReadOnlyList<IImageWriterItem> AvailableImageWriters { get; }

        public ScreenShotModel(VideoSourcesViewModel VideoSourcesViewModel,
            ISystemTray SystemTray,
            IRegionProvider RegionProvider,
            IMainWindow MainWindow,
            IVideoSourcePicker SourcePicker,
            IAudioPlayer AudioPlayer,
            IEnumerable<IImageWriterItem> ImageWriters,
            Settings Settings,
            LanguageManager Loc,
            IPlatformServices PlatformServices)
        {
            _videoSourcesViewModel = VideoSourcesViewModel;
            _systemTray = SystemTray;
            _regionProvider = RegionProvider;
            _mainWindow = MainWindow;
            _sourcePicker = SourcePicker;
            _audioPlayer = AudioPlayer;
            _settings = Settings;
            _loc = Loc;
            _platformServices = PlatformServices;

            AvailableImageWriters = ImageWriters.ToList();

            if (!AvailableImageWriters.Any(M => M.Active))
                AvailableImageWriters[0].Active = true;
        }

        public async Task ScreenshotRegion()
        {
            var region = _sourcePicker.PickRegion();

            if (region == null)
                return;

            await SaveScreenShot(ScreenShot.Capture(region.Value));
        }

        public async Task ScreenshotWindow()
        {
            var window = _sourcePicker.PickWindow();

            if (window != null)
            {
                var img = ScreenShot.Capture(window.Rectangle);

                await SaveScreenShot(img);
            }
        }

        public async Task ScreenshotScreen()
        {
            var screen = _sourcePicker.PickScreen();

            if (screen != null)
            {
                var img = ScreenShot.Capture(screen.Rectangle);

                await SaveScreenShot(img);
            }
        }

        public async Task SaveScreenShot(IBitmapImage Bmp, string FileName = null)
        {
            _audioPlayer.Play(SoundKind.Shot);

            if (Bmp != null)
            {
                var allTasks = AvailableImageWriters
                    .Where(M => M.Active)
                    .Select(M => M.Save(Bmp, _settings.ScreenShots.ImageFormat, FileName));

                await Task.WhenAll(allTasks).ContinueWith(T => Bmp.Dispose());
            }
            else _systemTray.ShowNotification(new TextNotification(_loc.ImgEmpty));
        }

        public IBitmapImage ScreenShotWindow(IWindow Window)
        {
            _systemTray.HideNotification();

            if (Window.Handle == _platformServices.DesktopWindow.Handle)
            {
                return ScreenShot.Capture(_settings.IncludeCursor);
            }

            try
            {
                IBitmapImage bmp = null;

                if (_settings.ScreenShots.WindowShotTransparent)
                {
                    bmp = ScreenShot.CaptureTransparent(Window, _settings.IncludeCursor);
                }

                // Capture without Transparency
                return bmp ?? ScreenShot.Capture(Window.Rectangle, _settings.IncludeCursor);
            }
            catch
            {
                return null;
            }
        }

        public async void CaptureScreenShot(string FileName = null)
        {
            _systemTray.HideNotification();

            var bmp = await GetScreenShot();

            await SaveScreenShot(bmp, FileName);
        }

        public async Task<IBitmapImage> GetScreenShot(bool SuppressHide = false)
        {
            IBitmapImage bmp = null;

            var selectedVideoSource = _videoSourcesViewModel.SelectedVideoSourceKind?.Source;
            var includeCursor = _settings.IncludeCursor;

            switch (_videoSourcesViewModel.SelectedVideoSourceKind)
            {
                case WindowSourceProvider _:
                    var hWnd = _platformServices.DesktopWindow;

                    switch (selectedVideoSource)
                    {
                        case WindowItem windowItem:
                            hWnd = windowItem.Window;
                            break;
                    }

                    bmp = ScreenShotWindow(hWnd);
                    break;

                case DeskDuplSourceProvider _:
                    if (selectedVideoSource is DeskDuplItem deskDuplItem)
                    {
                        bmp = ScreenShot.Capture(deskDuplItem.Rectangle, includeCursor);
                    }
                    break;

                case FullScreenSourceProvider _:
                    var hide = !SuppressHide && _mainWindow.IsVisible && _settings.UI.HideOnFullScreenShot;

                    if (hide)
                    {
                        _mainWindow.IsVisible = false;

                        // Ensure that the Window is hidden
                        await Task.Delay(300);
                    }

                    bmp = ScreenShot.Capture(includeCursor);

                    if (hide)
                        _mainWindow.IsVisible = true;
                    break;

                case ScreenSourceProvider _:
                    if (selectedVideoSource is ScreenItem screen)
                        bmp = ScreenShot.Capture(screen.Screen.Rectangle, includeCursor);
                    break;

                case RegionSourceProvider _:
                    bmp = ScreenShot.Capture(_regionProvider.SelectedRegion, includeCursor);
                    break;
            }

            return bmp;
        }
    }
}