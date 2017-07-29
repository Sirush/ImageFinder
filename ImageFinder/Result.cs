using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageFinder
{
	public class Result
	{
		public string File1 { get;  }
		public string File2 { get;  }
		public int Similarity { get;  }

		public Result(string f1, string f2, int s)
		{
			File1 = f1;
			File2 = f2;
			Similarity = s;
		}
	}
}
