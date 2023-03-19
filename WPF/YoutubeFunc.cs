using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace YoutubeArchive.WPF
{
    internal class YoutubeFunc
    {
        internal YoutubeClient? _youtube { get; private set; } = null;
        private HttpClient? _httpClient = null;
        private const int _downloadCheckSpanMs = 20;

        internal YoutubeFunc()
        {
            _youtube = new YoutubeClient();
            _httpClient = new HttpClient();
        }

        public string GetSafeTitle(string title)
        {
            List<char> invalidChars = System.IO.Path.GetInvalidFileNameChars().ToList(); //ファイル名に使用できない文字
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
            catch
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
                catch
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
            catch
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
        //ダウンロード
        internal async Task DownloadAsync(List<(string url, string title)> videoInfos, string saveFolderPath,
            Action<(double progress, int completeCnt, int errCnt)> progressCallback, int maxParallelDownloadCnt,
            Action<string>? onCompleteItem = null, Action<(string url, string title)>? onErrorItem = null,
            CancellationToken cancelToken = default)
        {
            if (_youtube == null) return;

            try
            {
                var taskList = new List<Task>();
                var taskProgressList = new List<double>();
                var convertProgressList = new List<double>();
                int completeCnt = 0, errCnt = 0;

                for (int i = 0; i < videoInfos.Count; i++)
                {
                    taskProgressList.Add(0);

                    if (Path.GetExtension(videoInfos[i].title) == ".mp3")
                    {
                        convertProgressList.Add(0);
                    }
                    //一度ローカル変数に入れないとラムダ式が実行されるときのiの値が使われてしまう
                    //参考サイト：https://qiita.com/hiki_neet_p/items/8efc80739657b52922c7
                    int ii = i;
                    int iii = convertProgressList.Count - 1;

                    void onError()
                    {
                        onErrorItem?.Invoke((videoInfos[ii].url, videoInfos[ii].title));
                        errCnt++;
                    }

                    void onComplete()
                    {
                        onCompleteItem?.Invoke(videoInfos[ii].title);
                        completeCnt++;
                    }

                    Task task;
                    task = Path.GetExtension(videoInfos[i].title) switch
                    {
                        ".mp4" => DownloadVideoAsync(videoInfos[i].url, @$"{saveFolderPath}\{GetSafeTitle(videoInfos[i].title)}",
                            progressCallback: x => taskProgressList[ii] = x, onComplete: onComplete, onError: onError,
                            cancelToken: cancelToken),
                        ".mp3" => task = DownloadAudioAsync(videoInfos[i].url, @$"{saveFolderPath}\{GetSafeTitle(videoInfos[i].title)}",
                            downloadProgressCallback: x => taskProgressList[ii] = x, convertProgressCallBack: x => convertProgressList[iii] = x,
                            onComplete: onComplete, onError: onError,
                            cancelToken: cancelToken),
                        _ => DownloadThumbnail(videoInfos[i].url, @$"{saveFolderPath}\{GetSafeTitle(videoInfos[i].title)}",
                            progressCallback: x => taskProgressList[ii] = x, onComplete: onComplete, onError: onError,
                            cancelToken: cancelToken),
                    };
                    taskList.Add(task);

                    progressCallback(((taskProgressList.Sum() + convertProgressList.Sum()) / (videoInfos.Count + convertProgressList.Count), completeCnt, errCnt));
                    //並列ダウンロード数に達したらタスクリスト追加を停止し、一定時間処理をスリープ
                    while (taskProgressList.Count(x => 1 > Math.Round(x, 1)) >= maxParallelDownloadCnt && !cancelToken.IsCancellationRequested)
                    {
                        await Task.Delay(_downloadCheckSpanMs, cancelToken);
                        progressCallback(((taskProgressList.Sum() + convertProgressList.Sum()) / (videoInfos.Count + convertProgressList.Count), completeCnt, errCnt));
                    }
                }

                //すべてのタスクが終了するまで待つ
                while (taskList.Count(x => x.IsCompleted) < taskList.Count)
                {
                    await Task.Delay(_downloadCheckSpanMs);
                    //progressCallback(((taskProgressList.Sum() + convertProgressList.Sum()) / (videoInfos.Count + ), completeCnt, errCnt));
                    progressCallback(((taskProgressList.Sum() + convertProgressList.Sum()) / (videoInfos.Count + convertProgressList.Count), completeCnt, errCnt));
                    Debug.Print("convertProgress" + convertProgressList.Sum().ToString());
                }
            }
            catch
            {
                //MessageBox.Show($"DownloadVideoAsync\n{ex.Message}");
                throw;
            }
        }

        //単体ビデオダウンロード
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
            catch (Exception ex)
            {
                if (Settings.Default.IsShowErrorMessage)
                    MessageBox.Show(ex.Message);
                onError?.Invoke();
            }
        }

        internal async Task DownloadAudioAsync(string url, string savePath, Action<double> downloadProgressCallback,
            Action<double> convertProgressCallBack, Action? onComplete = null, Action? onError = null, CancellationToken cancelToken = default)
        {
            if (_youtube == null) return;

            int GetWebmDuration(string webmPath)
            {
                using var proc = new Process();
                proc.StartInfo.FileName = Environment.CurrentDirectory + @"\ffprobe.exe";
                proc.StartInfo.Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{webmPath}\"";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.CreateNoWindow = true;
                proc.Start();
                proc.WaitForExit();
                string txt = proc.StandardOutput.ReadToEnd();
                return (int)Math.Floor(double.Parse(txt));
            }

            try
            {
                string savePathWebm = Path.ChangeExtension(savePath, ".webm");
                string savePathMp3 = Path.ChangeExtension(savePath, ".mp3");
                var streamManifest = await _youtube.Videos.Streams.GetManifestAsync(url);
                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                var downloadProgressHandler = new Progress<double>(downloadProgressCallback);
                await _youtube.Videos.Streams.DownloadAsync(audioStreamInfo, savePathWebm, downloadProgressHandler, cancellationToken: cancelToken);

                var convertProgressHandler = new Progress<double>(convertProgressCallBack);

                await Task.Run(() =>
                {
                    int duration = GetWebmDuration(savePathWebm);

                    Debug.Print("-------------------------");

                    using var proc = new Process();
                    proc.StartInfo.FileName = Environment.CurrentDirectory + @"\ffmpeg.exe";
                    proc.StartInfo.Arguments = $"-y -i \"{savePathWebm}\" -acodec libmp3lame -aq 4 -progress - \"{savePathMp3}\"";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.Start();
                    StreamReader stream = proc.StandardOutput;
                    bool isFirstMatch = true;
                    while (!stream.EndOfStream)
                    {
                        string? input = stream.ReadLine();
                        if (input == null)
                            continue;

                        if (input.Contains("out_time="))
                        {
                            if (isFirstMatch)
                            {
                                isFirstMatch = false;
                                continue;
                            }

                            // 正規表現オブジェクトを作成
                            Regex regex = new(@"\d{2}");
                            // 文字列から数字を抽出
                            MatchCollection matches = regex.Matches(input);
                            int totalSec = 0;
                            for (int i = 0; i < 3; i++)
                            {
                                totalSec += int.Parse(matches[i].Value) * (int)Math.Pow(60, 2 - i);
                            }
                            convertProgressCallBack((double)totalSec / duration);
                        }
                    }
                    File.Delete(savePathWebm);
                });

                onComplete?.Invoke();
            }
            catch (Exception ex)
            {
                if (Settings.Default.IsShowErrorMessage)
                    MessageBox.Show(ex.Message);
                onError?.Invoke();
            }
        }

        //単体サムネイルダウンロード
        internal async Task DownloadThumbnail(string url, string savePath, Action<double>? progressCallback = null,
            Action? onComplete = null, Action? onError = null, CancellationToken cancelToken = default)
        {
            if (_youtube == null || _httpClient == null) return;

            try
            {
                string? thumbnailUrl = (await GetVideoInfoAsync(url))?.Thumbnails.TryGetWithHighestResolution()?.Url;

                if (thumbnailUrl != null)
                {
                    //画像をダウンロード
                    var response = await _httpClient.GetAsync(thumbnailUrl);
                    //画像を保存
                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var outStream = File.Create(savePath);
                    stream.CopyTo(outStream);
                }

                if (progressCallback != null)
                    progressCallback(1f);

                onComplete?.Invoke();
            }
            catch
            {
                onError?.Invoke();
            }

        }
    }
}
//Todo: progressを2にして1をダウンロード,　1を変換にする
//プログレスに変換をどう考慮させるか
//返還中のプログレステキストの変更