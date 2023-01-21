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
using System.Text.RegularExpressions;

namespace YoutubeChannelArchive
{
    /// <summary>
    /// SettingPage.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingPage : Page
    {
        public static SettingPage? instance { get; private set; } = null;
        //public enum defaultFileNameTypes { title, movieTitleAndDate, channelTitleAndMovieTitle }
        public string[] defaultFileNameTypes = {"動画タイトル", "動画タイトル_日付", "チャンネル名_動画タイトル"};

        public SettingPage()
        {
            InitializeComponent();
            instance ??= this;
            DefaultFileNameTypeComboBox.ItemsSource = defaultFileNameTypes;
        }

        public static SettingPage GetInstance()
        {
            return instance ?? new SettingPage();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            grid1.Focus();
        }

        public void OnLostFocus(object sender, RoutedEventArgs e)
        {
            int num;
            int.TryParse(MaxParallelDownloadTextBox.Text, out num);
            if (num < 1)
            {
                num = 1;
            }

            MaxParallelDownloadTextBox.Text = num.ToString();
        }

        private void textBoxPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !new Regex("[0-9]").IsMatch(e.Text);
        }
        private void textBoxPrice_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // 貼り付けを許可しない
            if (e.Command == ApplicationCommands.Paste)
            {
                e.Handled = true;
            }
        }

        private void MainPage_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(MainPage.GetInstance());
        }
    }
}
