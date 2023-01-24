using System;
using System.Windows;

//並列ダウンロードテストの結果1:30程度の1080pの動画を60個同時にダウンロードすることができた
/*  tasks
    ダウンロード時にファイル名が重複するときの処理の追加
    ダウンロード速度を表示させる
//*/
namespace YoutubeArchive
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Uri uri = new Uri("WPF/Pages/MainPage.xaml", UriKind.Relative);
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