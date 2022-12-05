﻿using System;
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
