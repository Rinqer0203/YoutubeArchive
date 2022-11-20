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

        public MainWindow()
        {
            InitializeComponent();
            /*
            PaletteHelper paletteHelper = new PaletteHelper();
            Theme theme = (Theme)paletteHelper.GetTheme();
            theme.SetBaseTheme(Theme.Dark);
            paletteHelper.SetTheme(theme);
            //*/
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            /*/
            CheckFuncTest();
            /*/
            AddDownloadList();
            //*/
        }

        private void OnProgressChanged(double progress)
        {
            DownloadProgress.Value = progress;
        }

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

            await Download(_youtube.DownloadVideoAsync(url, savePath, OnProgressChanged));
        }

        private async Task DownloadPlaylist(string url)
        {
            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                await DialogHost.Show(new MsgBox("キャンセルされました"));
                return;
            }

            await Download(_youtube.DownloadPlaylistVideosAsync(url, savePath, OnProgressChanged));
        }

        private async Task DownloadChannelVideos(string url)
        {
            var savePath = SaveFolderPathDialog();
            if (savePath == null)
            {
                await DialogHost.Show(new MsgBox("キャンセルさました"));
                return;
            }

            await Download(_youtube.DownaloadChannelVideosAsync(url, savePath, OnProgressChanged));
        }

        private async Task Download(Task downloadTask)
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

            async void AddList()
            {
                /*
                StackPanel panel = new StackPanel();
                System.Windows.Controls.Image image = new System.Windows.Controls.Image();
                BitmapImage bmpImage = new BitmapImage();
                UrlTextBox.Text = s.Thumbnails.First().Url;

                panel.Children.Add(image);

                DownloadListBox.Items.Add(s.Thumbnails.Cast<Image>());
                //*/

                var s = await _youtube.GetVideoInfoAsync(url);

                int max = s.Thumbnails.Count();
                MessageBox.Show($"{s.Thumbnails[max - 1].Resolution}\n{s.Thumbnails[0].Resolution}");

                Image image = new Image();
                BitmapImage imageSource = new BitmapImage(new Uri(s.Thumbnails.First().Url));
                image.Source = imageSource;
                image.Width = s.Thumbnails.First().Resolution.Width;
                image.Height = s.Thumbnails.First().Resolution.Height;

                var videoInfo = new VideoInfo();
                videoInfo.Text = s.Title;
                videoInfo.ImgSource = new BitmapImage(new Uri(s.Thumbnails.First().Url));

                /*
                StackPanel stack = new StackPanel();                
                stack.Orientation = Orientation.Horizontal;
                stack.Children.Add(image);
                stack.Children.Add(textBlock);
                //*/

                DownloadListBox.Items.Add(videoInfo);

                //画像のロード完了イベントを処理して、画像のサイズを設定する
                /*
                imageSource.DownloadCompleted += new EventHandler((object sender, EventArgs e) =>
                {
                    image.Width = imageSource.PixelWidth;
                    image.Height = imageSource.PixelHeight;
                });
                //*/
            }

            switch(UrlActionComboBox.Text)
            {
                case "単体ダウンロード":
                    if (await _youtube.GetVideoInfoAsync(url) == null)
                    {
                        await DialogHost.Show(new MsgBox("動画データを取得できませんでした\nURLが不正な可能性があります"));
                    }
                    else
                    {
                        AddList();
                    }
                    break;
                case "プレイリストダウンロード":
                    if (await _youtube.GetPlayListInfoAsync(url) == null)
                    {
                        await DialogHost.Show(new MsgBox("プレイリストデータを取得できませんでした\nURLが不正な可能性があります"));
                    }
                    else
                    {
                        AddList();
                    }
                    break;
                case "チャンネルダウンロード":
                    if (await _youtube.GetChannelInfoAsync(url) == null)
                    {
                        await DialogHost.Show(new MsgBox("チャンネルデータを取得できませんでした\nURLが不正な可能性があります"));
                    }
                    else
                    {
                        AddList();
                    }
                    break;
                case "チャンネルアーカイブ":
                    //後で実装
                    break;
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