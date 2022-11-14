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

namespace YoutubeChannelArchive
{
    internal class YoutubeFunc
    {
        internal YoutubeClient? _youtube { get; private set; } = null;

        internal YoutubeFunc()
        {
            _youtube = new YoutubeClient();
        }

        public string GetSafeTitle(string title)
        {
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars(); //ファイル名に使用できない文字
            return string.Concat(title.Select(c => invalidChars.Contains(c) ? '_' : c));  //使用できない文字を'_'に置換
        }

        //メタデータ取得系関数----------------------------------------------------

        internal async Task<Playlist?> GetPlayListInfoAsync(string url)
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
                    await DialogHost.Show(new MsgBox("プレイリストが非公開のため情報を取得できません。"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("playlist is exception\n" + ex.Message);
                }
            }
            return playlist;
        }

        internal async Task<IReadOnlyList<PlaylistVideo>?> GetPlayListVideosAsync(string url)
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
                    await DialogHost.Show(new MsgBox("プレイリストが非公開のため情報を取得できません。"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("video is exception\n" + ex.Message);
                }
            }

            return videos;
        }

        internal async Task<Video?> GetVideoInfoAsync(string url)
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
                    MessageBox.Show("videoInfo is exception\n" + ex.Message);

                }
            }
            return videoInfo;
        }

        internal async Task<Channel?> GetChannelInfoAsync(string url)
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
                    MessageBox.Show("channelInfo is exception\n" + ex.Message);
                }
            }
            return channelInfo;
        }

        //ダウンロード系関数-------------------------------------------------

        internal async Task DownloadVideoAsync(string url, string savePath, Action<double>? progressCallback = null)
        {
            if (_youtube == null) return;

            try
            {
                await _youtube.Videos.DownloadAsync(url, savePath);

                if (progressCallback == null)
                {
                    await _youtube.Videos.DownloadAsync(url, savePath);
                }
                else
                {
                    var progressHandler = new Progress<double>(progressCallback);
                    await _youtube.Videos.DownloadAsync(url, savePath, progressHandler);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DownloadVideoAsync\n{ex.Message}");
                //エラーログファイルを出力
                using (StreamWriter sw = new StreamWriter(@$"{System.IO.Directory.GetCurrentDirectory()}\errorLog.txt", true, Encoding.UTF8))
                {
                    sw.WriteLine(ex.Message);
                }
                //失敗した際の処理も書く
            }
        }

        internal async Task DownloadPlaylistVideosAsync(string url, string saveFolderPath, Action<double> progressCallBack)
        {
            if (_youtube == null) return;

            try
            {
                var videoList = await GetPlayListVideosAsync(url);

                if (videoList != null)
                {
                    //フォルダ選択はMainWindowに託す
                    /*
                    using (var dialog = new CommonOpenFileDialog()
                    {
                        Title = "フォルダを選択してください",
                        IsFolderPicker = true,
                    })
                    {
                        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                        {
                            await DialogHost.Show(new MsgBox("キャンセルされました"));
                            return;
                        }
                    //*/

                    await DialogHost.Show(new MsgBox($"{saveFolderPath}に保存します"));

                    var taskList = new List<Task>();
                    for (int i = 0; i < videoList.Count; i++)
                    {
                        taskList.Add(DownloadVideoAsync(videoList[i].Url, @$"{saveFolderPath}\{GetSafeTitle(videoList[i].Title)}.mp4"));
                    }

                    //await Task.WhenAll(taskList);
                    await WaitAllTask(taskList, progressCallBack);
                    await DialogHost.Show(new MsgBox("ダウンロード完了"));
                    //*/
                }


                else
                {
                    await DialogHost.Show(new MsgBox("プレイリストに動画が含まれていません"));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        internal async Task WaitAllTask(List<Task> tasks, Action<double> proglessCallBack)
        {
            int cnt = 0;
            while (cnt < tasks.Count)
            {
                cnt = 0;
                foreach (var s in tasks)
                {
                    if (s.IsCompleted)
                        cnt++;
                }
                proglessCallBack((double)cnt / tasks.Count);
                await Task.Delay(10);
            }
        }

        internal async Task DownloadOnlyAudioAsync(string url, string savePath)
        {
            if (_youtube != null)
            {
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePath + $"sound_only.{audioStreamInfo.Container}");
            }
        }

        internal async Task DownloadOnlyVideoAsync(string url, string savePath)
        {
            if (_youtube != null)
            {
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var movieStreamInfo = streamManifest
                    .GetVideoOnlyStreams()
                    .Where(s => s.Container == Container.Mp4)
                    .GetWithHighestVideoQuality();

                await _youtube.Videos.Streams.DownloadAsync(movieStreamInfo, savePath + $"no_sound.{movieStreamInfo.Container}");
            }
        }
    }
}