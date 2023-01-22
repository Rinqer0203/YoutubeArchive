using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using YoutubeExplode.Videos;
using YoutubeExplode.Playlists;
using YoutubeExplode.Channels;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Common;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using System.Media;
using System.Threading;
using System.Diagnostics;
using MaterialDesignThemes.Wpf;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Reflection.Metadata;

//並列ダウンロードテストの結果1:30程度の1080pの動画を60個同時にダウンロードすることができた
/*  tasks
    ダウンロード時にファイル名が重複するときの処理の追加
    ダウンロード速度を表示させる
    並列ダウンロード時に({ダウンロード済みの動画数}/{ダウンロード予定動画数})を左下のテキストボックスに追加
    設定ウィンドウの追加
    同じURLのアイテムをlistに追加させない
    保存先パスを事前に設定させる
    ダウンロードリストの選択を複数できるようにする
    エラーURLに対する処理（一括ダウンロード）
    名前が重複したときに(1) or 上書きで設定項目を作る
//*/
namespace YoutubeChannelArchive
{

    public partial class MainPage : Page
    {
        public static MainPage? _instance { get; private set; } = null;
        private YoutubeFunc _youtube = new YoutubeFunc();
        private BitmapImage _soundThumbnail = new BitmapImage();
        private CancellationTokenSource? _tokenSource = null;
        private bool _isBusy = false;
        private bool _isDownloadingErrList = false;

        private enum addListType { video, playlist, channel };

        public MainPage()
        {
            InitializeComponent();
            _instance ??= this;

            //UrlTextBoxにフォーカスしたときに全選択するイベントを追加
            UrlTextBox.GotFocus += (s, e) =>
            {
                UrlTextBox.SelectAll();
            };

            UrlTextBox.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (UrlTextBox.IsFocused)
                {
                    UrlTextBox.SelectionLength = 0;
                    return;
                }
                e.Handled = true;
                UrlTextBox.Focus();
            };

            //テーマを設定（前回のテーマを引き継ぐ）
            SetDarkTheme(Settings.Default.IsDarkTheme);

            //保存先フォルダパスリストに特殊パスを追加する
            //ダウンロード
            SavePathComboBox.Items.Add(System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
            // ドキュメント
            SavePathComboBox.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            // デスクトップ
            SavePathComboBox.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            // ピクチャ
            SavePathComboBox.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            // ビデオ
            SavePathComboBox.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            // ミュージック
            SavePathComboBox.Items.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            //最後に選択されていたパス
            if (Directory.Exists(Settings.Default.UserPath))
            {
                SavePathComboBox.Items.Insert(0, Settings.Default.UserPath);
            }
            SavePathComboBox.SelectedIndex = 0;

            //サウンドのサムネイルデータの設定
            using (FileStream stream = File.OpenRead(@"Images/soundOnly.png"))
            {
                _soundThumbnail.BeginInit();
                _soundThumbnail.StreamSource = stream;
                _soundThumbnail.DecodePixelWidth = 500;
                _soundThumbnail.CacheOption = BitmapCacheOption.OnLoad;
                _soundThumbnail.CreateOptions = BitmapCreateOptions.None;
                _soundThumbnail.EndInit();
                _soundThumbnail.Freeze();
            }
        }

        public static MainPage GetInstance()
        {
            return _instance ?? new MainPage();
        }

        //進捗バーの被コールバック関数
        private void OnProgressChanged((double progress, int completeNum , int failureNum ) progressInfo)
        {
            DownloadProgress.Value = progressInfo.progress;

            double num = Math.Round((progressInfo.progress * 100) / 1, 1, MidpointRounding.AwayFromZero);
            DownloadStateLabel.Content = $"ダウンロード中 ：{(string.Format("{0:F1}", num)).PadLeft(4)}%  " +
                $"(完了：{progressInfo.completeNum}  失敗：{progressInfo.failureNum})";
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            grid1.Focus();
        }

        private void SetDarkTheme(bool isDarkTheme)
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            Theme theme = (Theme)paletteHelper.GetTheme();
            if (isDarkTheme)
            {
                theme.SetBaseTheme(Theme.Dark);
            }
            else
            {
                theme.SetBaseTheme(Theme.Light);
            }
            Settings.Default.IsDarkTheme = isDarkTheme;
            paletteHelper.SetTheme(theme);
        }

