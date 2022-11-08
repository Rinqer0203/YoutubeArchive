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

namespace YoutubeChannelArchive
{
    public partial class MainWindow : Window
    {
        private YoutubeClient? _youtube = null;

        public MainWindow()
        {
            InitializeComponent();
            SetYoutubeClient();
        }

        public void SetYoutubeClient()
        {
            _youtube = new YoutubeClient();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CheckFuncTest();
        }

        private async void CheckFuncTest()
        {
            await DialogHost.Show(new MsgBox("ボタンがクリックされました"));
            //テスト
            string videoURL = "https://www.youtube.com/watch?v=umK9xiCXcvs";  //動画
            //string videoURL = "https://www.youtube.com/watch?v=WhWc3b3KhnY";  //動画2
            //string videoURL = "https://www.youtube.com/playlist?list=PL1AnGLbywPJPB-1s_WrZT_pVgSSK_58Bm";    //非公開プレイリスト
            //string videoURL = "https://www.youtube.com/watch?v=j8QnzBGCTyU&list=PL1AnGLbywPJPB-1s_WrZT_pVgSSK_58Bm&index=1";    //非公開プレイリスト(動画選択)
            //string videoURL = "https://www.youtube.com/playlist?list=PLpm4E1LO_i2-z2nxlIaU55HPpBLTNjg1d";    //公開プレイリスト
            //string videoURL = "https://www.youtube.com/watch?v=Jt4ATYElevA&list=PLpm4E1LO_i2-z2nxlIaU55HPpBLTNjg1d";    //公開プレイリスト（動画選択）
            //string videoURL = "https://www.youtube.com/channel/UCSMOQeBJ2RAnuFungnQOxLg";    //チャンネル

            string savePath = @"C:\Users\Tomoki\Downloads\movies\";

            Video? videoInfo = await GetVideoInfoSync(videoURL);
            Playlist? playlist = await GetPlayListInfoSync(videoURL);
            Channel? channelInfo = await GetChannelInfoAsync(videoInfo == null ? "tekito" : videoInfo.Author.ChannelUrl);

            //youtubeチャンネルかどうかの処理も後に実装する
            if (videoInfo == null)
            {
                if (playlist == null)
                {
                    //URLが対象外
                    //MessageBox.Show("URLが対象外");

                }
                else
                {
                    if (playlist != null)
                    {
                        var videoList = await GetPlayListVideosSync(videoURL);

                        if (videoList != null)
                        {
                            //MessageBox.Show("プレイリストダウンロード");
                            var taskList = new List<Task>();
                            for (int i = 0; i < videoList.Count; i++)
                            {
                                taskList.Add(DownloadVideo(videoList[i].Url, savePath + $"{GetSafeTitle(videoList[i].Title)}.mp4"));
                            }

                            await Task.WhenAll(taskList);
                            //MessageBox.Show("ダウンロード完了");
                        }
                    }
                }
            }
            else
            {
                if (playlist == null)
                {
                    await DownloadVideo(videoURL, savePath + $"{GetSafeTitle(videoInfo.Title)}.mp4");
                    //MessageBox.Show("動画のダウンロードが完了いました。");
                    await DialogHost.Show(new MsgBox("動画のダウンロードが完了いました。"));
                }
                else
                {
                    //playlistの動画をすべてダウンロード or 動画だけダウンロード
                    //MessageBox.Show("playlistの動画をすべてダウンロード or 動画だけダウンロード");
                }

            }

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

        private string GetSafeTitle(string title)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars(); //ファイル名に使用できない文字
            return string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));  //使用できない文字を'_'に置換
        }

        private async Task<Playlist?> GetPlayListInfoSync(string url)
        {
            Playlist? playlist = null;

            if (_youtube != null)
            {
                try
                {
                    playlist = await _youtube.Playlists.GetAsync(url);
                }
                catch (PlaylistUnavailableException)
                {
                    //MessageBox.Show("プレイリストが非公開のため情報を取得できません。");
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("playlist is exception\n" + ex.Message);
                }
            }
            return playlist;
        }

        private async Task<IReadOnlyList<PlaylistVideo>?> GetPlayListVideosSync(string url)
        {
            IReadOnlyList<PlaylistVideo>? videos = null;

            if (_youtube != null)
            {
                try
                {
                    videos = await _youtube.Playlists.GetVideosAsync(url);
                }
                catch (PlaylistUnavailableException)
                {
                    //MessageBox.Show("プレイリストが非公開のため情報を取得できません。");
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("video is exception\n" + ex.Message);
                }
            }

            return videos;
        }

        private async Task<Video?> GetVideoInfoSync(string url)
        {
            Video? videoInfo = null;
            if (_youtube != null)
            {
                try
                {
                    videoInfo = await _youtube.Videos.GetAsync(url);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("videoInfo is exception\n" + ex.Message);
                }
            }
            return videoInfo;
        }

        private async Task<Channel?> GetChannelInfoAsync(string url)
        {
            Channel? channelInfo = null;
            if (_youtube != null)
            {
                try
                {
                    channelInfo = await _youtube.Channels.GetAsync(url);
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("channelInfo is exception\n" + ex.Message);
                }
            }
            return channelInfo;
        }

        private async Task DownloadVideo(string url, string savePath)
        {
            if (_youtube != null)
            {
                try
                {
                    await _youtube.Videos.DownloadAsync(url, savePath);
                    Debug.Print($"{savePath} 完了");
                }
                catch (Exception ex)
                {
                    //MessageBox.Show(ex.Message);
                }
            }
        }

        private async Task DownloadOnlyAudio(string url, string savePath)
        {
            if (_youtube != null)
            {
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePath + $"only_audio.{audioStreamInfo.Container}");
            }
        }

        private async Task DownloadOnlyVideo(string url, string savePath)
        {
            if (_youtube != null)
            {
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var movieStreamInfo = streamManifest
                    .GetVideoOnlyStreams()
                    .Where(s => s.Container == Container.Mp4)
                    .GetWithHighestVideoQuality();

                await _youtube.Videos.Streams.DownloadAsync(movieStreamInfo, savePath + $"only_video.{movieStreamInfo.Container}");
            }
        }
    }
}