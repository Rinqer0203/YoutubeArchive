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
using System.Diagnostics;
using MaterialDesignThemes.Wpf;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Reflection.Metadata;

namespace YoutubeChannelArchive
{
    public partial class MainWindow : Window
    {
        private YoutubeFunc _youtube = new YoutubeFunc();
        private enum addListType { video, playlist, channel };

        public MainWindow()
        {
            InitializeComponent();

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

            //ダークテーマ
            /*
            PaletteHelper paletteHelper = new PaletteHelper();
            Theme theme = (Theme)paletteHelper.GetTheme();
            theme.SetBaseTheme(Theme.Dark);
            paletteHelper.SetTheme(theme);
            //*/
        }

        //進捗バーの被コールバック関数
        private void OnProgressChanged(double progress)
        {
            DownloadProgress.Value = progress;
        }

        //ボタンクリックイベント------------------------------------------------

        private void AddDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            AddDownloadList();
        }

        private async void AllDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var videoInfos = new List<(string url, string title)>();
            foreach (UiVideoInfo item in DownloadListBox.Items)
            {
                if (item.VideoIcon.Visibility == Visibility.Visible)
                {
                    videoInfos.Add((url: item.Url.Text, title: item.TitleText.Text));
                }
                else if (item.PlaylistIcon.Visibility == Visibility.Visible)
                {
                    var videos = await _youtube.GetPlayListVideosAsync(item.Url.Text);
                    if (videos != null)
                    {
                        foreach (var video in videos)
                        {
                            videoInfos.Add((url: video.Url, title: video.Title));
                        }
                    }
                }
                else if (item.ChannelIcon.Visibility == Visibility.Visible)
                {
                    var videos = await _youtube.GetChannelVideosAsync(item.Url.Text);
                    if (videos != null)
                    {
                        foreach(var video in videos)
                        {
                            videoInfos.Add((url: video.Url, title: video.Title));
                        }
                    }
                }
            }

            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                //await DialogHost.Show(new MsgBox("キャンセルされました"));
                return;
            }

            await DownloadedDialog(_youtube.DownloadVideoAsync(videoInfos, savePath, OnProgressChanged));
        }

        private async void SingleDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var videoInfos = new List<(string url, string title)>();
            foreach (UiVideoInfo s in DownloadListBox.SelectedItems)
            {
                videoInfos.Add((url: s.Url.Text, title: s.TitleText.Text));
            }

            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                //await DialogHost.Show(new MsgBox("キャンセルされました"));
                return;
            }

            await DownloadedDialog(_youtube.DownloadVideoAsync(videoInfos, savePath, OnProgressChanged));
        }

        //------------------------------------------------

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

        private async Task DownloadOne(string url)
        {
            Video? vInfo = await _youtube.GetVideoInfoAsync(url);
            if (vInfo == null)
            {
                await DialogHost.Show(new MsgBox("動画データを取得できませんでした"));
                return;
            }

            var savePath = SaveFilePathDialog(GetSafeTitle(vInfo.Title), "mp4");
            if (savePath == null)
            {
                await DialogHost.Show(new MsgBox("キャンセルされました"));
                return;
            }

            await DownloadedDialog(_youtube.DownloadVideoAsync(url, savePath, OnProgressChanged));
        }

        private async Task DownloadPlaylist(string url)
        {
            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                await DialogHost.Show(new MsgBox("キャンセルされました"));
                return;
            }

            await DownloadedDialog(_youtube.DownloadPlaylistVideosAsync(url, savePath, OnProgressChanged));
        }

        private async Task DownloadChannelVideos(string url)
        {
            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                await DialogHost.Show(new MsgBox("キャンセルさました"));
                return;
            }

            await DownloadedDialog(_youtube.DownaloadChannelVideosAsync(url, savePath, OnProgressChanged));
        }

        private async Task DownloadedDialog(Task downloadTask)
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

        private async void AddDownloadList()
        {
            string url = UrlTextBox.Text;
            string errMsgBase = "を取得できませんでした\nURLが不正な可能性があります";

            void AddList(string title, Thumbnail thumbnail, string url, addListType type)
            {
                var uiVideoInfo = new UiVideoInfo();
                uiVideoInfo.text = title;
                uiVideoInfo.ImgSource = new BitmapImage(new Uri(thumbnail.Url));
                uiVideoInfo.Url.Text = url;

                //タイプごとのアイコンの設定
                if (type == addListType.video)
                {
                    uiVideoInfo.VideoIcon.Visibility = Visibility.Visible;
                }
                else if (type == addListType.playlist)
                {
                    uiVideoInfo.PlaylistIcon.Visibility = Visibility.Visible;
                }
                else if (type == addListType.channel)
                {
                    uiVideoInfo.ChannelIcon.Visibility = Visibility.Visible;
                }

                DownloadListBox.Items.Add(uiVideoInfo);
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

        private async void CheckFuncTest()
        {
            //テスト
            string videoUrl = "https://www.youtube.com/watch?v=umK9xiCXcvs";  //動画
            //string videoUrl = "https://www.youtube.com/watch?v=WhWc3b3KhnY";  //動画2
            //string videoUrl = "https://www.youtube.com/watch?v=GwNZSdp7WNk";  //8k動画3
            //string videoUrl = "https://www.youtube.com/playlist?list=PL1AnGLbywPJPB-1s_WrZT_pVgSSK_58Bm";    //非公開プレイリスト
            //string videoUrl = "https://www.youtube.com/watch?v=j8QnzBGCTyU&list=PL1AnGLbywPJPB-1s_WrZT_pVgSSK_58Bm&index=1";    //非公開プレイリスト(動画選択)
            //string videoUrl = "https://www.youtube.com/playlist?list=PLpm4E1LO_i2-z2nxlIaU55HPpBLTNjg1d";    //公開プレイリスト
            //string videoUrl = "https://www.youtube.com/playlist?list=PLTz7YgHsKaJU-wgC47rD4f_2WjS_U-ClO";    //公開プレイリスト(長い)
            //string videoUrl = "https://www.youtube.com/playlist?list=PLSfaMlUCtfeHCCs_88hSSE617roKod2Km";    //公開プレイリスト(短い)
            //string videoUrl = "https://www.youtube.com/watch?v=Jt4ATYElevA&list=PLpm4E1LO_i2-z2nxlIaU55HPpBLTNjg1d";    //公開プレイリスト（動画選択）
            //string videoUrl = "https://www.youtube.com/channel/UCSMOQeBJ2RAnuFungnQOxLg";    //チャンネル
            //string videoUrl = "https://www.youtube.com/channel/UCAZfv8y2eKy-5S9rCgT8y9A/videos";    //チャンネル(小)

            //string savePath = @"C:\Users\Tomoki\Downloads\movies\";
            //var s = SaveFilePathDialog("test", "mp4");
            //await GetUrlActionType(videoUrl);

            switch (UrlActionComboBox.Text)
            {
                case "単体ダウンロード":
                    await DownloadOne(videoUrl);
                    break;
                case "プレイリストダウンロード":
                    await DownloadPlaylist(videoUrl);
                    break;
                case "チャンネルダウンロード":
                    await DownloadChannelVideos(videoUrl);
                    break;
                case "チャンネルアーカイブ":
                    //後で実装
                    break;
            }



            //----------------------------------------------------------------------------------

            /*
            Video? videoInfo = await _youtube.GetVideoInfoAsync(videoUrl);
            Playlist? playlist = await _youtube.GetPlayListInfoAsync(videoUrl);
            Channel? channelInfo = await _youtube.GetChannelInfoAsync(videoUrl);
            Channel? videoChannelInfo = null;

            if (videoInfo != null)
            {
                videoChannelInfo = await _youtube.GetChannelInfoAsync(videoInfo.Author.ChannelUrl);
            }

            //youtubeチャンネルかどうかの処理も後に実装する
            if (videoInfo == null)
            {
                if (playlist == null)
                {
                    //URLが対象外
                    await DialogHost.Show(new MsgBox("URLが対象外"));
                }
                else
                {
                    if (playlist != null)
                    {
                        var videoList = await _youtube.GetPlayListVideosAsync(videoUrl);

                        if (videoList != null)
                        {
                            await DialogHost.Show(new MsgBox("プレイリストダウンロード"));
                            var taskList = new List<Task>();
                            for (int i = 0; i < videoList.Count; i++)
                            {
                                taskList.Add(_youtube.DownloadVideoAsync(videoList[i].Url, savePath + $"{GetSafeTitle(videoList[i].Title)}.mp4", OnProgressChanged));
                            }

                            await Task.WhenAll(taskList);
                            await DialogHost.Show(new MsgBox("ダウンロード完了"));
                        }
                    }
                }
            }
            else
            {
                if (playlist == null)
                {
                    await _youtube.DownloadVideoAsync(videoUrl, savePath + $"{GetSafeTitle(videoInfo.Title)}.mp4", OnProgressChanged);
                    await DialogHost.Show(new MsgBox("動画のダウンロードが完了いました。"));
                }
                else
                {
                    //playlistの動画をすべてダウンロード or 動画だけダウンロード
                    await DialogHost.Show(new MsgBox("playlistの動画をすべてダウンロード or 動画だけダウンロード"));
                }
            }
            //*/

            //動画の情報を取得
            /*
            Video? videoInfo = await GetVideoInfo(videoURL);
            Playlist? playlist = await GetPlayListInfo(videoURL);
            var playlistVideos = await GetPlayListVideos(videoURL);

            MessageBox.Show($"videoInfo:{(videoInfo == null ? "null" : "not null")}\n" +
                $"  playlist:{(playlist == null ? "null" : "not null")}\n" +
                $" playlistVideos:{(playlistVideos == null ? "null" : "not null")}");

            if (videoInfo != null && false)
            {
                MessageBox.Show($"タイトル：{videoInfo.Title}\n チャンネル名：{videoInfo.Author}\n 動画時間：{videoInfo.Duration}");
                if (playlist != null)
                {
                    MessageBox.Show($"プレイリスト情報\n タイトル：{playlist.Title} チャンネル名：{playlist.Author.ChannelTitle}");
                    var list = await GetPlayListVideos(videoURL);
                    string titles = "";
                    foreach (var s in list)
                    {
                        titles += s.Title;
                    }
                    MessageBox.Show($"タイトル一覧：{titles}");
                }
                DownloadVideo(videoURL, savePath);
                DownloadOnlyAudio(videoURL, savePath);
                DownloadOnlyVideo(videoURL, savePath);
                SystemSounds.Asterisk.Play();
                MessageBox.Show("ダウンロード終了");
            }
            //*/
        }

        private string GetSafeSavepath(string savePath, string title, string extension)
        {
            return @$"{savePath}\{GetSafeTitle(Title)}.{extension}";
        }

        public string GetSafeTitle(string title)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars(); //ファイル名に使用できない文字
            return string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));  //使用できない文字を'_'に置換
        }
    }
}