        private List<UiVideoInfo> GetDownloadTargetList()
        {
            if (DownloadListTabControl.SelectedIndex == 0)
            {
                return DownloadList.ListBox.Items.Cast<UiVideoInfo>().ToList();
            }
            else
            {
                return ErrDownloadList.ListBox.Items.Cast<UiVideoInfo>().ToList();
            }
        }

        //ボタンクリックイベント------------------------------------------------

        private async void AddDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            string url = UrlTextBox.Text;
            string errMsgBase = "を取得できませんでした\nURLが不正な可能性があります";

            //引数で渡されたurlタイプによってアイコンを可視化させリストに追加する
            void AddList(string title, Thumbnail thumbnail, string url, addListType type)
            {
                string itemTitle;
                BitmapImage itemThumbnail;

                if (DownloadExtensionTypeComboBox.Text == "動画")
                {
                    itemTitle = title + ".mp4";
                    itemThumbnail = new BitmapImage(new Uri(thumbnail.Url));
                }
                else
                {
                    itemTitle = title + ".mp3";
                    itemThumbnail = _soundThumbnail;
                }

                //重複するタイトルのアイテムがすでに存在していれば追加しない
                foreach (UiVideoInfo item in DownloadList.ListBox.Items)
                {
                    if (item.Title.Text.Contains(itemTitle))
                    {
                        return;
                    }
                }

                var uiVideoInfo = new UiVideoInfo();
                uiVideoInfo.Title.Text = itemTitle;
                uiVideoInfo.Thumbnail.Source = itemThumbnail;
                uiVideoInfo.Url.Text = url;

                //タイプごとのアイコンの設定
                switch (type)
                {
                    case addListType.video:
                        uiVideoInfo.VideoIcon.Visibility = Visibility.Visible;
                        break;
                    case addListType.playlist:
                        uiVideoInfo.PlaylistIcon.Visibility = Visibility.Visible;
                        break;
                    case addListType.channel:
                        uiVideoInfo.ChannelIcon.Visibility = Visibility.Visible;
                        break;
                }
                DownloadList.ListBox.Items.Add(uiVideoInfo);

                //タブをダウンロードリストを選択している状態にする
                DownloadListTabControl.SelectedIndex = 0;
            }

