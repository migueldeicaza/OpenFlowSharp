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
using System.Threading;
using FlickrNet;
using System.Drawing;

namespace Sample
{
	public partial class SampleViewController : UIViewController, IOpenFlowDataSource
	{
		const string apiKey = "c0cf24ba43385203b331b578dcaa54eb";
		const string sharedSecret = "670547d41098cd97";
		Flickr flickr;
		Photos photos;
			
		OpenFlowView flowView;
		AutoResetEvent signal = new AutoResetEvent (false);
		Queue<NSAction> tasks = new Queue<NSAction> ();
		
		#region IOpenFlowDataSource implementation
		UIImage PrepareFlickrPhoto (UIImage image, SizeF cropSize)
		{
			// First rescale
			var rect = new RectangleF (0, 0, cropSize.Width, cropSize.Height);
			UIGraphics.BeginImageContext (rect.Size);
			image.Draw (rect);
			var scaledImage = UIGraphics.GetImageFromCurrentImageContext ();
			UIGraphics.EndImageContext ();
			
			// Now crop
			var cropRect = new RectangleF ((scaledImage.Size.Width-cropSize.Width)/2,
			                               (scaledImage.Size.Height-cropSize.Height)/2,
			                               cropSize.Width, cropSize.Height);

			UIGraphics.BeginImageContext (cropRect.Size);
			var ctx = UIGraphics.GetCurrentContext ();
			
			// Compensate for Quartz coordinates
			ctx.TranslateCTM (0.0f, cropRect.Size.Height);
			ctx.ScaleCTM (1, -1);
			
			// Draw view into context
			ctx.DrawImage (new RectangleF (-cropRect.X, cropRect.Y - (image.Size.Height - cropRect.Size.Height), image.Size.Width, image.Size.Height), image.CGImage);
			
			// Create UIImage from context
			var newImage = UIGraphics.GetImageFromCurrentImageContext ();
			UIGraphics.EndImageContext ();
			
			return newImage;
		}
		
		SizeF CalculateSizeForCroppingBox (UIImage image, int width, int height)
		{
			float newHeight, newWidth;
			
			if (image.Size.Width < image.Size.Height){
				newWidth = width;
				newHeight = width * (image.Size.Height / image.Size.Width);
			} else {
				newHeight = height;
				newWidth = height * (image.Size.Width / image.Size.Height);
			}
			return new SizeF (newWidth, newHeight);
		}
		
		void IOpenFlowDataSource.RequestImage (OpenFlowView view, int index)
		{
			NSAction task;
			
			if (flickr == null){
				task = delegate {
					var img = UIImage.FromFile ("images/" + index + ".jpg");
					InvokeOnMainThread (delegate {
						flowView [index] = img;
					});
				};
			} else {
				task = delegate {
					var data = NSData.FromUrl (new NSUrl (photos [index].SmallUrl));
					var image = UIImage.LoadFromData (data);
					
					if (image != null){
						InvokeOnMainThread (delegate {
							image = PrepareFlickrPhoto (image, CalculateSizeForCroppingBox (image, 255, 255));
						
							flowView [index] = image;
						});
					}
				};
			}
			lock (tasks){
				tasks.Enqueue (task);
			}
			signal.Set ();
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
		
		void LoadAllImages ()
		{
				// Load images all at once
				for (int i = 0; i < 30; i++){
					var img = UIImage.FromFile ("images/" + i + ".jpg");
					flowView [i] = img;
				}
				flowView.NumberOfImages = 30;
		}
		
		void Initialize ()
		{
			flowView = new OpenFlowView (UIScreen.MainScreen.Bounds, this);
			View = flowView;
			
			using (var alertView = new UIAlertView ("OpenFlowSharp Demo Data Source",
				"Would you like to download images from Flickr or use 30 sample images included with this project?",
				null, "Flickr",
				"Samples (all at once)",
				"Samples (using threads)")){
				alertView.Dismissed += delegate(object sender, UIButtonEventArgs e) {
					switch (e.ButtonIndex){
					// Flickr
					case 0:
						flickr = new Flickr (apiKey, sharedSecret);
						new Thread (Worker).Start ();
						
						tasks.Enqueue (delegate {
							try {
								photos = flickr.InterestingnessGetList ();
								InvokeOnMainThread (delegate {
									flowView.NumberOfImages = photos.Count;
								});
							} catch {
								InvokeOnMainThread (delegate {
									using (var alert = new UIAlertView ("Error", "While accessing Flickr", null, "Ok")){
										alert.Show ();
									}
								});
							}
						});
						
						break;
						
					// Sync case, load all images at startup
					case 1:
						LoadAllImages ();
						break;
						
					// Load images on demand on a worker thread
					case 2:
						flowView.NumberOfImages = 30;
						new Thread (Worker).Start ();
						break;
					}
				};
			    alertView.Show ();
			}
		}

		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return true;
		}
		//
		// Dispatches the tasks queued in the tasks queue
		// 
		void Worker ()
		{
			while (signal.WaitOne ()){
				while (true){
					NSAction task;
				
					lock (tasks){
						if (tasks.Count > 0)
							task = tasks.Dequeue ();
						else
							break;
					}
					task ();
				}
			}
		}
		#endregion
	}
}
