using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.CodeDom;
using System.Windows.Media.TextFormatting;

namespace YoutubeChannelArchive
{
    internal class YoutubeFunc
    {
        internal YoutubeClient? _youtube { get; private set; } = null;
        private const int _downloadCheckSpanMs = 20;

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
            if (_youtube == null) return null;

            Playlist? playlist = null;

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
                //MessageBox.Show("playlist is exception\n" + ex.Message);
            }

            return playlist;
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
                    //MessageBox.Show("videoInfo is exception\n" + ex.Message);
                }
            }
            return videoInfo;
        }

        internal async Task<Channel?> GetChannelInfoAsync(string url)
        {
            if (_youtube == null) return null;

            try
            {
                return await _youtube.Channels.GetAsync(url);
            }
            catch
            {
                try
                {
                    return await _youtube.Channels.GetByUserAsync(url);
                }
                catch
                {
                    try
                    {
                        return await _youtube.Channels.GetBySlugAsync(url);
                    }
                    catch
                    {
                        try
                        {
                            return await _youtube.Channels.GetByHandleAsync(url);
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
            }
        }

        internal async Task<IReadOnlyList<PlaylistVideo>?> GetPlayListVideosAsync(string url)
        {
            if (_youtube == null) return null;

            IReadOnlyList<PlaylistVideo>? videos = null;

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
                //MessageBox.Show("video is exception\n" + ex.Message);
            }

            return videos;
        }

        internal async Task<IReadOnlyList<PlaylistVideo>?> GetChannelVideosAsync(string url)
        {
            if (_youtube == null) return null;

            try
            {
                return await _youtube.Channels.GetUploadsAsync(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"GetChannelVideosAsync\n{ex.Message}");
                return null;
            }
        }

        //ダウンロード系関数-------------------------------------------------
        internal async Task DownloadVideoAsync(List<(string url, string title)> videoInfos, string saveFolderPath,
            Action<(double progress, int completeCnt, int errCnt)> progressCallback, int maxParallelDownloadCnt, Action<(string url, string title)>? onErrorItem,
            CancellationToken cancelToken = default)
        {
            if (_youtube == null) return;

            try
            {
                var taskList = new List<Task>();
                var taskProgressList = new List<double>();
                int completeCnt = 0, errCnt = 0;

                for (int i = 0; i < videoInfos.Count; i++)
                {
                    taskProgressList.Add(0);
                    //一度ローカル変数に入れないとラムダ式が実行されるときのiの値が使われてしまう(値型なのに...)
                    //参考サイト：https://qiita.com/hiki_neet_p/items/8efc80739657b52922c7
                    int ii = i;

                    void onError()
                    {
                        onErrorItem?.Invoke((videoInfos[ii].url, videoInfos[ii].title));
                        errCnt++;
                    }

                    Task task;
                    if (System.IO.Path.GetExtension(videoInfos[i].title) == ".mp4")
                    {
                        task = DownloadVideoAsync(videoInfos[i].url, @$"{saveFolderPath}\{GetSafeTitle(videoInfos[i].title)}",
                            progressCallback: (x) => taskProgressList[ii] = x, onComplete: () => completeCnt++, onError: onError,
                            cancelToken: cancelToken);
                    }
                    else
                    {
                        task = DownloadAudioAsync(videoInfos[i].url, @$"{saveFolderPath}\{GetSafeTitle(videoInfos[i].title)}",
                            progressCallback: (x) => taskProgressList[ii] = x, onComplete: () => completeCnt++, onError: onError,
                            cancelToken: cancelToken);
                    }
                    taskList.Add(task);

                    progressCallback((GetDictionaryProgress(taskProgressList, videoInfos.Count), completeCnt, errCnt));
                    //並列ダウンロード数に達したらタスクリスト追加を停止し、一定時間処理をスリープ
                    while (taskProgressList.Count(x => 1 > Math.Round(x, 1)) >= maxParallelDownloadCnt && !cancelToken.IsCancellationRequested)
                    {
                        await Task.Delay(_downloadCheckSpanMs);
                        progressCallback((GetDictionaryProgress(taskProgressList, videoInfos.Count), completeCnt, errCnt));
                    }
                }

                while (taskList.Count(x => x.IsCompleted) < taskList.Count)
                {
                    //proglessCallBack((double)cnt / tasks.Count);
                    progressCallback((GetDictionaryProgress(taskProgressList, videoInfos.Count), completeCnt, errCnt));
                    await Task.Delay(_downloadCheckSpanMs);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"DownloadVideoAsync\n{ex.Message}");
                //エラーログファイルを出力
                PrintAddLog("errorLog.txt", ex.Message);
                throw;
            }
        }

        internal async Task DownloadVideoAsync(string url, string savePath, Action<double>? progressCallback = null,
            Action? onComplete = null, Action? onError = null, CancellationToken cancelToken = default)
        {
            if (_youtube == null) return;

            try
            {
                if (progressCallback == null)
                {
                    await _youtube.Videos.DownloadAsync(url, savePath, cancellationToken: cancelToken);
                }
                else
                {
                    var progressHandler = new Progress<double>(progressCallback);
                    await _youtube.Videos.DownloadAsync(url, savePath, progressHandler, cancellationToken: cancelToken);
                }

                if (onComplete != null)
                    onComplete();
            }
            catch
            {
                //if (!cancelToken.IsCancellationRequested)
                    onError?.Invoke();
            }
        }

        internal async Task DownloadAudioAsync(string url, string savePath, Action<double>? progressCallback = null,
            Action? onComplete = null, Action? onError = null, CancellationToken cancelToken = default)
        {
            if (_youtube == null) return;

            try
            {
                if (progressCallback == null)
                {
                    var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                    await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePath, cancellationToken: cancelToken);
                }
                else
                {
                    var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                    var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                    var progressHandler = new Progress<double>(progressCallback);
                    await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePath, progressHandler, cancellationToken: cancelToken);
                }

                onComplete?.Invoke();
            }
            catch
            {
                //if (!cancelToken.IsCancellationRequested)
                    onError?.Invoke();
            }
        }


        //プレイリストとチャンネルの動画ダウンロード関数はいらないかも

        private List<Task> GetVieosTask(IReadOnlyList<PlaylistVideo> videoList, string saveFolderPath)
        {
            var taskList = new List<Task>();
            for (int i = 0; i < videoList.Count; i++)
            {
                taskList.Add(DownloadVideoAsync(videoList[i].Url, @$"{saveFolderPath}\{GetSafeTitle(videoList[i].Title)}.mp4"));
            }
            return taskList;
        }

        internal int GetCompleteTasksCnt(List<Task> tasks)
        {
            int cnt = 0;
            foreach (var task in tasks)
            {
                if (task.IsCompleted)
                    cnt++;
            }
            return cnt;
        }

        internal double GetDictionaryProgress(List<double> taskProgressList, int maxCnt)
        {
            return taskProgressList.Sum() / maxCnt;
        }

        private void PrintAddLog(string fileName, string message)
        {
            using (StreamWriter sw = new StreamWriter(@$"{System.IO.Directory.GetCurrentDirectory()}\{fileName}", true, Encoding.UTF8))
            {
                sw.WriteLine(message);
            }
        }
    }
}