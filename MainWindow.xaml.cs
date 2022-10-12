using System;
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
using YoutubeExplode.Common;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;
using System.Media;

//YouTube Channel Archives という名前で作り直す

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
            //テスト
            string videoURL = "https://www.youtube.com/watch?v=umK9xiCXcvs";
            string savePath = @"C:\Users\Tomoki\Downloads\movies\";

            //動画の情報を取得
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
                VideoDownload(videoURL, savePath);
                OnlyAudioDownload(videoURL, savePath);
                OnlyVideoDownload(videoURL, savePath);
                SystemSounds.Asterisk.Play();
                MessageBox.Show("ダウンロード終了");

            }
        }

        private async Task<Playlist?> GetPlayListInfo(string url)
        {
            Playlist? playlist = null;
            if (_youtube != null)
            {
                playlist = await _youtube.Playlists.GetAsync(url);
            }
            return playlist;
        }

        private async Task<IReadOnlyList<PlaylistVideo>?> GetPlayListVideos(string url)
        {
            IReadOnlyList<PlaylistVideo>? videos = null;
            if (_youtube != null)
            {
                videos = await _youtube.Playlists.GetVideosAsync(url);
            }
            return videos;
        }

        private async Task<Video?> GetVideoInfo(string url)
        {
            Video? videoInfo = null;
            if (_youtube != null)
            {
                videoInfo = await _youtube.Videos.GetAsync(url);
            }
            return videoInfo;
        }

        private async void VideoDownload(string url, string savePath)
        {
            if (_youtube != null)
            {
                await _youtube.Videos.DownloadAsync(url, savePath + "video.mp4");
            }
        }

        private async void OnlyAudioDownload(string url, string savePath)
        {
            if (_youtube != null)
            {
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePath + $"only_audio.{audioStreamInfo.Container}");
            }
        }

        private async void OnlyVideoDownload(string url, string savePath)
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