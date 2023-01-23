using System.Windows.Controls;
using System.Windows.Media;

namespace YoutubeArchive
{
    /// <summary>
    /// VideoInfo.xaml の相互作用ロジック
    /// </summary>
    public partial class UiVideoInfo : UserControl
    {
        public ImageSource ImgSource
        {
            get
            {
                return Thumbnail.Source;
            }
            set
            {
                Thumbnail.Source = value;
            }
        }

        public UiVideoInfo()
        {
            InitializeComponent();
        }        
    }
}
