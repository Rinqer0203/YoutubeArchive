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

namespace YoutubeChannelArchive
{
    /// <summary>
    /// MyUserControl.xaml の相互作用ロジック
    /// </summary>
    public partial class MsgBox : UserControl
    {
        public MsgBox(string message, bool HideCancelButton = false)
        {
            InitializeComponent();
            
            txtMessage.Text = message;

            if (HideCancelButton)
                CancelButton.Visibility = Visibility.Hidden;
            else
                CancelButton.Visibility = Visibility.Collapsed;
        }
    }
}