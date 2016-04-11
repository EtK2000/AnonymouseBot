using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Main
{
	public partial class MainWindow : Window
	{
		private static string dir;
		private static List<string> bots;
		private static int players;

		public MainWindow()
		{
			InitializeComponent();

			Left = 0;
			Top = (System.Windows.SystemParameters.PrimaryScreenHeight - Height) / 2;

			// get the working directory
			dir = Directory.GetCurrentDirectory();
			dir = dir.Substring(0, dir.Length - 26);

			// index the found bots
			bots = new List<string>();
		}

		// run the bots according to the given info
		private void run(object sender, RoutedEventArgs e)
		{
			// save the current config
			StreamWriter sw = new StreamWriter("config.cfg");
			sw.WriteLine("first={0}", first.SelectedValue);
			sw.WriteLine("second={0}", second.SelectedValue);
			sw.WriteLine("third={0}", third.SelectedValue);
			sw.WriteLine("fourth={0}", fourth.SelectedValue);
			sw.WriteLine("map={0}", map.SelectedValue);
			sw.Close();

			// clear the logs from the last simulation
			if (Directory.Exists(dir + @"lib\game_logs"))
				Directory.Delete(dir + @"lib\game_logs", true);

			// run the simulation
			Process proc = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = "/c C:\\python27\\python.exe \"lib\\playgame.py\" --nolaunch --loadtime 10000 -e -E -d -O --debug_in_replay --log_dir lib\\game_logs --html=replay.html --map_file \"maps\\" + map.SelectedItem + "\" \"bots\\" + first.SelectedItem + "\" \"bots\\" + second.SelectedItem + "\" ",
					UseShellExecute = false,
					RedirectStandardOutput = false,
					CreateNoWindow = false,
					WorkingDirectory = dir.Substring(0, dir.Length - 1)
				}
			};

			if (players > 2)
				proc.StartInfo.Arguments += "\"bots\\" + third.SelectedItem + "\" ";
			if (players > 3)
				proc.StartInfo.Arguments += "\"bots\\" + fourth.SelectedItem + "\" ";

			proc.StartInfo.Arguments += " && echo. && pause";
			proc.Start();
			proc.WaitForExit();

			// if it finished let the user watch the replay if they want
			if (File.Exists(dir + @"lib\game_logs\replay.html"))
			{
				if (MessageBox.Show("Show replay?", "Replay", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
					Process.Start(dir + @"lib\game_logs\replay.html");
			}
			else
				MessageBox.Show("Simulation canceled...", "Canceled", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// set maps
			map.Items.Clear();
			foreach (string s in filesIn(dir + "maps"))
			{
				if (s.EndsWith(".map"))
					map.Items.Add(s.Substring(dir.Length + 5));
			}
			map.SelectedIndex = 0;

			// cache all found bots
			bots.Clear();
			foreach (string s in filesIn(dir + "bots"))
			{
				if (s.EndsWith(".java") || s.EndsWith(".py") || (s.EndsWith(".cs") && !s.EndsWith(".Designer.cs") && !s.EndsWith(".g.cs") && !s.EndsWith(".i.cs") && !s.EndsWith(".xaml.cs") && !s.EndsWith("AssemblyInfo.cs")))
					bots.Add(s.Substring(dir.Length + 5));
			}

			// set bots
			ComboBox[] bot = new ComboBox[] { first, second, third, fourth };
			foreach (ComboBox b in bot)
				b.Items.Clear();

			foreach (string s in bots)
			{
				foreach (ComboBox b in bot)
					b.Items.Add(s);
			}

			foreach (ComboBox b in bot)
				b.SelectedIndex = 0;

			// read from the config if it exists
			if (File.Exists("config.cfg"))
			{
				string line;
				StreamReader sr = new StreamReader("config.cfg");
				while (!sr.EndOfStream)
				{
					line = sr.ReadLine();
					switch (line.Substring(0, line.IndexOf('=')))
					{
						case "first":
							first.SelectedValue = line.Substring(line.IndexOf('=') + 1);
							break;
						case "fourth":
							fourth.SelectedValue = line.Substring(line.IndexOf('=') + 1);
							break;
						case "map":
							map.SelectedValue = line.Substring(line.IndexOf('=') + 1);
							map_SelectionChanged(null, null);// load the selected map
							break;
						case "second":
							second.SelectedValue = line.Substring(line.IndexOf('=') + 1);
							break;
						case "third":
							third.SelectedValue = line.Substring(line.IndexOf('=') + 1);
							break;
					}
				}
				sr.Close();
			}
		}

		// get the number of players required for the selected map
		private void map_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (map.SelectedIndex >= 0)
			{
				MapPreview.Map m = new MapPreview.Map();
				int y = 0;
				int r = 0, c = 0;
				string line;
				StreamReader sr = new StreamReader(dir + "maps\\" + map.SelectedValue);
				while ((line = sr.ReadLine()) != null)
				{
					if (line.StartsWith("cols "))
						c = int.Parse(line.Substring(5));
					else if (line.StartsWith("rows "))
						r = int.Parse(line.Substring(5));
					else if (line.StartsWith("players "))
					{
						players = int.Parse(line.Substring(8));
						for (int i = 0; i < players; i++)
							m.starts.Add(new List<Point>(players));
					}
					else if (line.StartsWith("m "))
					{
						string l = line.Substring(2);
						for (int i = 0; i < l.Length; i++)
						{
							if (l[i] == '$')
								m.treasures.Add(new Point(i, y));
							else if (l[i] >= 'a' && l[i] <= 'z')
								m.starts[l[i] - 'a'].Add(new Point(i, y));
						}
						y++;
					}
				}
				sr.Close();

				MapPreview mp = new MapPreview(r, c, m);
				mp.Show();
				mp.Owner = this;
			}

			Activate();
		}

		// recursively get all the files in the given directory
		private static string[] filesIn(string directory)
		{
			List<string> res = new List<string>(Directory.GetFiles(directory));
			foreach (string dir in Directory.GetDirectories(directory))
				res.AddRange(filesIn(dir));
			return res.ToArray();
		}
	}
}
