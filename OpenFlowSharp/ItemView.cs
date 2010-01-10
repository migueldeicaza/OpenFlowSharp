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
using MonoTouch.UIKit;
using System.Drawing;

namespace OpenFlowSharp
{
	public class ItemView : UIView {
		OpenFlowView container;
		float originalImageHeight;
		int number;
		
		public float HorizontalPosition { get; private set; }
		public float VerticalPosition { get; private set; }
		public UIImageView ImageView { get; private set; }
		
		public ItemView (OpenFlowView container, RectangleF bounds) : base (bounds)
		{
			this.container = container;
			
			Opaque = true;
			//BackgroundColor = null;
			
			ImageView = new UIImageView (bounds) {
				Opaque = true
			};
			AddSubview (ImageView);
		}
		
		public void SetImage (UIImage newImage, float imageHeight, float reflectionFraction)
		{
			ImageView.Image = newImage;
			VerticalPosition = imageHeight * reflectionFraction / 2;
			originalImageHeight = imageHeight;
			Frame = new RectangleF (0, 0, newImage.Size.Width, newImage.Size.Height);
		}
		
		public SizeF CalculateNewSize (SizeF baseImageSize, SizeF boundingBox)
		{
			var boundingRatio = boundingBox.Width / boundingBox.Height;
			var originalImageRatio = baseImageSize.Width / baseImageSize.Height;
			
			double newWidth, newHeight;
			if (originalImageRatio > boundingRatio){
				newWidth = boundingBox.Width;
				newHeight = boundingBox.Width * baseImageSize.Height / baseImageSize.Width;
			} else {
				newHeight = boundingBox.Height;
				newWidth = boundingBox.Height * baseImageSize.Width / baseImageSize.Height;
			}
			return new SizeF ((float) newWidth, (float) newHeight);
		}
		
		public int Number {
			get {
				return number;
			}
			set {
				HorizontalPosition = container.CoverSpacing * value;
				number = value;
			}
		}
		
		public override RectangleF Frame {
			get {
				return base.Frame;
			}
			set {
				base.Frame = value;
				if (ImageView != null)
					ImageView.Frame = value;
			}
		}
		
		protected override void Dispose (bool disposing)
		{
			if (disposing)
				ImageView.Dispose ();
			base.Dispose (disposing);
		}

	}
}
