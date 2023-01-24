using System.Windows;
using System.Windows.Controls;

namespace YoutubeArchive
{
    public partial class MsgBox : UserControl
    {
        public MsgBox(string message, bool HideCancelButton = false)
        {
            InitializeComponent();

            txtMessage.Text = message;

            if (HideCancelButton)
                CancelButton.Visibility = Visibility.Visible;
            else
                CancelButton.Visibility = Visibility.Collapsed;
        }
    }
}