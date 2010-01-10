// Copyright (c) 2009 Alex Fajkowski, Apparent Logic LLC
// C# port Copyright (C) 2010 Miguel de Icaza.
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using OpenFlowSharp;

namespace Sample
{
	public partial class SampleViewController : UIViewController, IOpenFlowDataSource
	{
		OpenFlowView flowView;
		
		#region IOpenFlowDataSource implementation
		void IOpenFlowDataSource.RequestImage (OpenFlowView view, int index)
		{
			throw new NotImplementedException ();
		}
		
		
		UIImage IOpenFlowDataSource.GetDefaultImage ()
		{
			return UIImage.FromFile ("default.png");
		}
		
		#endregion
		
		#region Constructors
		public SampleViewController () : base("SampleViewController", null)
		{
			Initialize ();
		}
		
		void Initialize ()
		{
			flowView = new OpenFlowView (this);
			View.AddSubview (flowView);
			
			using (var alertView = new UIAlertView ("OpenFlowSharp Demo Data Source",
				"Would you like to download images from Flickr or use 30 sample images included with this project?",
				null, "Flickr",
				"Samples (all at once)",
				"Samples (using threads)")){
				alertView.Dismissed += delegate(object sender, UIButtonEventArgs e) {
					switch (e.ButtonIndex){
					case 0:
						// TODO
					case 1:
						// Load images all at once
						for (int i = 0; i < 30; i++)
							flowView [i] = UIImage.FromFile ("images/" + i + ".jpg");
						flowView.NumberOfImages = 30;
						break;
					case 2:
						flowView.NumberOfImages = 30;
						break;
					}
				};
			    alertView.Show ();
			}
		}

		#endregion
	}
}
