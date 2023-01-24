using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using YoutubeExplode.Common;

namespace YoutubeArchive
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
            _soundThumbnail = new BitmapImage(new Uri("/Resources/soundOnly.png", UriKind.Relative));
        }

        public static MainPage GetInstance()
        {
            return _instance ?? new MainPage();
        }

        //進捗バーの被コールバック関数
        private void OnProgressChanged((double progress, int completeNum, int failureNum) progressInfo)
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

        //選択されているタブに応じてダウンロードリストorエラーダウンロードリストのListBoxを返す
        private List<UiVideoInfo> GetDownloadTargetList(bool isSelectedItems)
        {
            if (DownloadListTabControl.SelectedIndex == 0)
            {
                if (isSelectedItems)
                    return DownloadList.ListBox.SelectedItems.Cast<UiVideoInfo>().ToList();
                else
                    return DownloadList.ListBox.Items.Cast<UiVideoInfo>().ToList();
            }
            else
            {
                if (isSelectedItems)
                    return ErrDownloadList.ListBox.SelectedItems.Cast<UiVideoInfo>().ToList();
                else
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
            List<UiVideoInfo> uiVideoInfos = GetDownloadTargetList(false);
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
            List<UiVideoInfo> uiVideoInfos = GetDownloadTargetList(true);
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

        //ダウンロード処理
        private async void DownloadListItems(List<UiVideoInfo> targetDownloadList)
        {
            if (_isBusy) return;
            _isBusy = true;
            _isDownloadingErrList = (DownloadListTabControl.SelectedIndex == 0 ? false : true);

            var videoInfos = new List<(string url, string title)>();

            //videoInfosに引数のdownloadListを動画単位に変換して追加する
            foreach (UiVideoInfo item in targetDownloadList)
            {
                if (item.VideoIcon.Visibility == Visibility.Visible)
                {
                    videoInfos.Add((url: item.Url.Text, title: await GetSaveVideoTitle(item.Title.Text, item.Url.Text)));
                }
                else if (item.PlaylistIcon.Visibility == Visibility.Visible)
                {
                    var videos = await _youtube.GetPlayListVideosAsync(item.Url.Text);
                    if (videos != null)
                    {
                        foreach (var video in videos)
                        {
                            string title = await GetSaveVideoTitle(video.Title, System.IO.Path.GetExtension(item.Title.Text), video.Url);
                            videoInfos.Add((url: video.Url, title: title));
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
                            string title = await GetSaveVideoTitle(video.Title, System.IO.Path.GetExtension(item.Title.Text), video.Url);
                            videoInfos.Add((url: video.Url, title: title));
                        }
                    }
                }
            }

            //ダウンロードキャンセル用トークンを生成
            _tokenSource = new CancellationTokenSource();
            var cancelToken = _tokenSource.Token;

            void onComplete(string title)
            {
                if (_isDownloadingErrList)
                {
                    foreach (UiVideoInfo item in ErrDownloadList.ListBox.Items)
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
               Settings.Default.MaxParallelDownloadNum, cancelToken: cancelToken, onCompleteItem: onComplete, onErrorItem: onError);

            await ShowDownloadedDialog(task, cancelToken);

            if (Settings.Default.IsRemoveCompletedItem)
            {
                targetDownloadList.ForEach(x => DownloadList.ListBox.Items.Remove(x));
            }

            _isBusy = false;
        }

        private async Task ShowDownloadedDialog(Task downloadTask, CancellationToken cancelToken = default)
        {
            try
            {
                await downloadTask;
                DialogHost.CloseDialogCommand.Execute(null, null);
                if (cancelToken.IsCancellationRequested)
                    await DialogHost.Show(new MsgBox("キャンセルされました"));
                else
                    await DialogHost.Show(new MsgBox("ダウンロード完了"));
            }
            catch
            {
                DialogHost.CloseDialogCommand.Execute(null, null);
                await DialogHost.Show(new MsgBox("エラーが発生したためダウンロードが完了しませんでした"));
            }

        }

        private async Task<string> GetSaveVideoTitle(string videoTitle, string url)
        {
            switch (Settings.Default.DefaultFileNameType)
            {
                case 0: //動画タイトル
                    return videoTitle;
                case 1: //動画タイトル_日付
                    string title = System.IO.Path.ChangeExtension(videoTitle, null);
                    MessageBox.Show(title);
                    string extention = System.IO.Path.GetExtension(videoTitle);
                    return title + "_" + DateTime.Now.ToString("yyyyMMdd") + extention;
                case 2: //チャンネル名_動画タイトル
                    return (await _youtube.GetVideoInfoAsync(url))?.Author.ChannelTitle + "_" + videoTitle;
                default:
                    return videoTitle;
            }
        }

        private async Task<string> GetSaveVideoTitle(string videoTitle, string extention, string url)
        {
            switch (Settings.Default.DefaultFileNameType)
            {
                case 0: //動画タイトル
                    return videoTitle + extention;
                case 1: //動画タイトル_日付
                    return videoTitle + "_" + DateTime.Now.ToString() + extention;
                case 2: //チャンネル名_動画タイトル
                    return (await _youtube.GetVideoInfoAsync(url))?.Author.ChannelTitle + "_" + videoTitle + extention;
                default:
                    return videoTitle + extention;
            }
        }
    }
}