            if (UrlActionComboBox.Text == "単体ダウンロード")
            {
                var videoInfo = await _youtube.GetVideoInfoAsync(url);

                if (videoInfo == null)
                {
                    await DialogHost.Show(new MsgBox("動画データ" + errMsgBase));
                    return;
                }

                AddList(videoInfo.Title, videoInfo.Thumbnails.First(), videoInfo.Url, addListType.video);
            }
            else if (UrlActionComboBox.Text == "プレイリストダウンロード")
            {
                var playlistInfo = await _youtube.GetPlayListInfoAsync(url);

                if (playlistInfo == null)
                {
                    await DialogHost.Show(new MsgBox("プレイリストの情報" + errMsgBase));
                    return;
                }

                AddList(playlistInfo.Title, playlistInfo.Thumbnails.First(), playlistInfo.Url, addListType.playlist);
            }
            else if (UrlActionComboBox.Text == "チャンネルダウンロード")
            {
                var channelInfo = await _youtube.GetChannelInfoAsync(url);

                if (channelInfo == null)
                {
                    await DialogHost.Show(new MsgBox("チャンネルの情報" + errMsgBase));
                    return;
                }

                AddList(channelInfo.Title, channelInfo.Thumbnails.First(), channelInfo.Url, addListType.channel);
            }

        }

        private async void AllItemsDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            List<UiVideoInfo> uiVideoInfos = GetDownloadTargetList();
            if (uiVideoInfos.Count == 0)
            {
                await DialogHost.Show(new MsgBox("ダウンロードリストにアイテムがありません"));
            }
            else
            {
                DownloadListItems(uiVideoInfos);
            }
        }

        private async void SelectedItemsDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            List<UiVideoInfo> uiVideoInfos = GetDownloadTargetList();
            if (uiVideoInfos.Count == 0)
            {
                await DialogHost.Show(new MsgBox("アイテムを選択してください"));
            }
            else
            {
                DownloadListItems(uiVideoInfos);
            }
        }

        private void DarkModeButton_Click(object sender, RoutedEventArgs e)
        {
            SetDarkTheme(!Settings.Default.IsDarkTheme);
        }

        private void SavePathReferenceButton_Click(object sender, RoutedEventArgs e)
        {
            string? path = SaveFolderPathDialog();
            if (path != null)
            {
                Settings.Default.UserPath = path;
                if (SavePathComboBox.Items.Contains(path))
                {
                    SavePathComboBox.Items.Remove(path);
                }
                SavePathComboBox.Items.Insert(0, path);
                SavePathComboBox.SelectedItem = path;
            }
        }

        private void BinButton_Click(object sender, RoutedEventArgs e)
        {
            ListBox targetListBox;
            if (DownloadListTabControl.SelectedIndex == 0)
            {
                targetListBox = DownloadList.ListBox;
            }
            else
            {
                targetListBox = ErrDownloadList.ListBox;
            }

            while (targetListBox.SelectedItem != null)
            {
                targetListBox.Items.Remove(targetListBox.SelectedItem);
            }
        }

        private async void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await DialogHost.Show(new MsgBox("ダウンロードを中断しますか？", true));
            if (_tokenSource != null && result != null && (bool)result)
            {
                _tokenSource.Cancel();
            }
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(SettingPage.GetInstance());
        }
        //------------------------------------------------

        //いらんかも
        internal async Task GetUrlActionType(string url)
        {
            Video? videoInfo = await _youtube.GetVideoInfoAsync(url);
            Playlist? playlistInfo = await _youtube.GetPlayListInfoAsync(url);
            Channel? channelInfo = await _youtube.GetChannelInfoAsync(url);
            Channel? videoChannelInfo = null;
            if (videoInfo != null)
            {
                videoChannelInfo = await _youtube.GetChannelInfoAsync(videoInfo.Author.ChannelUrl);
            }

            await DialogHost.Show(new MsgBox($"videoInfo : {(videoInfo == null ? "なし" : "あり " + videoInfo.Title)}\n" +
                $"playlist: {(playlistInfo == null ? "なし" : "あり " + playlistInfo.Title)}\n" +
                $"channelInfo: {(channelInfo == null ? "なし" : "あり " + channelInfo.Title)}\n" +
                $"videoChannelInfo: {(videoChannelInfo == null ? "なし" : "あり " + videoChannelInfo.Title)}\n"));
        }

        //保存先選択ダイアログ------------------------------------------------

        internal string? SaveFilePathDialog(string defaultFileName, string defaultExt)
        {
            var dialog = new SaveFileDialog
            {
                AddExtension = true,
                DefaultExt = defaultExt,
                FileName = defaultFileName,
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        internal string? SaveFolderPathDialog()
        {
            var dialog = new CommonOpenFileDialog()
            {
                Title = "フォルダを選択してください",
                IsFolderPicker = true,
            };
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                return dialog.FileName;
            }
            return null;
        }

        //ダウンロード系------------------------------------------------

        private async void DownloadListItems(List<UiVideoInfo> downloadList)
        {
            if (_isBusy) return;
            _isBusy = true;
            _isDownloadingErrList = (DownloadListTabControl.SelectedIndex == 0 ? false : true);

            var videoInfos = new List<(string url, string title)>();

            //videoInfosに引数のdownloadListを動画単位に変換して追加する
            foreach (UiVideoInfo item in downloadList)
            {
                if (item.VideoIcon.Visibility == Visibility.Visible)
                {
                    videoInfos.Add((url: item.Url.Text, title: item.Title.Text));
                }
                else if (item.PlaylistIcon.Visibility == Visibility.Visible)
                {
                    var videos = await _youtube.GetPlayListVideosAsync(item.Url.Text);
                    if (videos != null)
                    {
                        foreach (var video in videos)
                        {
                            videoInfos.Add((url: video.Url, title: video.Title + System.IO.Path.GetExtension(item.Title.Text)));
                        }
                    }
                }
                else if (item.ChannelIcon.Visibility == Visibility.Visible)
                {
                    var videos = await _youtube.GetChannelVideosAsync(item.Url.Text);
                    if (videos != null)
                    {
                        foreach (var video in videos)
                        {
                            videoInfos.Add((url: video.Url, title: video.Title + System.IO.Path.GetExtension(item.Title.Text)));
                        }
                    }
                }
            }

            _tokenSource = new CancellationTokenSource();
            var cancelToken = _tokenSource.Token;

            void onComplete(string title)
            {
                if (_isDownloadingErrList)
                {
                    foreach(UiVideoInfo item in ErrDownloadList.ListBox.Items)
                    {
                        if (item.Title.Text == title)
                        {
                            ErrDownloadList.ListBox.Items.Remove(item);
                            return;
                        }
                    }
                }
            }

            async void onError((string url, string title) itemInfo)
            {
                if (_isDownloadingErrList) return;
                //エラーアイテムをエラーダウンロードリストに追加する
                var uiVideoInfo = new UiVideoInfo();
                if (System.IO.Path.GetExtension(itemInfo.title) == ".mp4")
                {
                    var info = await _youtube.GetVideoInfoAsync(itemInfo.url);
                    if (info != null)
                    {
                        uiVideoInfo.ImgSource = new BitmapImage(new Uri(info.Thumbnails.First().Url));
                    }
                }
                else
                {
                    uiVideoInfo.ImgSource = _soundThumbnail;
                }
                uiVideoInfo.Title.Text = itemInfo.title;
                uiVideoInfo.Url.Text = itemInfo.url;
                uiVideoInfo.VideoIcon.Visibility = Visibility.Visible;
                ErrDownloadList.ListBox.Items.Add(uiVideoInfo);
            }

            //videoInfosのダウンロードタスクを生成
            Task task = _youtube.DownloadVideoAsync(videoInfos, SavePathComboBox.Text, progressCallback: OnProgressChanged, 
               Settings.Default.MaxParallelDownloadNum, cancelToken: cancelToken, onCompleteItem: onComplete, onErrorItem:onError);

            await ShowDownloadedDialog(task);

            //ユーザ設定によってダウンロード完了後にリストをクリア
            if (Settings.Default.IsRemoveCompletedItem)
            {
                DownloadList.ListBox.Items.Clear();
            }

            _isBusy = false;
        }

        //単体とプレイリスト、チャンネルの関数いらんかも
        //private async Task DownloadOne(string url)
        //{
        //    Video? vInfo = await _youtube.GetVideoInfoAsync(url);
        //    if (vInfo == null)
        //    {
        //        await DialogHost.Show(new MsgBox("動画データを取得できませんでした"));
        //        return;
        //    }

        //    var savePath = SaveFilePathDialog(GetSafeTitle(vInfo.Title), "mp4");
        //    if (savePath == null)
        //    {
        //        await DialogHost.Show(new MsgBox("キャンセルされました"));
        //        return;
        //    }

        //    await ShowDownloadedDialog(_youtube.DownloadVideoAsync(url, savePath, OnProgressChanged));
        //}
        //private async Task DownloadPlaylist(string url)
        //{
        //    var savePath = SaveFolderPathDialog();
        //    if (savePath == null)
        //    {
        //        await DialogHost.Show(new MsgBox("キャンセルされました"));
        //        return;
        //    }

        //    await ShowDownloadedDialog(_youtube.DownloadPlaylistVideosAsync(url, savePath, OnProgressChanged));
        //}
        //private async Task DownloadChannelVideos(string url)
        //{
        //    var savePath = SaveFolderPathDialog();
        //    if (savePath == null)
        //    {
        //        await DialogHost.Show(new MsgBox("キャンセルさました"));
        //        return;
        //    }

        //    await ShowDownloadedDialog(_youtube.DownaloadChannelVideosAsync(url, savePath, OnProgressChanged));
        //}

        private async Task ShowDownloadedDialog(Task downloadTask)
        {
            try
            {
                await downloadTask;
                await DialogHost.Show(new MsgBox("ダウンロード完了"));
            }
            catch
            {
                await DialogHost.Show(new MsgBox("エラーが発生したためダウンロードが完了しませんでした"));
            }

        }

    }
}