// Copyright Microsoft Corp. All Rights Reserved.
using System;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;

namespace Microsoft.VisualXpress
{
	class Resource
	{
		private static Dictionary<string, stdole.Picture> m_Pictures = new Dictionary<string, stdole.Picture>();

		public static System.Drawing.Image LoadImageFromAssembly(string name)
		{
			try
			{
				if (String.IsNullOrEmpty(name) == false)
				{
					var assembly = System.Reflection.Assembly.GetExecutingAssembly();
					var stream = assembly.GetManifestResourceStream("Microsoft.VisualXpress.Resources."+name);
					if (stream != null)
						return System.Drawing.Image.FromStream(stream);
				}
			} 
			catch {}
			return null;
		}

		public static stdole.Picture LoadPictureFromAssembly(string name)
		{
			try
			{
				System.Drawing.Image image = LoadImageFromAssembly(name);
				if (image != null)
					return AxHostConverter.ImageToPictureDisp(image) as stdole.Picture;
			}
			catch {}
			return null;
		}

		public static stdole.Picture LoadPictureFromFile(string name)
		{
			try
			{
				if (System.IO.File.Exists(name))
				{
					System.Drawing.Image image = System.Drawing.Bitmap.FromFile(name);
					if (image != null)
						return AxHostConverter.ImageToPictureDisp(image) as stdole.Picture;
				}
			} 
			catch {}
			return null;
		}

		public static stdole.Picture LoadPicture(string name)
		{
			stdole.Picture picture = LoadPictureFromAssembly(name);
			if (picture == null)
				picture = LoadPictureFromFile(name);
			return picture;
		}

		private static stdole.Picture GetPictureResource(string name)
		{
			stdole.Picture picture;
			if (m_Pictures.TryGetValue(name, out picture))
				return picture;
			picture = LoadPictureFromAssembly(name);
			m_Pictures[name] = picture;
			return picture;
		}

		public static stdole.Picture PictureCheck
		{ 
			get { return GetPictureResource("check.png"); } 
		}

		public static stdole.Picture PictureCheckout
		{ 
			get { return GetPictureResource("checkout.png"); } 
		}

		public static stdole.Picture PictureCompare	
		{ 
			get { return GetPictureResource("compare.png"); } 
		}

		public static stdole.Picture PictureReconcile	
		{ 
			get { return GetPictureResource("reconcile.png"); } 
		}

		public static stdole.Picture PictureRevert		
		{ 
			get { return GetPictureResource("revert.png"); } 
		}

		private class AxHostConverter : System.Windows.Forms.AxHost
		{
			private AxHostConverter() : base(String.Empty)
			{
			}

			static public stdole.IPictureDisp ImageToPictureDisp(System.Drawing.Image image)
			{
				return GetIPictureDispFromPicture(image) as stdole.IPictureDisp;
			}

			static public System.Drawing.Image PictureDispToImage(stdole.IPictureDisp pictureDisp)
			{
				return GetPictureFromIPicture(pictureDisp);
			}
		}
	}
}

