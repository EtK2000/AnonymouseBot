using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Main
{
	public partial class MapPreview : Window
	{
		public class Map
		{
			public List<Point> treasures = new List<Point>();
			public List<List<Point>> starts = new List<List<Point>>();
		}

		private static MapPreview instance;
		private static List<BitmapImage> imgs = new List<BitmapImage>();

		public MapPreview(int rows, int cols, Map m)
		{
			if (instance != null)
				instance.Close();
			instance = this;

			InitializeComponent();
			Height = rows * 25;
			Width = cols * 25;

			Left = System.Windows.SystemParameters.PrimaryScreenWidth - Width;
			Top = (System.Windows.SystemParameters.PrimaryScreenHeight - Height) / 2;

			// setup the grid
			grid.ShowGridLines = true;

			grid.Height = (4 / 5f) * Height;
			grid.Width = (4 / 5f) * Width;

			for (int i = 0; i < rows; i++)
				grid.RowDefinitions.Add(new RowDefinition());
			for (int i = 0; i < cols; i++)
				grid.ColumnDefinitions.Add(new ColumnDefinition());

			foreach (Point p in m.treasures)
			{
				Image img = new Image();
				img.Source = new BitmapImage(new Uri("pack://application:,,,/coins.png"));
				Grid.SetRow(img, (int)p.Y);
				Grid.SetColumn(img, (int)p.X);
				grid.Children.Add(img);
			}

			for (int i = 0; i < m.starts.Count; i++)
			{
				foreach (Point p in m.starts[i])
				{
					Image img = new Image();
					if (imgs.Count > i)
						img.Source = imgs[i];
					else
					{
						for (int j = imgs.Count; j < (i + 1); j++)
						{
							try
							{
								imgs.Add(new BitmapImage(new Uri("pack://application:,,,/" + (j + 1) + ".png")));
							}
							catch (Exception)
							{
								imgs.Add(new BitmapImage(new Uri("pack://application:,,,/2.png")));
							}
						}
						img.Source = imgs[i];
					}
					Grid.SetRow(img, (int)p.Y);
					Grid.SetColumn(img, (int)p.X);
					grid.Children.Add(img);
				}
			}
		}
	}
}