using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace ImageFinder
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public ConcurrentBag<Result> Results;
		public List<FileData> FilesData;
		public List<Result> ResultsObservable;
		private List<string> Files = new List<string>();
		private Action DeleteButtonFunc = null;
		private Action SwapButtonFunc = null;
		private bool UseSift = false;

		public MainWindow()
		{
			InitializeComponent();

			Output.ItemsSource = ResultsObservable;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			ConcurrentBag<FileData> concurrentData = new ConcurrentBag<FileData>();
			Results = new ConcurrentBag<Result>();
			ResultsObservable = new List<Result>();
			FilesData = new List<FileData>();

			UseSift = SiftCheckbox.IsChecked == true;

			List<Task> allTasks = new List<Task>();
			Stopwatch watch = new Stopwatch();
			watch.Start();

			//Files = GetFileList(@"B:\CSharp\Perso\ImageFinder\dataset");
			Files = GetFileList(Folder.Text);

			foreach (var file in Files)
			{
				var task = new Task(() =>
				{
					var f = new FileData();
					var img = new Image<Bgr, byte>(file);

					f.Path = file;
					f.Size = new FileInfo(file).Length;
					img = img.Resize(128, 128, Inter.Nearest);
					f.Histogram = CalculateHistogram(img);
					f.SiftDescriptor = CalculateSIFT(img);

					concurrentData.Add(f);
				});
				allTasks.Add(task);
				task.Start();
			}

			Task.WaitAll(allTasks.ToArray());

			FilesData = concurrentData.ToList();

			Console.WriteLine("first loop took : " + watch.ElapsedMilliseconds + "ms");

			allTasks.Clear();
			for (int i = 0; i < Files.Count - 1; i++)
			{
				for (int j = i + 1; j < Files.Count - 1; j++)
				{
					var a = i;
					var b = j;
					var task = new Task(() => CompareFile(a, b));
					allTasks.Add(task);
					task.Start();
				}
			}
			Task.WaitAll(allTasks.ToArray());
			ResultsObservable = Results.ToList();
			Output.ItemsSource = ResultsObservable;
			Output.Items.SortDescriptions.Clear();
			Output.Items.SortDescriptions.Add(new SortDescription(Output.Columns[2].SortMemberPath, ListSortDirection.Descending));

			watch.Stop();
			Time.Content = "Processed " + Files.Count + " images in " + watch.ElapsedMilliseconds + "ms.";
		}

		void CompareFile(int i, int j)
		{
			float result1 = CompareHistogram(FilesData[i], FilesData[j]);
			float result2 = 0f;
			int resultFinal = 0;
			int threshold = 45;
			if (result1 > 30)
			{
				if (UseSift)
					result2 = CompareSIFT(FilesData[i], FilesData[j]);
				/*Similarity1.Content = "Histogram similarity : " + Math.Round(result1) + "%";
				Similarity2.Content = "SIFT Similarity : " + Math.Round(result2) + "%";
				Similarity3.Content = "Similarity : " + Math.Round(result1 * (1 - Confidence.Value) + result2 * Confidence.Value) +
									  "%";*/
			}
			else
			{
				//Similarity3.Content = "NOT SIMILAR AT ALL";
			}
			if (UseSift)
			{
				resultFinal = (int) (result1 * (.25f) + result2 * .75f);
			}
			else
			{
				resultFinal = (int)result1;
				threshold = 85;
			}
			if (resultFinal >= threshold)
			{
				if (FilesData[i].Size > FilesData[j].Size)
					Results.Add(new Result(FilesData[i].Path, FilesData[j].Path, resultFinal));
				else
					Results.Add(new Result(FilesData[j].Path, FilesData[i].Path, resultFinal));
			}
		}

		List<string> GetFileList(string path)
		{
			List<string> files = new List<string>();

			string extensions = "*.jpg,*.bmp,*.jpeg,*.png";

			var f = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
				.Where(x => extensions.Contains(System.IO.Path.GetExtension(x).ToLower()));
			files = f.ToList();

			return files;
		}

		Histogram CalculateHistogram(Image<Bgr, Byte> img)
		{
			DenseHistogram BlueHistComp = new DenseHistogram(256, new RangeF(0, 255f));
			DenseHistogram RedHistComp = new DenseHistogram(256, new RangeF(0, 255f));
			DenseHistogram GreenHistComp = new DenseHistogram(256, new RangeF(0, 255f));

			Image<Gray, byte> img1Blue = img[0];
			Image<Gray, byte> img1Green = img[1];
			Image<Gray, byte> img1Red = img[2];

			BlueHistComp.Calculate(new Image<Gray, byte>[] {img1Blue}, true, null);
			GreenHistComp.Calculate(new Image<Gray, byte>[] {img1Green}, true, null);
			RedHistComp.Calculate(new Image<Gray, byte>[] {img1Red}, true, null);

			return new Histogram(BlueHistComp, GreenHistComp, RedHistComp);
		}

		float CompareHistogram(FileData img1, FileData img2)
		{
			double blue = CvInvoke.CompareHist(img1.Histogram.Blue, img2.Histogram.Blue, HistogramCompMethod.Bhattacharyya);
			double green = CvInvoke.CompareHist(img1.Histogram.Green, img2.Histogram.Green, HistogramCompMethod.Bhattacharyya);
			double red = CvInvoke.CompareHist(img1.Histogram.Red, img2.Histogram.Red, HistogramCompMethod.Bhattacharyya);

			double similarity = 100 - (((blue + red + green) / 3) * 100f);
			return (float) similarity;
		}

		Mat CalculateSIFT(Image<Bgr, byte> img)
		{
			SIFT sift = new SIFT(400, 3, 0.04d, 10d, 1.6d);
			VectorOfKeyPoint kp1 = new VectorOfKeyPoint();
			Mat desc = new Mat();
			sift.DetectAndCompute(img, null, kp1, desc, false);

			return desc;
		}

		float CompareSIFT(FileData img1, FileData img2)
		{
			BFMatcher bfm = new BFMatcher(DistanceType.L2Sqr, false);
			VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch();
			bfm.Add(img1.SiftDescriptor);
			bfm.KnnMatch(img2.SiftDescriptor, matches, 2, null);

			Matrix<byte> mask = new Matrix<byte>(img2.SiftDescriptor.Rows, 1);
			mask.SetValue(255);

			//Calculate similarity
			Features2DToolbox.VoteForUniqueness(matches, 0.8, mask.Mat);

			int cnt = 0;
			for (int i = 0; i < mask.Rows; i++)
			{
				if (mask.Data[i, 0] != 0)
					cnt++;
			}

			float similarity = cnt * 100f / img2.SiftDescriptor.Rows;
			return similarity;
		}

		private void ImagePanel1_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);

				HandleFileDrop(files, 1);
			}
		}

		private void ImagePanel2_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[]) e.Data.GetData(DataFormats.FileDrop);

				HandleFileDrop(files, 2);
			}
		}

		void HandleFileDrop(string[] files, int panel)
		{
			if (panel == 1)
			{
				Image1.Source = new BitmapImage(new Uri(files[0], UriKind.Absolute));
				ImageSource1.Text = files[0];
			}
			else
			{
				Image2.Source = new BitmapImage(new Uri(@files[0], UriKind.Absolute));
				ImageSource2.Text = files[0];
			}
		}

		string GetMD5(string file)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(file))
				{
					return md5.ComputeHash(stream).ToString();
				}
			}
		}

		private void Output_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			//Get which row was clicked
			DataGridRow row =
				ItemsControl.ContainerFromElement((DataGrid) sender, e.OriginalSource as DependencyObject) as DataGridRow;
			if (row == null)
				return;

			Result res = (Result) row.Item;

			if (!File.Exists(res.File1) || !File.Exists(res.File2))
			{
				ResultsObservable.Remove((Result)row.Item);
				Refresh();
				return;
			}

			ImageSource1.Text = (FilesData.Find(x => x.Path == res.File1).Size / 1000).ToString() + "kb";
			ImageSource2.Text = (FilesData.Find(x => x.Path == res.File2).Size / 1000).ToString() + "kb";

			Image1.Source = new BitmapImage(new Uri(res.File1, UriKind.Absolute));

			using (var stream = File.OpenRead(res.File2))
			{
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.StreamSource = stream;
				bmp.CacheOption = BitmapCacheOption.OnLoad;
				bmp.EndInit();
				Image2.Source = bmp;
			}

			DeleteButtonFunc = () =>
			{
				Image1.Source = null;
				Image2.Source = null;
				FileSystem.DeleteFile(res.File2, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
				ResultsObservable.Remove((Result) row.Item);
				Refresh();
				DeleteButtonFunc = null;
			};

			SwapButtonFunc = () =>
			{
				ResultsObservable.Add(new Result(res.File2, res.File1, res.Similarity));
				ResultsObservable.Remove(res);
				Refresh();
				SwapButtonFunc = null;
			};
		}

		private void DeleteButton_Click(object sender, RoutedEventArgs e)
		{
			DeleteButtonFunc();
		}

		private void SwapFilesButton_Click(object sender, RoutedEventArgs e)
		{
			SwapButtonFunc();
		}

		private void Refresh()
		{
			Output.ItemsSource = ResultsObservable;
			Output.Items.SortDescriptions.Clear();
			Output.Items.SortDescriptions.Add(new SortDescription(Output.Columns[2].SortMemberPath, ListSortDirection.Descending));
		}
	}
}