using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;

namespace ImageFinder
{
	public struct Histogram
	{
		public DenseHistogram Blue;
		public DenseHistogram Green;
		public DenseHistogram Red;

		public Histogram(DenseHistogram b, DenseHistogram g, DenseHistogram r)
		{
			Blue = b;
			Green = g;
			Red = r;
		}
	}

	public class FileData
	{
		public string Path;
		public string Md5Hash;
		public Histogram Histogram;
		public Mat SiftDescriptor;
		public long Size;
	}
}
