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
//*/
namespace YoutubeChannelArchive
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Uri uri = new Uri("/MainPage.xaml", UriKind.Relative);
            frame.Source = uri;

            Height = Settings.Default.Window_Height;
            Width = Settings.Default.Window_Width;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //設定値を保存する

            //ウィンドウサイズ
            Settings.Default.Window_Height = Height;
            Settings.Default.Window_Width = Width;

            Settings.Default.Save();
        }
    }
}